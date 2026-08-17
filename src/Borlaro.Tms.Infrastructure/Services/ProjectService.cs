using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Storage;

namespace Borlaro.Tms.Infrastructure.Services;

public record CreateProjectRequest(
    string Key,
    string Name,
    string? Description,
    Guid TemplateId,
    string? RepoUrl,
    /// <summary>Quiénes trabajan en el proyecto. Se asignan en el alta porque un proyecto sin
    /// nadie no tiene a quién asignarle trabajo ni a quién preguntarle en el check-in.</summary>
    /// <summary>Quiénes lideran el proyecto. No es la lista del equipo: el equipo se deduce de
    /// quién tiene trabajo asignado. Liderar es lo único que no se puede deducir, porque hace
    /// falta desde antes de que exista la primera tarea.</summary>
    IReadOnlyList<Guid>? LeadIds = null,
    /// <summary>Quién lo crea. Queda como líder: alguien tiene que poder asignar la primera
    /// tarea, y si no es quien lo creó hay que ir a molestar a un admin.</summary>
    Guid? CreatedById = null,
    /// <summary>Cómo llama este proyecto a los tres niveles de dificultad. Nulos = los nombres por
    /// defecto. Se renombra la etiqueta, nunca la escala: debajo siguen siendo Baja, Media y Alta,
    /// que es lo que hace que el agente sepa cuál extremo es cuál.</summary>
    string? DifficultyLabelLow = null,
    string? DifficultyLabelMedium = null,
    string? DifficultyLabelHigh = null);

/// <summary>Lo que se lleva puesto el borrado de un proyecto, contado antes de tocar nada.
/// «Archivar» y «Eliminar» quedan a un clic de distancia en el mismo menú, y la diferencia entre
/// los dos no se puede descubrir después.</summary>
public record ProjectDeletionPreview(
    string Key,
    string Name,
    int Items,
    int Comments,
    int Deliverables,
    int StoredFiles,
    int Members);

public class ProjectService(BorlaroTmsDbContext db, IFileStore files)
{
    /// <summary>Materializa una plantilla en un proyecto real: clona el workflow con sus etapas
    /// y transiciones, y las definiciones de campos.
    ///
    /// Se clona en vez de referenciar a propósito: editar la plantilla "Diseño" no debe
    /// reescribir el tablero de un proyecto que lleva seis meses corriendo.</summary>
    /// <summary>Vacío es nulo: una etiqueta en blanco no es una etiqueta, es no haber puesto
    /// ninguna, y guardarla como cadena vacía haría que la interfaz dibuje un nivel sin nombre.</summary>
    private static string? Etiqueta(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<Project> CreateFromTemplateAsync(
        CreateProjectRequest request,
        CancellationToken ct = default)
    {
        var key = request.Key.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(key) || key.Length > 10 || !key.All(char.IsLetterOrDigit))
        {
            throw new InvalidOperationException(
                "La clave del proyecto debe ser alfanumérica, de 1 a 10 caracteres (ej. DEV, DIS, VID).");
        }

        if (await db.Projects.AnyAsync(p => p.Key == key, ct))
        {
            throw new InvalidOperationException($"Ya existe un proyecto con la clave «{key}».");
        }

        var template = await db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == request.TemplateId, ct)
            ?? throw new InvalidOperationException("La plantilla indicada no existe.");

        if (template.Stages.Count == 0)
        {
            throw new InvalidOperationException("La plantilla no define ninguna etapa.");
        }

        var workflow = new Workflow { Name = $"{request.Name} — {template.Name}" };

        // Primero las etapas, para tener sus Id antes de resolver las transiciones por nombre.
        var stagesByName = new Dictionary<string, WorkflowStage>(StringComparer.OrdinalIgnoreCase);
        foreach (var templateStage in template.Stages.OrderBy(s => s.Order))
        {
            var stage = new WorkflowStage
            {
                WorkflowId = workflow.Id,
                Name = templateStage.Name,
                Order = templateStage.Order,
                Category = templateStage.Category,
                RequiresBlockerReason = templateStage.RequiresBlockerReason,
                OpensReviewRound = templateStage.OpensReviewRound
            };
            workflow.Stages.Add(stage);
            stagesByName[stage.Name] = stage;
        }

        var transitions = new List<WorkflowTransition>();
        foreach (var templateStage in template.Stages)
        {
            var from = stagesByName[templateStage.Name];
            foreach (var targetName in templateStage.AllowedNext)
            {
                // Una plantilla con un nombre mal escrito en AllowedNext produciría una etapa
                // sin salida. Es mejor fallar acá que descubrirlo con el tablero trabado.
                if (!stagesByName.TryGetValue(targetName, out var to))
                {
                    throw new InvalidOperationException(
                        $"La plantilla «{template.Name}» declara una transición de «{templateStage.Name}» " +
                        $"a «{targetName}», que no es una etapa de la plantilla.");
                }

                transitions.Add(new WorkflowTransition { FromStageId = from.Id, ToStageId = to.Id });
            }
        }

        var project = new Project
        {
            Key = key,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            TemplateId = template.Id,
            WorkflowId = workflow.Id,
            ItemNounSingular = template.ItemNounSingular,
            ItemNounPlural = template.ItemNounPlural,
            WorkItemTypes = [.. template.WorkItemTypes],
            AgentContext = template.AgentContext,
            RepoUrl = request.RepoUrl?.Trim(),
            DifficultyLabelLow = Etiqueta(request.DifficultyLabelLow),
            DifficultyLabelMedium = Etiqueta(request.DifficultyLabelMedium),
            DifficultyLabelHigh = Etiqueta(request.DifficultyLabelHigh)
        };

        foreach (var field in template.Fields.OrderBy(f => f.Order))
        {
            project.CustomFields.Add(new CustomFieldDef
            {
                ProjectId = project.Id,
                Key = field.Key,
                Label = field.Label,
                Type = field.Type,
                Options = [.. field.Options],
                Required = field.Required,
                Order = field.Order,
                AgentHint = field.AgentHint
            });
        }

        var wanted = (request.LeadIds ?? []).ToList();

        // Quien lo crea lidera, esté o no en la lista —un proyecto sin nadie que pueda asignar la
        // primera tarea nace dependiendo de que un admin lo rescate—, pero solo si tiene rol para
        // liderar. Si no, alguien más tiene que figurar en la lista.
        if (request.CreatedById is Guid creator && !wanted.Contains(creator))
        {
            var creatorRole = await db.Users.Where(u => u.Id == creator).Select(u => (UserRole?)u.Role)
                .FirstOrDefaultAsync(ct);

            if (creatorRole is UserRole role && CanLead(role)) wanted.Insert(0, creator);
        }

        if (wanted.Count == 0)
        {
            throw new DomainException(
                "El proyecto necesita un responsable con rol Admin o Manager. Elegí a alguien de " +
                "esa lista antes de crearlo.");
        }

        foreach (var leadId in await ExistingActiveUsersAsync(wanted, ct, mustLead: true))
        {
            project.Members.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = leadId,
                Role = ProjectRole.Lead
            });
        }

        db.Workflows.Add(workflow);
        db.WorkflowTransitions.AddRange(transitions);
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return project;
    }

    /// <summary>Define quiénes lideran el proyecto. No define el equipo: el equipo son quienes
    /// tienen trabajo asignado, y eso se cambia asignando tareas, no editando una lista.</summary>
    public async Task<IReadOnlyList<ProjectMember>> SetLeadsAsync(
        string projectKey,
        IReadOnlyList<Guid> leadIds,
        CancellationToken ct = default)
    {
        var project = await db.Projects
            .Include(p => p.Members)
            .FirstOrDefaultAsync(p => p.Key == projectKey.ToUpperInvariant(), ct)
            ?? throw new DomainException($"No existe el proyecto «{projectKey}».");

        var wanted = await ExistingActiveUsersAsync(leadIds, ct, mustLead: true);

        // Un proyecto sin líder no lo puede administrar nadie salvo un admin: nadie puede
        // asignar trabajo ni abrir el formulario público. Se rechaza antes de guardar.
        if (wanted.Count == 0)
        {
            throw new DomainException(
                "El proyecto necesita al menos un líder. Sin eso, nadie puede asignar trabajo y " +
                "hay que pedirle a un admin cada cambio.");
        }

        foreach (var gone in project.Members.Where(m => !wanted.Contains(m.UserId)).ToList())
        {
            // Dejar de liderar no desasigna nada: el trabajo que esa persona tenga sigue siendo
            // suyo, y ahora participa como cualquier otro integrante.
            project.Members.Remove(gone);
        }

        foreach (var leadId in wanted.Where(id => project.Members.All(m => m.UserId != id)))
        {
            project.Members.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = leadId,
                Role = ProjectRole.Lead
            });
        }

        await db.SaveChangesAsync(ct);
        return project.Members.ToList();
    }

    /// <summary>Quiénes pueden liderar un proyecto: solo Admin y Manager. Un colaborador puede
    /// tener trabajo asignado en cualquier proyecto, pero responder por uno es un rol de la
    /// instancia, no algo que se reparta por tablero.</summary>
    public static bool CanLead(UserRole role) => role is UserRole.Admin or UserRole.Manager;

    /// <summary>Filtra ids inexistentes o de gente desactivada en vez de reventar: el alta de un
    /// proyecto no puede fallar entera porque en la lista quedó alguien que se dio de baja.
    ///
    /// Con <paramref name="mustLead"/> además rechaza —ruidosamente— a quien no tiene rol para
    /// liderar: acá callarse dejaría un proyecto sin responsable sin decir por qué.</summary>
    private async Task<HashSet<Guid>> ExistingActiveUsersAsync(
        IReadOnlyList<Guid>? ids,
        CancellationToken ct,
        bool mustLead = false)
    {
        if (ids is null || ids.Count == 0) return [];

        var distinct = ids.Distinct().ToList();

        var found = await db.Users
            .Where(u => distinct.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync(ct);

        if (mustLead)
        {
            var invalid = found.Where(u => !CanLead(u.Role)).ToList();
            if (invalid.Count > 0)
            {
                throw new DomainException(
                    $"{string.Join(", ", invalid.Select(u => u.Name))} no puede liderar un proyecto: " +
                    "hace falta rol Admin o Manager en la instancia. Cambiale el rol desde Personas " +
                    "o elegí a otra persona.");
            }
        }

        return found.Select(u => u.Id).ToHashSet();
    }

    /// <summary>Cuenta qué hay adentro sin borrar nada, para que la confirmación diga números
    /// concretos en vez de «esta acción no se puede deshacer».</summary>
    public async Task<ProjectDeletionPreview> DeletionPreviewAsync(
        Guid projectId,
        CancellationToken ct = default)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new DomainException("El proyecto no existe.");

        return new ProjectDeletionPreview(
            project.Key,
            project.Name,
            await db.WorkItems.CountAsync(i => i.ProjectId == projectId, ct),
            await db.WorkItemComments.CountAsync(c => c.WorkItem!.ProjectId == projectId, ct),
            await db.Deliverables.CountAsync(d => d.WorkItem!.ProjectId == projectId, ct),
            await db.DeliverableVersions.CountAsync(
                v => v.Deliverable!.WorkItem!.ProjectId == projectId && v.FilePath != null, ct),
            await db.ProjectMembers.CountAsync(m => m.ProjectId == projectId, ct));
    }

    /// <summary>Borrado definitivo, con todo lo que cuelga del proyecto. Archivar es para el
    /// proyecto que terminó y cuyo historial es el registro de lo que hizo el equipo; esto es para
    /// el que nunca tendría que haber existido —una prueba, un duplicado, una clave mal puesta—.
    ///
    /// El orden no es decorativo. El grafo de FKs tiene tres aristas que no cascadean y que
    /// abortarían el borrado a mitad de camino: la dependencia cuyo bloqueante vive en este
    /// proyecto, la versión de entregable que cuelga de una ronda de revisión, y la etapa a la que
    /// apunta un work item. Se limpian a mano y en este orden, todo en una transacción: un borrado
    /// a medias deja un tablero que no abre.</summary>
    public async Task<ProjectDeletionPreview> DeleteAsync(Guid projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new DomainException("El proyecto no existe.");

        var summary = await DeletionPreviewAsync(projectId, ct);
        var workflowId = project.WorkflowId;

        // Las rutas se leen antes de borrar las filas: después no hay de dónde sacarlas. Los
        // archivos se borran recién con la transacción cerrada — si algo falla, la base vuelve
        // atrás, y unos bytes de más en disco son mejores que una fila que nombra un archivo
        // que ya no existe.
        var storedPaths = await db.DeliverableVersions
            .Where(v => v.Deliverable!.WorkItem!.ProjectId == projectId && v.FilePath != null)
            .Select(v => v.FilePath!)
            .ToListAsync(ct);

        var itemIds = await db.WorkItems
            .Where(i => i.ProjectId == projectId)
            .Select(i => i.Id)
            .ToListAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (itemIds.Count > 0)
        {
            // Restrict en BlockingItemId: una tarea de otro proyecto puede estar esperando a una de
            // este. El vínculo se corta en las dos direcciones.
            await db.WorkItemDependencies
                .Where(d => itemIds.Contains(d.BlockedItemId) || itemIds.Contains(d.BlockingItemId))
                .ExecuteDeleteAsync(ct);

            // Restrict en DeliverableVersionId: las rondas se van antes que las versiones que revisan.
            await db.ReviewRounds
                .Where(r => itemIds.Contains(r.WorkItemId)
                         || itemIds.Contains(r.DeliverableVersion!.Deliverable!.WorkItemId))
                .ExecuteDeleteAsync(ct);

            // El resto cascadea desde WorkItem: comentarios, eventos, bloqueos, enlaces de repo,
            // etiquetas asignadas y entregables con sus versiones.
            await db.WorkItems.Where(i => i.ProjectId == projectId).ExecuteDeleteAsync(ct);
        }

        // Miembros, campos personalizados y etiquetas cascadean con el proyecto.
        await db.Projects.Where(p => p.Id == projectId).ExecuteDeleteAsync(ct);

        // El workflow es de este proyecto —se clona de la plantilla en el alta—, pero la FK es
        // Restrict y por eso sobrevive al borrado: hay que llevárselo aparte. Se comprueba que no
        // lo comparta nadie antes de tocarlo, porque el modelo no lo prohíbe.
        if (!await db.Projects.AnyAsync(p => p.WorkflowId == workflowId, ct))
        {
            // Restrict en ToStageId: las transiciones se van antes que las etapas que apuntan.
            await db.WorkflowTransitions
                .Where(t => t.FromStage!.WorkflowId == workflowId || t.ToStage!.WorkflowId == workflowId)
                .ExecuteDeleteAsync(ct);

            await db.Workflows.Where(w => w.Id == workflowId).ExecuteDeleteAsync(ct);
        }

        await tx.CommitAsync(ct);

        foreach (var path in storedPaths)
        {
            // Un archivo que ya no está no es motivo para fallar: la fila que lo nombraba se fue,
            // que es lo que se pidió. Quedaría un huérfano en disco, no un error para el usuario.
            try
            {
                await files.DeleteAsync(path, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return summary;
    }

    public Task<Project?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.Projects
            .Include(p => p.CustomFields)
            .Include(p => p.Workflow!)
                .ThenInclude(w => w.Stages)
                    .ThenInclude(s => s.AllowedTransitions)
            // El responsable de cada etapa, para poder mostrar su nombre sin una consulta por
            // columna del tablero.
            .Include(p => p.Workflow!)
                .ThenInclude(w => w.Stages)
                    .ThenInclude(s => s.DefaultAssignee)
            .FirstOrDefaultAsync(p => p.Key == key.ToUpperInvariant(), ct);
}
