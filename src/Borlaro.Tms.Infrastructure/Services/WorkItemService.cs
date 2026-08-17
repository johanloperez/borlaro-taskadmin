using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Notifications;
using Borlaro.Tms.Infrastructure.Realtime;
using Borlaro.Tms.Infrastructure.Storage;

namespace Borlaro.Tms.Infrastructure.Services;

public class DomainException(string message) : Exception(message);

public record CreateWorkItemRequest(
    string Title,
    string? DescriptionMd,
    string? Type,
    WorkItemPriority Priority,
    Guid? AssigneeId,
    decimal? Estimate,
    DateOnly? DueDate,
    JsonObject? CustomFields,
    /// <summary>Obligatoria. Sin default: ver `WorkItem.Difficulty`.</summary>
    WorkItemDifficulty Difficulty);

public record UpdateWorkItemRequest(
    string? Title,
    string? DescriptionMd,
    string? Type,
    WorkItemPriority? Priority,
    Guid? AssigneeId,
    bool ClearAssignee,
    decimal? Estimate,
    DateOnly? DueDate,
    int? ProgressPct,
    JsonObject? CustomFields,
    bool? AssigneeCanMove = null,
    bool? AssigneeCanEdit = null,
    bool? AssigneeCanDelete = null,
    WorkItemDifficulty? Difficulty = null);

public record TransitionRequest(Guid ToStageId, string? BlockerReason, double? SortOrder);

/// <summary>Toda mutación de un work item pasa por acá — la UI y el agente de IA usan los mismos
/// métodos. Eso garantiza que las reglas de workflow y el historial se apliquen igual sin
/// importar quién pida el cambio.
///
/// Y es también por eso que el aviso en vivo se emite acá y no en los endpoints: si viviera en la
/// API, un cambio hecho por el agente —que no pasa por HTTP— no llegaría a ninguna pantalla.</summary>
// `boardEvents` y no `events`: adentro de UpdateAsync hay una lista local llamada `events`, de
// WorkItemEvent, que es otra cosa. Dos nombres iguales para el historial y para el aviso en vivo
// es una trampa de lectura.
public class WorkItemService(
    BorlaroTmsDbContext db,
    IFileStore files,
    IBoardEvents boardEvents,
    HandoffService handoffs,
    FeedService feed)
{
    /// <summary>Anuncia el cambio, siempre después de guardar.
    ///
    /// El orden importa: si se avisara antes del SaveChanges, el cliente podría volver a pedir el
    /// tablero y leer el estado viejo — una carrera que se ve como «a veces no se actualiza».
    ///
    /// Y nunca puede tumbar la operación: la tarea ya se movió y la transacción cerró. Que el hub
    /// esté caído significa que alguien va a tener que refrescar a mano, no que el cambio se
    /// pierda.</summary>
    private async Task AnunciarAsync(
        WorkItem item, string projectKey, string kind, ActorType actorType, CancellationToken ct)
    {
        try
        {
            await boardEvents.PublishAsync(
                new BoardEvent(projectKey, item.Id, $"{projectKey}-{item.Number}", kind,
                               actorType == ActorType.Agent),
                ct);
        }
        catch
        {
            // Ver arriba: el aviso es mejor-esfuerzo por diseño.
        }
    }

    public async Task<WorkItem> CreateAsync(
        string projectKey,
        CreateWorkItemRequest request,
        ActorType actorType,
        Guid? actorId,
        Guid? checkInId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new DomainException("El título es obligatorio.");
        }

        var project = await db.Projects
            .Include(p => p.CustomFields)
            .Include(p => p.Workflow!).ThenInclude(w => w.Stages)
            .FirstOrDefaultAsync(p => p.Key == projectKey.ToUpperInvariant(), ct)
            ?? throw new DomainException($"No existe el proyecto «{projectKey}».");

        // Archivar tiene que significar algo: si se puede seguir cargando trabajo, es solo una
        // etiqueta que oculta el proyecto de una lista.
        if (project.IsArchived)
        {
            throw new DomainException(
                $"El proyecto «{project.Key}» está archivado. Reactivalo para cargarle trabajo nuevo.");
        }

        if (request.Type is not null &&
            project.WorkItemTypes.Count > 0 &&
            !project.WorkItemTypes.Contains(request.Type))
        {
            throw new DomainException(
                $"«{request.Type}» no es un tipo válido en este proyecto. " +
                $"Válidos: {string.Join(", ", project.WorkItemTypes)}.");
        }

        var errors = CustomFieldValidator.Validate(request.CustomFields, project.CustomFields.ToList());
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors.Select(e => e.Message)));
        }

        var firstStage = project.Workflow!.Stages.OrderBy(s => s.Order).First();

        // La numeración se asigna dentro de una transacción con bloqueo de la fila del proyecto.
        // Sin esto, dos altas simultáneas producen el mismo DEV-142 y una de las dos revienta
        // contra el índice único.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var number = await ReserveNextNumberAsync(project.Id, ct);

        var item = new WorkItem
        {
            ProjectId = project.Id,
            Number = number,
            Title = request.Title.Trim(),
            DescriptionMd = request.DescriptionMd?.Trim() ?? string.Empty,
            Type = request.Type ?? project.WorkItemTypes.FirstOrDefault() ?? "Tarea",
            Priority = request.Priority,
            StageId = firstStage.Id,
            AssigneeId = request.AssigneeId,
            ReporterId = actorType == ActorType.User ? actorId : null,
            Estimate = request.Estimate,
            Difficulty = request.Difficulty,
            DueDate = request.DueDate,
            SortOrder = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CustomFields = request.CustomFields is null
                ? null
                : JsonDocument.Parse(request.CustomFields.ToJsonString())
        };

        db.WorkItems.Add(item);
        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = item.Id,
            ActorType = actorType,
            ActorId = actorId,
            Field = "created",
            NewValue = item.Title,
            CheckInId = checkInId
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await AnunciarAsync(item, projectKey, "created", actorType, ct);
        return item;
    }

    private async Task<int> ReserveNextNumberAsync(Guid projectId, CancellationToken ct)
    {
        // UPDATE ... RETURNING es atómico y toma el lock de la fila: dos altas concurrentes se
        // serializan en la base en vez de competir en memoria.
        //
        // El alias "Value" no es cosmético: SqlQuery<T> de EF Core mapea escalares por una
        // columna con ese nombre exacto, y sin él la consulta compila pero falla en runtime.
        var numbers = await db.Database
            .SqlQuery<int>($"""
                UPDATE "Projects"
                SET "NextItemNumber" = "NextItemNumber" + 1
                WHERE "Id" = {projectId}
                RETURNING "NextItemNumber" - 1 AS "Value"
                """)
            .ToListAsync(ct);

        return numbers.Single();
    }

    public async Task<WorkItem> UpdateAsync(
        Guid itemId,
        UpdateWorkItemRequest request,
        ActorType actorType,
        Guid? actorId,
        Guid? checkInId = null,
        CancellationToken ct = default)
    {
        var item = await db.WorkItems
            .Include(i => i.Project!).ThenInclude(p => p.CustomFields)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("El item no existe.");

        var events = new List<WorkItemEvent>();

        void Track(string field, string? oldValue, string? newValue)
        {
            if (oldValue == newValue) return;
            events.Add(new WorkItemEvent
            {
                WorkItemId = item.Id,
                ActorType = actorType,
                ActorId = actorId,
                Field = field,
                OldValue = oldValue,
                NewValue = newValue,
                CheckInId = checkInId
            });
        }

        if (request.Title is not null)
        {
            Track("title", item.Title, request.Title.Trim());
            item.Title = request.Title.Trim();
        }

        if (request.DescriptionMd is not null)
        {
            Track("description", null, "(actualizada)");
            item.DescriptionMd = request.DescriptionMd;
        }

        if (request.Type is not null)
        {
            if (item.Project!.WorkItemTypes.Count > 0 && !item.Project.WorkItemTypes.Contains(request.Type))
            {
                throw new DomainException($"«{request.Type}» no es un tipo válido en este proyecto.");
            }
            Track("type", item.Type, request.Type);
            item.Type = request.Type;
        }

        if (request.Priority is not null)
        {
            Track("priority", item.Priority.ToString(), request.Priority.ToString());
            item.Priority = request.Priority.Value;
        }

        // Desasignar y "no tocar la asignación" son cosas distintas: un null en el request no
        // puede significar las dos, por eso el flag explícito.
        if (request.ClearAssignee)
        {
            Track("assignee", item.AssigneeId?.ToString(), null);
            item.AssigneeId = null;
        }
        else if (request.AssigneeId is not null)
        {
            Track("assignee", item.AssigneeId?.ToString(), request.AssigneeId.ToString());
            item.AssigneeId = request.AssigneeId;
        }

        // La estimación original se corrige mientras la tarea no arrancó, y después se congela.
        //
        // Las dos mitades importan. Corregir un número mal tipeado el primer día es legítimo y
        // prohibirlo obligaría a rehacer la tarea. Bajarlo cuando ya se agregaron doce horas es
        // otra cosa: hace que el trabajo entre en lo estimado retroactivamente, y borra
        // exactamente el dato que esta sección existe para conservar —cuánto se subestimó—.
        //
        // «Arrancó» es tener avance o alguna ampliación. Lo que sí se puede siempre es agregar
        // horas, que es el camino correcto para decir que hace falta más tiempo (§19 del plan).
        if (request.Estimate is not null && request.Estimate != item.Estimate)
        {
            if (item.AddedHours > 0 || item.ProgressPct > 0)
            {
                throw new DomainException(
                    "La estimación original no se puede cambiar en una tarea que ya arrancó. " +
                    "Si hace falta más tiempo, agregalo como ampliación: así queda cuánto se " +
                    "estimó al principio y cuánto costó de verdad.");
            }

            Track("estimate", item.Estimate?.ToString(), request.Estimate.ToString());
            item.Estimate = request.Estimate;
        }

        if (request.Difficulty is { } dificultad && dificultad != item.Difficulty)
        {
            Track("difficulty", item.Difficulty.ToString(), dificultad.ToString());
            item.Difficulty = dificultad;
        }

        // Los permisos del responsable quedan en el historial como cualquier otro cambio: «quién
        // pudo mover esta tarea y desde cuándo» es exactamente el tipo de pregunta que aparece
        // cuando algo se movió y nadie sabe quién.
        if (request.AssigneeCanMove is bool canMove)
        {
            Track("permiso:mover", item.AssigneeCanMove.ToString(), canMove.ToString());
            item.AssigneeCanMove = canMove;
        }

        if (request.AssigneeCanEdit is bool canEdit)
        {
            Track("permiso:editar", item.AssigneeCanEdit.ToString(), canEdit.ToString());
            item.AssigneeCanEdit = canEdit;
        }

        if (request.AssigneeCanDelete is bool canDelete)
        {
            Track("permiso:borrar", item.AssigneeCanDelete.ToString(), canDelete.ToString());
            item.AssigneeCanDelete = canDelete;
        }

        if (request.DueDate is not null)
        {
            Track("due_date", item.DueDate?.ToString("yyyy-MM-dd"), request.DueDate?.ToString("yyyy-MM-dd"));
            item.DueDate = request.DueDate;

            // Fecha nueva, cuenta nueva: si sigue vencida con la fecha corrida, hay que volver a
            // avisar. Sin esto, una tarea que se reprograma y se vuelve a vencer no avisa nunca
            // más, porque ya figura avisada.
            item.OverdueNotifiedAt = null;
        }

        if (request.ProgressPct is not null)
        {
            var pct = Math.Clamp(request.ProgressPct.Value, 0, 100);
            Track("progress", item.ProgressPct.ToString(), pct.ToString());
            item.ProgressPct = pct;
        }

        if (request.CustomFields is not null)
        {
            var defs = item.Project!.CustomFields.ToList();
            var errors = CustomFieldValidator.Validate(request.CustomFields, defs);
            if (errors.Count > 0)
            {
                throw new DomainException(string.Join(" ", errors.Select(e => e.Message)));
            }

            // Un evento por campo cambiado, no uno por "custom_fields": el historial tiene que
            // decir qué cambió, no que algo cambió.
            var previous = item.CustomFields is null
                ? new JsonObject()
                : JsonNode.Parse(item.CustomFields.RootElement.GetRawText())!.AsObject();

            foreach (var def in defs)
            {
                var before = previous.TryGetPropertyValue(def.Key, out var b) ? b?.ToJsonString() : null;
                var after = request.CustomFields.TryGetPropertyValue(def.Key, out var a) ? a?.ToJsonString() : null;
                Track($"field:{def.Key}", before, after);
            }

            item.CustomFields = JsonDocument.Parse(request.CustomFields.ToJsonString());
        }

        if (events.Count > 0)
        {
            item.UpdatedAt = DateTimeOffset.UtcNow;
            db.WorkItemEvents.AddRange(events);
            await db.SaveChangesAsync(ct);

            // Solo si algo cambió de verdad: un PUT que no modifica nada no tiene por qué hacer
            // que todas las pantallas abiertas vuelvan a pedir el tablero.
            await AnunciarAsync(item, item.Project!.Key, "updated", actorType, ct);
        }

        return item;
    }

    /// <summary>Borra una tarea con todo lo que cuelga de ella: comentarios, historial, bloqueos,
    /// entregables y sus archivos.
    ///
    /// Es para errores —la tarea duplicada, la que se cargó en el proyecto equivocado—, no para
    /// trabajo que pasó: eso se cierra moviéndolo a una etapa final, que conserva el registro.
    /// Acá el registro se va con la tarea, y por eso lo pide una confirmación explícita arriba.</summary>
    public async Task<string> DeleteAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await db.WorkItems
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("La tarea no existe.");

        var readable = $"{item.Project!.Key}-{item.Number}";
        var projectKey = item.Project.Key;

        // Las rutas se leen antes de borrar las filas: después no hay de dónde sacarlas.
        var storedPaths = await db.DeliverableVersions
            .Where(v => v.Deliverable!.WorkItemId == itemId && v.FilePath != null)
            .Select(v => v.FilePath!)
            .ToListAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Restrict en BlockingItemId: otra tarea puede estar esperando a esta. Se corta el
        // vínculo en las dos direcciones.
        await db.WorkItemDependencies
            .Where(d => d.BlockedItemId == itemId || d.BlockingItemId == itemId)
            .ExecuteDeleteAsync(ct);

        // Restrict en DeliverableVersionId: las rondas se van antes que las versiones que revisan.
        await db.ReviewRounds
            .Where(r => r.WorkItemId == itemId || r.DeliverableVersion!.Deliverable!.WorkItemId == itemId)
            .ExecuteDeleteAsync(ct);

        // El resto cascadea desde WorkItem: comentarios, eventos, bloqueos, enlaces de repo,
        // etiquetas y entregables con sus versiones.
        await db.WorkItems.Where(i => i.Id == itemId).ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);

        foreach (var path in storedPaths)
        {
            try
            {
                await files.DeleteAsync(path, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Un archivo que ya no está no es motivo para fallar: la fila que lo nombraba se
                // fue, que es lo que se pidió.
            }
        }

        // Se anuncia con los datos que se guardaron antes de borrar: después de esto la fila ya no
        // existe y no hay de dónde sacar ni la clave ni el número.
        try
        {
            await boardEvents.PublishAsync(
                new BoardEvent(projectKey, itemId, readable, "deleted", ByAgent: false), ct);
        }
        catch
        {
            // Mejor-esfuerzo, igual que el resto de los avisos.
        }

        return readable;
    }

    /// <summary>Mueve un item de etapa aplicando las reglas del workflow. Abre o cierra el
    /// bloqueo y la ronda de revisión según corresponda.</summary>
    public async Task<WorkItem> TransitionAsync(
        Guid itemId,
        TransitionRequest request,
        ActorType actorType,
        Guid? actorId,
        Guid? checkInId = null,
        CancellationToken ct = default)
    {
        var item = await db.WorkItems
            .Include(i => i.Blockers)
            // El proyecto se trae para poder nombrar el tablero al que hay que avisar. Es la
            // misma consulta con un join más, no una consulta extra por cada movimiento.
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new DomainException("El item no existe.");

        var from = await db.WorkflowStages
            .Include(s => s.AllowedTransitions)
            .FirstAsync(s => s.Id == item.StageId, ct);

        var to = await db.WorkflowStages
            .FirstOrDefaultAsync(s => s.Id == request.ToStageId, ct)
            ?? throw new DomainException("La etapa destino no existe.");

        var check = WorkflowRules.CanTransition(from, to, request.BlockerReason);
        if (!check.IsAllowed)
        {
            throw new DomainException(check.Message!);
        }

        // Los avisos se juntan acá y se mandan después de guardar: notificar adentro de la
        // transacción haría que alguien reciba «te llegó esto» por un cambio que todavía puede
        // fallar al escribirse.
        (User Owner, string? From)? relevo = null;
        var relevoHuerfano = false;

        if (from.Id != to.Id)
        {
            db.WorkItemEvents.Add(new WorkItemEvent
            {
                WorkItemId = item.Id,
                ActorType = actorType,
                ActorId = actorId,
                Field = "stage",
                OldValue = from.Name,
                NewValue = to.Name,
                CheckInId = checkInId
            });

            if (WorkflowRules.OpensBlocker(to))
            {
                db.Blockers.Add(new Blocker
                {
                    WorkItemId = item.Id,
                    Reason = request.BlockerReason!.Trim()
                });
            }
            else
            {
                // Salir de una etapa de bloqueo cierra los bloqueos abiertos: si no, el dashboard
                // seguiría contando como bloqueado algo que ya se destrabó.
                foreach (var open in item.Blockers.Where(b => b.IsOpen))
                {
                    open.ResolvedAt = DateTimeOffset.UtcNow;
                }
            }

            item.ClosedAt = WorkflowRules.ClosesWork(to) ? DateTimeOffset.UtcNow : null;
            if (WorkflowRules.ClosesWork(to)) item.ProgressPct = 100;

            // ── Guardar quién tenía antes (para devoluciones) ────────────────────────────────
            var previousAssignee = item.AssigneeId;
            var previousAssigneeName = previousAssignee.HasValue
                ? await db.Users.Where(u => u.Id == previousAssignee).Select(u => u.Name).FirstOrDefaultAsync(ct)
                : null;

            item.PreviousAssigneeId = previousAssignee;
            item.PreviousStageId = from.Id;
            item.StageId = to.Id;
            item.UpdatedAt = DateTimeOffset.UtcNow;

            // ── El relevo ────────────────────────────────────────────────────────────────────
            // Cuatro pasos, y el primero que contesta manda:
            //
            //   1. Asignación específica de esta tarea para esta etapa. La decidió una persona,
            //      así que le gana a cualquier cálculo.
            //   2. Lista de responsables de la etapa → el de menos carga entre los activos.
            //   3. Titular único de la etapa (`DefaultAssigneeId`), que es como se configuraba
            //      antes de que existieran las listas y sigue siendo lo que edita el encabezado
            //      de columna del tablero. Sin este paso, un proyecto configurado a la vieja deja
            //      de relevar en silencio en cuanto se aplica la migración de las listas.
            //   4. Etapa sin configurar → no cambia de manos, la tarea sigue con quien la tenía.
            //      Es lo correcto en etapas de tránsito como «Bloqueado».
            //
            // La diferencia entre «no hay a quién darle» y «no hay que darle a nadie» es la que
            // decide si esto es un modo de falla o el funcionamiento normal, y por eso se
            // distingue explícitamente más abajo.
            var specificAssignment = await db.WorkItemStageAssignments
                .FirstOrDefaultAsync(a => a.WorkItemId == item.Id && a.StageId == to.Id, ct);

            User? owner = null;
            bool assignedBySystem = false;
            var etapaReparteTrabajo = false;

            if (specificAssignment?.AssignedUserId is { } specificId)
            {
                owner = await db.Users.FirstOrDefaultAsync(u => u.Id == specificId, ct);
                etapaReparteTrabajo = true;
            }
            else if (await db.StageResponsibles.AnyAsync(s => s.StageId == to.Id, ct))
            {
                owner = await CalculateLeastBurdened(to.Id, ct);
                assignedBySystem = true;
                etapaReparteTrabajo = true;
            }
            else if (to.DefaultAssigneeId is not null)
            {
                owner = await handoffs.ResolveOwnerAsync(to, ct);
                etapaReparteTrabajo = true;
            }

            if (owner is not null && owner.Id != item.AssigneeId)
            {
                // El trabajo cambió de manos
                db.WorkItemEvents.Add(new WorkItemEvent
                {
                    WorkItemId = item.Id,
                    ActorType = actorType,
                    ActorId = actorId,
                    Field = "assignee",
                    OldValue = previousAssigneeName,
                    NewValue = owner.Name,
                    CheckInId = checkInId
                });

                item.AssigneeId = owner.Id;
                item.AssignedBySystem = assignedBySystem;
                relevo = (owner, previousAssigneeName);
            }
            else if (owner is null && etapaReparteTrabajo)
            {
                // La etapa reparte trabajo y no quedó nadie a quien dárselo: todos sus
                // responsables están inactivos, o el titular lo está. Dejar la tarea con su dueño
                // anterior la haría desaparecer —figura avanzando, en manos de alguien que ya
                // terminó su parte— así que se deja **sin asignar**, que se ve en el tablero, y
                // se le avisa a quien puede arreglarlo. Un ruteo automático que falla callado es
                // peor que no tener ruteo.
                if (item.AssigneeId is not null)
                {
                    db.WorkItemEvents.Add(new WorkItemEvent
                    {
                        WorkItemId = item.Id,
                        ActorType = actorType,
                        ActorId = actorId,
                        Field = "assignee",
                        OldValue = previousAssigneeName,
                        NewValue = null,
                        CheckInId = checkInId
                    });

                    item.AssigneeId = null;
                    item.AssignedBySystem = false;
                }

                relevoHuerfano = true;
            }
            // Si owner es la misma persona que ya la tenía, no pasa nada: no cambió de manos, y
            // contarle lo que ya sabe es la forma más rápida de que aprenda a ignorar los avisos.
        }

        if (request.SortOrder is not null)
        {
            item.SortOrder = request.SortOrder.Value;
        }

        await db.SaveChangesAsync(ct);

        // El movimiento entre etapas es *el* cambio que se quiere ver sin refrescar: es el que en
        // GitLab o Azure DevOps aparece solo en la pantalla del resto del equipo.
        var itemKey = $"{item.Project!.Key}-{item.Number}";
        await AnunciarAsync(item, item.Project.Key, "moved", actorType, ct);

        // Después de guardar y fuera de la transacción: lo que se avisa ya es un hecho.
        //
        // El relevo se encola en vez de mandarse: si a la misma persona le caen varias tareas
        // seguidas, el barrido las junta en un aviso solo. Ver `HandoffService.Ventana`.
        if (relevo is { } paso)
        {
            handoffs.Enqueue(paso.Owner.Id, item.Id, itemKey, item.Title, to.Name, paso.From);
            await db.SaveChangesAsync(ct);
        }
        else if (relevoHuerfano)
        {
            await handoffs.WarnOrphanStageAsync(itemKey, to.Name, ct);
        }

        // Arrancar sin fecha de entrega no se impide: bloquear el movimiento dejaría a alguien sin
        // poder trabajar por un campo de planificación que no le toca completar, y castigaría a
        // quien ejecuta por la omisión de quien lidera. Se deja pasar y se hace visible, que es lo
        // que el resto del sistema ya hace con la etapa huérfana y con el cambio de fecha.
        if (WorkflowRules.StartsWork(to) && item.DueDate is null)
        {
            var responsable = item.AssigneeId is null
                ? "sin responsable"
                : await db.Users.Where(u => u.Id == item.AssigneeId)
                    .Select(u => u.Name).FirstOrDefaultAsync(ct) ?? "sin responsable";

            await feed.PublicarAsync(
                item.ProjectId,
                FeedService.KindStartedWithoutDate,
                $"{itemKey} arrancó sin fecha de entrega",
                $"«{item.Title}» pasó a {to.Name} y no tiene fecha comprometida — {responsable}.",
                $"/p/{item.Project.Key}?item={item.Id}",
                excluir: null,
                ct);

            await db.SaveChangesAsync(ct);
        }

        return item;
    }

    /// <summary>Calcula quién en la lista de responsables de una etapa tiene menos carga
    /// de trabajo en el momento. Se elige el primero en orden si hay empate.</summary>
    private async Task<User?> CalculateLeastBurdened(Guid stageId, CancellationToken ct = default)
    {
        // Traer los responsables activos de la etapa, en orden
        var responsibles = await db.StageResponsibles
            .Where(s => s.StageId == stageId)
            .Include(s => s.User)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);

        if (responsibles.Count == 0) return null;

        // Filtrar los que estén activos (importante: no asignar trabajo a gente inactiva)
        var activeResponsibles = responsibles
            .Where(s => s.User is not null && s.User.IsActive)
            .ToList();

        if (activeResponsibles.Count == 0) return null;

        if (activeResponsibles.Count == 1) return activeResponsibles[0].User!;

        // Calcular carga: contar tareas abiertas asignadas a cada uno
        var userIds = activeResponsibles.Select(s => s.UserId).ToList();
        var workloadByUser = await db.WorkItems
            .Where(w => userIds.Contains(w.AssigneeId!.Value) && w.ClosedAt == null)
            .AsNoTracking()
            .GroupBy(w => w.AssigneeId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId!.Value, x => x.Count, ct);

        // Encontrar el con menos carga (si no tiene tareas, no está en el diccionario)
        var leastBurdened = activeResponsibles
            .OrderBy(s => workloadByUser.TryGetValue(s.UserId, out var count) ? count : 0)
            .ThenBy(s => s.Order)  // Desempate por orden
            .First();

        return leastBurdened.User;
    }
}
