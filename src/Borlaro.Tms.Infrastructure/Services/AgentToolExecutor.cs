using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Agent;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Notifications;

namespace Borlaro.Tms.Infrastructure.Services;

/// <summary>Ejecuta las herramientas del agente contra el dominio. Es la frontera donde la
/// conversación se convierte en cambios reales del tablero, así que acá se aplican tres reglas
/// sin excepción: se usan los mismos servicios que la UI (nada de atajos a la base), toda
/// acción queda trazada al check-in que la originó, y lo que compromete a terceros se propone
/// en vez de aplicarse.</summary>
public class AgentToolExecutor(
    BorlaroTmsDbContext db,
    WorkItemService workItems,
    IEnumerable<INotificationChannel> channels,
    FeedService feed)
{
    public async Task<ToolResult> ExecuteAsync(
        ToolCall call,
        CheckIn checkIn,
        CancellationToken ct = default)
    {
        var action = new AgentAction
        {
            CheckInId = checkIn.Id,
            ToolName = call.Name,
            Arguments = JsonDocument.Parse(call.Arguments.ToJsonString()),
            Status = AgentTools.RequireApproval.Contains(call.Name)
                ? AgentActionStatus.PendingApproval
                : AgentActionStatus.Applied
        };

        try
        {
            var (message, workItemId) = await DispatchAsync(call, checkIn, action.Status, ct);

            action.WorkItemId = workItemId;
            action.Result = message;
            action.AppliedAt = action.Status == AgentActionStatus.Applied ? DateTimeOffset.UtcNow : null;

            db.AgentActions.Add(action);
            await db.SaveChangesAsync(ct);

            return new ToolResult(call.Id, message, false);
        }
        // El error se devuelve al modelo como resultado de herramienta, no como excepción: así
        // puede corregirse solo —probar otra etapa, pedir el dato que falta— en vez de cortar la
        // conversación. Es lo que hace tolerable un modelo chico.
        //
        // Los tres tipos de abajo no son lo mismo pero se tratan igual, y a propósito: un
        // argumento con el tipo cambiado es un error del modelo, no del servidor, y un 500 en
        // medio de un check-in pierde la conversación entera por un número mal escrito.
        catch (Exception ex) when (ex is DomainException or InvalidOperationException
                                      or FormatException or JsonException)
        {
            action.Status = AgentActionStatus.Rejected;
            action.IsError = true;
            action.Result = ex.Message;

            db.AgentActions.Add(action);
            await db.SaveChangesAsync(ct);

            return new ToolResult(call.Id, $"No se pudo: {ex.Message}", true);
        }
    }

    private async Task<(string Message, Guid? WorkItemId)> DispatchAsync(
        ToolCall call,
        CheckIn checkIn,
        AgentActionStatus status,
        CancellationToken ct) => call.Name switch
    {
        AgentTools.GetAssignedTasks => (await DescribeTasksAsync(checkIn.UserId, ct), null),
        AgentTools.UpdateTaskStatus => await UpdateStatusAsync(call, checkIn, ct),
        AgentTools.LogProgress => await LogProgressAsync(call, checkIn, ct),
        AgentTools.SetCustomField => await SetCustomFieldAsync(call, checkIn, ct),
        AgentTools.FlagBlocker => await FlagBlockerAsync(call, checkIn, ct),
        AgentTools.ReprioritizeDay => await ReprioritizeAsync(call, checkIn, ct),
        AgentTools.RequestDateChange => await RequestDateChangeAsync(call, checkIn, status, ct),
        AgentTools.EscalateToManager => await EscalateAsync(call, checkIn, ct),
        AgentTools.CreateFollowupTask => await CreateFollowupAsync(call, status, ct),
        AgentTools.AddTimeEstimate => await AddTimeEstimateAsync(call, checkIn, ct),
        _ => throw new DomainException($"La herramienta «{call.Name}» no existe.")
    };

    // ── Lectura ──────────────────────────────────────────────────────────────

    public async Task<string> DescribeTasksAsync(Guid userId, CancellationToken ct)
    {
        var items = await db.WorkItems
            .AsNoTracking()
            // Archivar un proyecto es darlo por terminado, y eso incluye dejar de perseguir a la
            // gente por lo que quedó adentro. Sin este filtro, una tarea abierta en un proyecto
            // cerrado hace meses sigue siendo «trabajo asignado» para el check-in de mañana.
            .Where(i => i.AssigneeId == userId && i.ClosedAt == null && !i.Project!.IsArchived)
            .Include(i => i.Project)
            .Include(i => i.Stage)
            .Include(i => i.Blockers.Where(b => b.ResolvedAt == null))
            .OrderBy(i => i.DueDate == null).ThenBy(i => i.DueDate)
            .ToListAsync(ct);

        if (items.Count == 0) return "La persona no tiene trabajo abierto asignado.";

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var lines = items.Select(i =>
        {
            var id = $"{i.Project!.Key}-{i.Number}";
            var due = i.DueDate is null
                ? "sin fecha"
                : i.DueDate < today
                    ? $"VENCIDA el {i.DueDate:dd/MM}"
                    : $"vence {i.DueDate:dd/MM}";
            var blocker = i.Blockers.FirstOrDefault();
            var blocked = blocker is null ? "" : $" | BLOQUEADA: {blocker.Reason}";
            var stale = (DateTimeOffset.UtcNow - i.UpdatedAt).Days;

            // El tiempo se cuenta como «original + ampliado» y no como un total, porque es la
            // forma en que el agente tiene que pensarlo: lo que se dijo al principio no se toca, y
            // lo que se agrega después es lo que hay que preguntar.
            var tiempo = i.Estimate is null
                ? "sin estimar"
                : i.AddedHours > 0
                    ? $"estimada {i.Estimate:0.##} h + {i.AddedHours:0.##} h agregadas = {i.Estimate + i.AddedHours:0.##} h"
                    : $"estimada {i.Estimate:0.##} h";

            // La etiqueta del proyecto va primero y el nivel canónico entre paréntesis. Las dos
            // cosas hacen falta: la etiqueta es la palabra que la persona ve y con la que va a
            // contestar, y el nivel es lo único que le dice al modelo cuál extremo de la escala
            // es. Con solo la etiqueta —«Compleja»— tendría que adivinar si eso es mucho o poco.
            var etiqueta = i.Difficulty switch
            {
                WorkItemDifficulty.Baja => i.Project!.DifficultyLabelLow,
                WorkItemDifficulty.Media => i.Project!.DifficultyLabelMedium,
                _ => i.Project!.DifficultyLabelHigh
            };

            var dificultad = string.IsNullOrWhiteSpace(etiqueta)
                ? $" | dificultad {i.Difficulty}"
                : $" | dificultad {etiqueta} ({i.Difficulty})";

            // La categoría entre paréntesis y no solo el nombre: «Redacción» no le dice al modelo
            // si eso es trabajo en curso o una fila de espera, y sin esa distinción no puede
            // preguntar «¿por dónde arrancás hoy?» ni saber qué está efectivamente haciendo.
            var etapa = $"{i.Stage!.Name} ({i.Stage.Category})";

            var sinFecha = i.DueDate is null && i.Stage.Category == StageCategory.InProgress
                ? " | SIN FECHA DE ENTREGA"
                : "";

            return $"- {id} «{i.Title}» | etapa {etapa} | {due}{sinFecha} | avance {i.ProgressPct}% " +
                   $"| {tiempo}{dificultad} | sin cambios hace {stale} día(s){blocked}";
        });

        return string.Join("\n", lines);
    }

    // ── Mutaciones ───────────────────────────────────────────────────────────

    private async Task<(string, Guid?)> UpdateStatusAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var item = await ResolveItemAsync(call, ct);
        var stageName = Required(call, "stage_name");

        var stage = await db.WorkflowStages
            .FirstOrDefaultAsync(s => s.WorkflowId == item.Project!.WorkflowId
                                   && s.Name.ToLower() == stageName.ToLower(), ct)
            ?? throw new DomainException(
                $"«{stageName}» no es una etapa de este proyecto. Etapas válidas: " +
                string.Join(", ", await db.WorkflowStages
                    .Where(s => s.WorkflowId == item.Project!.WorkflowId)
                    .OrderBy(s => s.Order).Select(s => s.Name).ToListAsync(ct)));

        var etapaAnterior = item.Stage?.Name ?? "otra etapa";

        await workItems.TransitionAsync(
            item.Id,
            new TransitionRequest(stage.Id, Optional(call, "blocker_reason"), null),
            ActorType.Agent, null, checkIn.Id, ct);

        // Que una tarea cambie de etapa porque alguien lo dijo en una conversación —y no porque
        // la arrastró en el tablero— es precisamente lo que quien lidera no tiene forma de ver.
        var persona = await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);

        await feed.PublicarAsync(
            item.ProjectId,
            FeedService.KindStageChangedByAgent,
            $"{Readable(item)}: {etapaAnterior} → {stage.Name}",
            $"El agente la movió durante el check-in de {persona.Name}: «{item.Title}».",
            $"/p/{item.Project?.Key}?item={item.Id}",
            excluir: persona.Id,
            ct);

        return ($"{Readable(item)} movida a «{stage.Name}».", item.Id);
    }

    private async Task<(string, Guid?)> LogProgressAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var item = await ResolveItemAsync(call, ct);
        var pct = (int)Number(call, "progress_pct");

        await workItems.UpdateAsync(
            item.Id,
            new UpdateWorkItemRequest(null, null, null, null, null, false, null, null, pct, null),
            ActorType.Agent, null, checkIn.Id, ct);

        var note = Optional(call, "note");
        if (!string.IsNullOrWhiteSpace(note))
        {
            db.WorkItemEvents.Add(new WorkItemEvent
            {
                WorkItemId = item.Id,
                ActorType = ActorType.Agent,
                Field = "agent_note",
                NewValue = note,
                CheckInId = checkIn.Id
            });
        }

        return ($"Avance de {Readable(item)} registrado en {pct}%.", item.Id);
    }

    /// <summary>Registra que una tarea necesita más horas de las estimadas.
    ///
    /// Se aplica en el acto, a diferencia de `request_date_change`, que va a la cola de
    /// aprobación. La distinción es deliberada: mover una fecha cambia un compromiso con un
    /// tercero y no lo puede decidir quien se atrasó; agregar horas no cambia ningún compromiso,
    /// registra lo que ya está pasando. Un registro de tiempo que depende de que alguien lo
    /// apruebe llega tarde o no llega, y entonces deja de ser un registro.
    ///
    /// Lo que sí es innegociable es que avise: es el único momento en que el sistema dice «esto
    /// está costando más de lo que se dijo» mientras todavía se puede hacer algo.</summary>
    private async Task<(string, Guid?)> AddTimeEstimateAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var item = await ResolveItemAsync(call, ct);
        var horas = (decimal)Number(call, "hours");

        // El esquema ya acota el rango, pero un modelo local puede mandar cualquier cosa y el
        // dominio no puede confiar en que el esquema se haya respetado.
        if (horas <= 0)
        {
            throw new DomainException("Las horas adicionales tienen que ser mayores que cero.");
        }

        var motivo = Optional(call, "reason") ?? string.Empty;

        var anterior = item.AddedHours;

        // Se cargan las ampliaciones para poder recalcular el total desde ellas en vez de sumarle
        // al denormalizado: con dos caminos de escritura —agregar y anular— sumar de a poco
        // garantiza que tarde o temprano el total y la tabla cuenten cosas distintas.
        await db.Entry(item).Collection(i => i.TimeExtensions).LoadAsync(ct);

        var ampliacion = new WorkItemTimeExtension
        {
            WorkItemId = item.Id,
            Hours = horas,
            Reason = motivo,
            ActorType = ActorType.Agent,
            CheckInId = checkIn.Id
        };

        // Se registra en el DbSet y **además** en la colección, y las dos cosas son necesarias.
        //
        // Solo por la colección no alcanza: la entidad nace con su `Id` ya asignado, y EF la toma
        // por existente y emite un UPDATE en vez de un INSERT. Como la fila todavía no está, la
        // sentencia afecta cero filas y `SaveChanges` tira `DbUpdateConcurrencyException` — que
        // llegaba al agente como un 500 al responderle.
        //
        // Y solo por el DbSet tampoco: `RecalcularAddedHours` suma sobre la colección cargada, así
        // que si la ampliación no está ahí el total queda sin contarla.
        db.WorkItemTimeExtensions.Add(ampliacion);
        item.TimeExtensions.Add(ampliacion);

        item.RecalcularAddedHours();
        item.UpdatedAt = DateTimeOffset.UtcNow;

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = item.Id,
            ActorType = ActorType.Agent,
            Field = "added_hours",
            OldValue = anterior.ToString("0.##"),
            NewValue = item.AddedHours.ToString("0.##"),
            CheckInId = checkIn.Id
        });

        var persona = await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);
        var original = item.Estimate?.ToString("0.##") ?? "sin estimar";
        var total = (item.Estimate ?? 0m) + item.AddedHours;

        await feed.PublicarAsync(
            item.ProjectId,
            FeedService.KindTimeExtended,
            $"{Readable(item)} necesita {horas:0.##} h más",
            $"{persona.Name} agregó {horas:0.##} h a «{item.Title}» — {motivo} " +
            $"(original {original} h, total {total:0.##} h).",
            $"/p/{item.Project?.Key}?item={item.Id}",
            excluir: persona.Id,
            ct);

        return ($"Registradas {horas:0.##} horas más en {Readable(item)}. " +
                $"Total comprometido: {total:0.##} h.", item.Id);
    }

    private async Task<(string, Guid?)> SetCustomFieldAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var item = await ResolveItemAsync(call, ct);
        var key = Required(call, "field_key");
        var value = Required(call, "value");

        var def = await db.CustomFieldDefs
            .FirstOrDefaultAsync(f => f.ProjectId == item.ProjectId && f.Key == key, ct)
            ?? throw new DomainException(
                $"El campo «{key}» no existe en este proyecto. Campos válidos: " +
                string.Join(", ", await db.CustomFieldDefs
                    .Where(f => f.ProjectId == item.ProjectId).Select(f => f.Key).ToListAsync(ct)));

        var current = item.CustomFields is null
            ? new JsonObject()
            : JsonNode.Parse(item.CustomFields.RootElement.GetRawText())!.AsObject();

        // El valor llega siempre como texto porque el schema lo declara string; se convierte al
        // tipo real del campo antes de validar, que es donde se rechaza lo que no corresponde.
        current[key] = def.Type switch
        {
            // Invariante y no la cultura del servidor: el modelo escribe «1.5», y con una cultura
            // que usa coma decimal eso se leería como 15.
            CustomFieldType.Number => JsonValue.Create(double.Parse(
                value, System.Globalization.CultureInfo.InvariantCulture)),
            CustomFieldType.Checkbox => JsonValue.Create(bool.Parse(value)),
            CustomFieldType.MultiSelect => new JsonArray(value.Split(',').Select(v => (JsonNode)v.Trim()!).ToArray()),
            _ => JsonValue.Create(value)
        };

        await workItems.UpdateAsync(
            item.Id,
            new UpdateWorkItemRequest(null, null, null, null, null, false, null, null, null, current),
            ActorType.Agent, null, checkIn.Id, ct);

        return ($"Campo «{def.Label}» de {Readable(item)} actualizado a {value}.", item.Id);
    }

    private async Task<(string, Guid?)> FlagBlockerAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var item = await ResolveItemAsync(call, ct);
        var reason = Required(call, "reason");
        var blockedByName = Optional(call, "blocked_by");

        Guid? blockedById = null;
        if (!string.IsNullOrWhiteSpace(blockedByName))
        {
            var person = await db.Users
                .FirstOrDefaultAsync(u => u.Name.ToLower().Contains(blockedByName.ToLower()), ct);
            blockedById = person?.Id;
        }

        db.Blockers.Add(new Blocker
        {
            WorkItemId = item.Id,
            Reason = reason,
            BlockedByUserId = blockedById,
            BlockedByExternal = blockedById is null ? blockedByName : null
        });

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = item.Id,
            ActorType = ActorType.Agent,
            Field = "blocker",
            NewValue = reason,
            CheckInId = checkIn.Id
        });

        await db.SaveChangesAsync(ct);
        return ($"Bloqueo registrado en {Readable(item)}.", item.Id);
    }

    private async Task<(string, Guid?)> ReprioritizeAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var ids = (call.Arguments["task_ids"] as JsonArray)?
            .Select(n => n!.GetValue<string>()).ToList() ?? [];

        if (ids.Count == 0) throw new DomainException("Hay que indicar al menos una tarea.");

        var order = 0d;
        foreach (var readable in ids)
        {
            var item = await FindByReadableIdAsync(readable, ct);
            if (item is null) continue;
            item.SortOrder = order++;
        }

        await db.SaveChangesAsync(ct);
        return ($"Foco del día reordenado: {string.Join(" → ", ids)}.", null);
    }

    private async Task<(string, Guid?)> RequestDateChangeAsync(
        ToolCall call, CheckIn checkIn, AgentActionStatus status, CancellationToken ct)
    {
        var item = await ResolveItemAsync(call, ct);
        var raw = Required(call, "new_due_date");

        if (!DateOnly.TryParse(raw, out var newDate))
        {
            throw new DomainException($"«{raw}» no es una fecha válida. Usá el formato aaaa-mm-dd.");
        }

        // No se aplica: se propone. La fecha es un compromiso con alguien más, y moverla sin
        // que un responsable lo vea convierte al agente en una forma silenciosa de correr
        // deadlines.
        return ($"Propuesta de mover {Readable(item)} al {newDate:dd/MM} registrada. " +
                "Queda pendiente de que la apruebe un responsable; la fecha actual no cambió.", item.Id);
    }

    private async Task<(string, Guid?)> EscalateAsync(ToolCall call, CheckIn checkIn, CancellationToken ct)
    {
        var summary = Required(call, "summary");
        var severity = Optional(call, "severity") ?? "info";

        WorkItem? item = null;
        var readable = Optional(call, "task_id");
        if (!string.IsNullOrWhiteSpace(readable)) item = await FindByReadableIdAsync(readable, ct);

        var person = await db.Users.FirstAsync(u => u.Id == checkIn.UserId, ct);

        var managers = await db.Users
            .Where(u => u.IsActive && (u.Role == UserRole.Manager || u.Role == UserRole.Admin))
            .ToListAsync(ct);

        // El enlace apunta a la tarea concreta cuando la hay: el manager tiene que poder pasar
        // del aviso al lugar donde se resuelve, no al tablero para buscarla.
        var link = item is null ? "/" : $"/p/{item.Project?.Key}?item={item.Id}";
        var body = item is null ? summary : $"{Readable(item)} «{item.Title}» — {summary}";

        foreach (var manager in managers)
        {
            db.Notifications.Add(new Notification
            {
                UserId = manager.Id,
                Channel = NotificationChannel.InApp,
                Kind = $"agent_escalation_{severity}",
                Title = $"{person.Name}: {summary}",
                Body = body,
                LinkUrl = link
            });
        }

        await db.SaveChangesAsync(ct);

        // Y además se empuja al escritorio. Guardarla solo en la base convierte una alerta en
        // algo que el manager descubre cuando entra a la web — que es justo lo contrario de
        // alertar.
        var desktop = channels.FirstOrDefault(c => c.Kind == NotificationChannel.Desktop);
        if (desktop is not null)
        {
            var payload = new NotificationPayload(
                Kind: $"agent_escalation_{severity}",
                Title: $"{person.Name}: {summary}",
                Body: body,
                LinkPath: link,
                CheckInId: null);

            foreach (var manager in managers.Where(m => m.Id != checkIn.UserId))
            {
                if (await desktop.CanDeliverAsync(manager, ct))
                {
                    await desktop.SendAsync(manager, payload, ct);
                }
            }
        }
        return ($"Alerta enviada al responsable ({severity}).", item?.Id);
    }

    private async Task<(string, Guid?)> CreateFollowupAsync(
        ToolCall call, AgentActionStatus status, CancellationToken ct)
    {
        var projectKey = Required(call, "project_key");
        var title = Required(call, "title");

        if (!await db.Projects.AnyAsync(p => p.Key == projectKey.ToUpperInvariant(), ct))
        {
            throw new DomainException($"No existe el proyecto «{projectKey}».");
        }

        // Igual que el cambio de fecha: crear trabajo afecta la carga del equipo, así que se
        // propone y lo aprueba un lead.
        return ($"Tarea «{title}» propuesta en {projectKey.ToUpperInvariant()}. " +
                "Queda pendiente de aprobación; todavía no existe en el tablero.", null);
    }

    // ── Auxiliares ───────────────────────────────────────────────────────────

    private async Task<WorkItem> ResolveItemAsync(ToolCall call, CancellationToken ct)
    {
        var readable = Required(call, "task_id");
        return await FindByReadableIdAsync(readable, ct)
            ?? throw new DomainException($"No encuentro la tarea «{readable}».");
    }

    private async Task<WorkItem?> FindByReadableIdAsync(string readableId, CancellationToken ct)
    {
        var parts = readableId.Trim().Split('-');
        if (parts.Length < 2 || !int.TryParse(parts[^1], out var number)) return null;

        var key = string.Join('-', parts[..^1]).ToUpperInvariant();

        return await db.WorkItems
            .Include(i => i.Project)
            .FirstOrDefaultAsync(i => i.Project!.Key == key && i.Number == number, ct);
    }

    private static string Readable(WorkItem item) => $"{item.Project!.Key}-{item.Number}";

    private static string Required(ToolCall call, string name) =>
        call.Arguments[name]?.GetValue<string>()
        ?? throw new DomainException($"Falta el parámetro «{name}».");

    private static string? Optional(ToolCall call, string name) =>
        call.Arguments[name]?.GetValue<string>();

    /// <summary>Un número puede llegar como entero, como decimal o —con modelos que no respetan
    /// el esquema— como texto. `GetValue&lt;double&gt;()` solo acepta el caso exacto, así que se
    /// pasa por el texto crudo, que siempre funciona.</summary>
    private static double Number(ToolCall call, string name)
    {
        var node = call.Arguments[name]
            ?? throw new DomainException($"Falta el parámetro «{name}».");

        var raw = node.ToJsonString().Trim('"');

        return double.TryParse(raw, System.Globalization.NumberStyles.Any,
                               System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new DomainException($"«{raw}» no es un número válido para «{name}».");
    }
}
