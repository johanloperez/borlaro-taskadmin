using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskAdmin.Api.Auth;
using TaskAdmin.Domain;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Services;

namespace TaskAdmin.Api.Endpoints;

public record TemplateSummary(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string ItemNounSingular,
    string ItemNounPlural,
    IReadOnlyList<string> WorkItemTypes,
    IReadOnlyList<TemplateStageSummary> Stages,
    IReadOnlyList<TemplateFieldSummary> Fields);

public record TemplateStageSummary(string Name, int Order, StageCategory Category, bool RequiresBlockerReason);

public record TemplateFieldSummary(string Key, string Label, CustomFieldType Type, IReadOnlyList<string> Options, bool Required);

/// <summary>La plantilla completa, tal como la edita el admin. A diferencia del resumen que usa
/// el alta de proyectos, trae las transiciones, las pistas para el agente y el contexto de
/// disciplina — todo lo que hace falta para reconstruirla en el editor sin perder nada.</summary>
public record TemplateDetail(
    Guid Id,
    string Key,
    string Name,
    string Description,
    bool IsBuiltIn,
    string ItemNounSingular,
    string ItemNounPlural,
    IReadOnlyList<string> WorkItemTypes,
    string AgentContext,
    int ProjectsUsing,
    IReadOnlyList<TemplateStageDetail> Stages,
    IReadOnlyList<TemplateFieldDetail> Fields);

public record TemplateStageDetail(
    string Name,
    int Order,
    StageCategory Category,
    bool RequiresBlockerReason,
    bool OpensReviewRound,
    IReadOnlyList<string> AllowedNext);

public record TemplateFieldDetail(
    string Key,
    string Label,
    CustomFieldType Type,
    IReadOnlyList<string> Options,
    bool Required,
    string? AgentHint);

public record NewProjectRequest(
    string Key,
    string Name,
    string? Description,
    Guid TemplateId,
    string? RepoUrl,
    IReadOnlyList<Guid>? LeadIds);

/// <summary>Quién participa del proyecto. `Participation` no se edita: se deduce de tener trabajo
/// asignado o revisiones pendientes. Lo único que se designa es quién lidera.</summary>
public record ProjectMemberDto(
    Guid UserId,
    string Name,
    string Email,
    UserRole Role,
    Participation Participation,
    int OpenItems,
    int TotalItems);

public record SetLeadsBody(IReadOnlyList<Guid> LeadIds);

/// <summary>Lo que la persona que pregunta puede hacer sobre este proyecto. La UI lo usa para
/// mostrar solo lo que tiene sentido: un botón que siempre termina en 403 es peor que no
/// tenerlo.</summary>
public record ProjectPermissionsDto(
    bool CanCreateWork,
    bool CanEditAnyWork,
    bool CanAssign,
    bool CanManageProject,
    Participation MyParticipation);

public record ProjectSummary(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string ItemNounSingular,
    string ItemNounPlural,
    IReadOnlyList<string> WorkItemTypes,
    string? RepoUrl,
    int OpenItems,
    int TotalItems,
    string? TemplateName,
    bool IsArchived,
    DateTimeOffset? ArchivedAt,
    string? ArchivedByName,
    /// <summary>Si lidero este proyecto. La lista lo usa para ofrecer archivar solo a quien puede.</summary>
    bool ILead,
    DateTimeOffset? LastActivityAt);

public record StageDto(
    Guid Id,
    string Name,
    int Order,
    StageCategory Category,
    bool RequiresBlockerReason,
    bool OpensReviewRound,
    IReadOnlyList<Guid> AllowedNextStageIds,
    /// <summary>Quién recibe el trabajo que cae en esta etapa. Nulo = la tarea no cambia de manos.</summary>
    Guid? DefaultAssigneeId = null,
    string? DefaultAssigneeName = null);

/// <summary>Cambiar el responsable de una etapa. Nulo lo saca: la tarea entonces se queda con
/// quien la tenía, que es lo correcto para etapas de tránsito como «Bloqueado».</summary>
public record StageOwnerBody(Guid? AssigneeId);

public record CustomFieldDto(
    Guid Id,
    string Key,
    string Label,
    CustomFieldType Type,
    IReadOnlyList<string> Options,
    bool Required,
    int Order,
    string? AgentHint);

public record ProjectDetail(
    Guid Id,
    string Key,
    string Name,
    string Description,
    string ItemNounSingular,
    string ItemNounPlural,
    IReadOnlyList<string> WorkItemTypes,
    string? RepoUrl,
    IReadOnlyList<StageDto> Stages,
    IReadOnlyList<CustomFieldDto> CustomFields,
    ProjectPermissionsDto Permissions);

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var templates = app.MapGroup("/api/templates").WithTags("Plantillas").RequireAuthorization();

        templates.MapGet("/", async (TaskAdminDbContext db, CancellationToken ct) =>
        {
            var all = await db.ProjectTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

            return Results.Ok(all.Select(t => new TemplateSummary(
                t.Id, t.Key, t.Name, t.Description, t.ItemNounSingular, t.ItemNounPlural,
                t.WorkItemTypes,
                t.Stages.OrderBy(s => s.Order)
                    .Select(s => new TemplateStageSummary(s.Name, s.Order, s.Category, s.RequiresBlockerReason))
                    .ToList(),
                t.Fields.OrderBy(f => f.Order)
                    .Select(f => new TemplateFieldSummary(f.Key, f.Label, f.Type, f.Options, f.Required))
                    .ToList())));
        })
        .WithName("ListTemplates");

        var projects = app.MapGroup("/api/projects").WithTags("Proyectos").RequireAuthorization();

        projects.MapGet("/", async (
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            /// <summary>activos (por defecto) | archivados | todos</summary>
            string? estado,
            string? q,
            Guid? templateId,
            bool? soloMios,
            CancellationToken ct) =>
        {
            var me = principal.UserId();

            // Un colaborador ve los proyectos donde participa, no el catálogo entero de la
            // empresa. `null` es «sin filtro», que es distinto de «no ve ninguno».
            var visible = await access.VisibleProjectIdsAsync(me, ct);

            var query = db.Projects
                .AsNoTracking()
                .Where(p => visible == null || visible.Contains(p.Id));

            // Por defecto, los activos: un proyecto archivado es ruido salvo que lo busques.
            query = estado switch
            {
                "archivados" => query.Where(p => p.IsArchived),
                "todos" => query,
                _ => query.Where(p => !p.IsArchived)
            };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim().ToLower();
                query = query.Where(p =>
                    p.Key.ToLower().Contains(needle) ||
                    p.Name.ToLower().Contains(needle) ||
                    p.Description.ToLower().Contains(needle));
            }

            if (templateId is not null) query = query.Where(p => p.TemplateId == templateId);

            if (soloMios == true)
            {
                query = query.Where(p =>
                    p.Members.Any(m => m.UserId == me && m.Role == ProjectRole.Lead) ||
                    p.Items.Any(i => i.AssigneeId == me));
            }

            var rows = await query
                .OrderBy(p => p.IsArchived)
                .ThenBy(p => p.Key)
                .Select(p => new ProjectSummary(
                    p.Id, p.Key, p.Name, p.Description,
                    p.ItemNounSingular, p.ItemNounPlural, p.WorkItemTypes, p.RepoUrl,
                    p.Items.Count(i => i.ClosedAt == null),
                    p.Items.Count,
                    p.Template!.Name,
                    p.IsArchived,
                    p.ArchivedAt,
                    p.ArchivedBy!.Name,
                    p.Members.Any(m => m.UserId == me && m.Role == ProjectRole.Lead),
                    p.Items.Max(i => (DateTimeOffset?)i.UpdatedAt)))
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("ListProjects");

        /// Archivar es el «se terminó» de un proyecto: sale de la lista por defecto y deja de
        /// admitir trabajo nuevo, pero no se borra nada — el historial es el registro de lo que
        /// hizo el equipo, y queda dicho quién lo dio por terminado y cuándo.
        projects.MapPost("/{key}/archive", async (
            string key,
            bool? undo,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var permissions = await access.ForProjectKeyAsync(principal.UserId(), key, ct);
            if (!permissions.CanView) return Results.NotFound();
            if (!permissions.CanManageProject)
            {
                return Results.Problem(
                    "Solo el líder del proyecto o un administrador pueden archivarlo.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var project = await db.Projects.FirstOrDefaultAsync(p => p.Key == key.ToUpperInvariant(), ct);
            if (project is null) return Results.NotFound();

            if (undo == true)
            {
                project.IsArchived = false;
                project.ArchivedAt = null;
                project.ArchivedById = null;
            }
            else
            {
                project.IsArchived = true;
                project.ArchivedAt = DateTimeOffset.UtcNow;
                project.ArchivedById = principal.UserId();
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { project.Key, project.IsArchived, project.ArchivedAt });
        })
        .WithName("ArchiveProject");

        /// El inventario de lo que se va a perder, para que la confirmación diga «12 tareas, 3
        /// entregables» en vez de «esta acción no se puede deshacer».
        projects.MapGet("/{key}/eliminacion", async (
            string key,
            ProjectService service,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var id = await db.Projects.AsNoTracking()
                .Where(p => p.Key == key.ToUpperInvariant())
                .Select(p => p.Id)
                .FirstOrDefaultAsync(ct);

            return id == Guid.Empty
                ? Results.NotFound()
                : Results.Ok(await service.DeletionPreviewAsync(id, ct));
        })
        .WithName("PreviewProjectDeletion")
        .RequireAuthorization(Policies.IsAdmin);

        /// Borrar es la otra mitad de archivar: se lleva el tablero entero y no hay vuelta atrás.
        /// Dos diferencias con archivar, las dos a propósito: solo un admin puede —el líder archiva,
        /// borrar no—, y hay que repetir la clave del proyecto en `confirmar`. Un DELETE que se
        /// dispara sin más deja el destrozo a un clic mal dado o a una pestaña vieja.
        projects.MapDelete("/{key}", async (
            string key,
            string? confirmar,
            ProjectService service,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var project = await db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Key == key.ToUpperInvariant(), ct);
            if (project is null) return Results.NotFound();

            if (!string.Equals(confirmar, project.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    $"Para borrar «{project.Name}» hay que repetir su clave ({project.Key}) en " +
                    "«confirmar». Si lo que querés es sacarlo de la lista sin perder el historial, " +
                    "archivalo.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                return Results.Ok(await service.DeleteAsync(project.Id, ct));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("DeleteProject")
        .RequireAuthorization(Policies.IsAdmin);

        projects.MapPost("/", async (
            NewProjectRequest request,
            ClaimsPrincipal principal,
            ProjectService service,
            CancellationToken ct) =>
        {
            try
            {
                var project = await service.CreateFromTemplateAsync(
                    new CreateProjectRequest(
                        request.Key, request.Name, request.Description, request.TemplateId,
                        request.RepoUrl,
                        request.LeadIds,
                        principal.UserId()),
                    ct);

                return Results.Created($"/api/projects/{project.Key}", new { project.Id, project.Key, project.Name });
            }
            catch (Exception ex) when (ex is InvalidOperationException or DomainException)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CreateProject")
        .RequireAuthorization(Policies.CanManage);

        projects.MapGet("/{key}", async (
            string key,
            ClaimsPrincipal principal,
            ProjectService service,
            ProjectAccess access,
            CancellationToken ct) =>
        {
            var project = await service.GetByKeyAsync(key, ct);
            if (project is null) return Results.NotFound();

            var permissions = await access.ForProjectAsync(principal.UserId(), project.Id, ct);

            // 404 y no 403: a quien no es miembro no le confirmamos siquiera que el proyecto
            // existe. Un 403 sobre /api/projects/NOMINA ya cuenta algo.
            if (!permissions.CanView) return Results.NotFound();

            var stages = project.Workflow!.Stages
                .OrderBy(s => s.Order)
                .Select(s => new StageDto(
                    s.Id, s.Name, s.Order, s.Category, s.RequiresBlockerReason, s.OpensReviewRound,
                    s.AllowedTransitions.Select(t => t.ToStageId).ToList(),
                    s.DefaultAssigneeId, s.DefaultAssignee?.Name))
                .ToList();

            var fields = project.CustomFields
                .OrderBy(f => f.Order)
                .Select(f => new CustomFieldDto(f.Id, f.Key, f.Label, f.Type, f.Options, f.Required, f.Order, f.AgentHint))
                .ToList();

            return Results.Ok(new ProjectDetail(
                project.Id, project.Key, project.Name, project.Description,
                project.ItemNounSingular, project.ItemNounPlural, project.WorkItemTypes,
                project.RepoUrl, stages, fields,
                new ProjectPermissionsDto(
                    permissions.CanCreateWork,
                    permissions.CanEditAnyWork,
                    permissions.CanAssign,
                    permissions.CanManageProject,
                    permissions.Participation)));
        })
        .WithName("GetProject");

        /// Quién se hace cargo del trabajo que llega a una etapa. Lo define quien lidera el
        /// proyecto, una vez, y después cada tarea que caiga ahí se asigna sola y avisa.
        projects.MapPut("/{key}/stages/{stageId:guid}/responsable", async (
            string key,
            Guid stageId,
            StageOwnerBody body,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var permissions = await access.ForProjectKeyAsync(principal.UserId(), key, ct);
            if (!permissions.CanView) return Results.NotFound();

            // Repartir trabajo es del líder: la misma regla que ya rige asignar una tarea a otro.
            if (!permissions.CanAssign)
            {
                return Results.Problem(
                    "Solo el líder del proyecto puede definir quién se hace cargo de una etapa.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // La etapa se busca por el workflow del proyecto y no solo por su id: sin eso, alguien
            // con acceso a un proyecto podría cambiarle el responsable a una etapa de otro.
            var workflowId = await db.Projects.AsNoTracking()
                .Where(p => p.Key == key.ToUpperInvariant())
                .Select(p => p.WorkflowId)
                .FirstOrDefaultAsync(ct);

            var stage = await db.WorkflowStages
                .FirstOrDefaultAsync(s => s.Id == stageId && s.WorkflowId == workflowId, ct);

            if (stage is null) return Results.NotFound();

            if (body.AssigneeId is { } id)
            {
                // Se comprueba que exista y esté activa: poner de responsable a alguien dado de
                // baja haría que el trabajo se apile en silencio sobre una cuenta muerta.
                var activo = await db.Users.AnyAsync(u => u.Id == id && u.IsActive, ct);
                if (!activo)
                {
                    return Results.Problem(
                        "Esa persona no existe o está desactivada.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }

            stage.DefaultAssigneeId = body.AssigneeId;
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("SetStageOwner");

        projects.MapGet("/{key}/members", async (
            string key,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var permissions = await access.ForProjectKeyAsync(principal.UserId(), key, ct);
            if (!permissions.CanView) return Results.NotFound();

            var projectId = await db.Projects.AsNoTracking()
                .Where(p => p.Key == key.ToUpperInvariant())
                .Select(p => p.Id)
                .FirstAsync(ct);

            // El equipo no sale de una lista que alguien mantiene: sale de quién tiene trabajo
            // asignado, quién revisa y quién lidera. Así no puede desincronizarse de la realidad.
            var leadIds = await db.ProjectMembers.AsNoTracking()
                .Where(m => m.ProjectId == projectId && m.Role == ProjectRole.Lead)
                .Select(m => m.UserId)
                .ToListAsync(ct);

            var assignedIds = await db.WorkItems.AsNoTracking()
                .Where(i => i.ProjectId == projectId && i.AssigneeId != null)
                .Select(i => i.AssigneeId!.Value)
                .Distinct()
                .ToListAsync(ct);

            var reviewerIds = await db.ReviewRounds.AsNoTracking()
                .Where(r => r.WorkItem!.ProjectId == projectId)
                .Select(r => r.ReviewerId)
                .Distinct()
                .ToListAsync(ct);

            var everyone = leadIds.Concat(assignedIds).Concat(reviewerIds).Distinct().ToList();

            var people = await db.Users.AsNoTracking()
                .Where(u => everyone.Contains(u.Id))
                .Select(u => new
                {
                    u.Id, u.Name, u.Email, u.Role,
                    Open = db.WorkItems.Count(i => i.ProjectId == projectId && i.AssigneeId == u.Id && i.ClosedAt == null),
                    Total = db.WorkItems.Count(i => i.ProjectId == projectId && i.AssigneeId == u.Id)
                })
                .ToListAsync(ct);

            var rows = people
                .Select(p => new ProjectMemberDto(
                    p.Id, p.Name, p.Email, p.Role,
                    leadIds.Contains(p.Id) ? Participation.Lead
                        : assignedIds.Contains(p.Id) ? Participation.Assignee
                        : Participation.Reviewer,
                    p.Open, p.Total))
                // Líderes primero, después quien más trabajo abierto tiene: es lo que se busca
                // al abrir el panel.
                .OrderByDescending(p => p.Participation == Participation.Lead)
                .ThenByDescending(p => p.OpenItems)
                .ThenBy(p => p.Name)
                .ToList();

            return Results.Ok(rows);
        })
        .WithName("ListProjectMembers");

        // Se manda la lista completa de líderes, no altas y bajas sueltas: dos personas editando
        // a la vez con PATCH incrementales terminan discutiendo por quién aplicó último.
        projects.MapPut("/{key}/leads", async (
            string key,
            SetLeadsBody body,
            ClaimsPrincipal principal,
            ProjectService service,
            ProjectAccess access,
            CancellationToken ct) =>
        {
            // Designar líderes es del líder del proyecto o del admin. Antes alcanzaba con el rol
            // global de Manager, así que cualquier manager podía reacomodar equipos ajenos.
            var permissions = await access.ForProjectKeyAsync(principal.UserId(), key, ct);
            if (!permissions.CanView) return Results.NotFound();
            if (!permissions.CanManageProject)
            {
                return Results.Problem(
                    "Solo el líder del proyecto o un administrador pueden designar líderes.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                await service.SetLeadsAsync(key, body.LeadIds ?? [], ct);
                return Results.NoContent();
            }
            catch (Exception ex) when (ex is InvalidOperationException or DomainException)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("SetProjectLeads");

        // ────────────────────────────────────────────────────────────────────────────────────
        // Responsables por etapa (lista inteligente)
        // ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Lista de responsables que pueden recibir trabajo en una etapa.</summary>
        projects.MapGet("/{key}/stages/{stageId:guid}/responsables", async (
            string key,
            Guid stageId,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var permissions = await access.ForProjectKeyAsync(principal.UserId(), key, ct);
            if (!permissions.CanView) return Results.NotFound();

            var workflowId = await db.Projects.AsNoTracking()
                .Where(p => p.Key == key.ToUpperInvariant())
                .Select(p => p.WorkflowId)
                .FirstOrDefaultAsync(ct);

            var stage = await db.WorkflowStages
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stageId && s.WorkflowId == workflowId, ct);

            if (stage is null) return Results.NotFound();

            var responsibles = await db.StageResponsibles
                .AsNoTracking()
                .Where(s => s.StageId == stageId)
                .Include(s => s.User)
                .OrderBy(s => s.Order)
                .Select(s => new { s.UserId, s.User!.Name, s.Order })
                .ToListAsync(ct);

            return Results.Ok(responsibles);
        })
        .WithName("GetStageResponsibles");

        /// <summary>Agrega alguien a la lista de responsables de una etapa.</summary>
        projects.MapPost("/{key}/stages/{stageId:guid}/responsables", async (
            string key,
            Guid stageId,
            StageResponsibleBody body,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForProjectKeyAsync(me, key, ct);
            if (!permissions.CanAssign) return Results.Problem("No tienes permiso.", statusCode: StatusCodes.Status403Forbidden);

            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == me, ct);

            if (user is not null && !user.CanAssignTasks)
            {
                return Results.Problem(
                    "No tienes permiso configurado para asignar tareas.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var workflowId = await db.Projects.AsNoTracking()
                .Where(p => p.Key == key.ToUpperInvariant())
                .Select(p => p.WorkflowId)
                .FirstOrDefaultAsync(ct);

            var stage = await db.WorkflowStages
                .FirstOrDefaultAsync(s => s.Id == stageId && s.WorkflowId == workflowId, ct);

            if (stage is null) return Results.NotFound();

            var userExists = await db.Users.AnyAsync(u => u.Id == body.UserId && u.IsActive, ct);
            if (!userExists) return Results.Problem("El usuario no existe o está inactivo.", statusCode: StatusCodes.Status400BadRequest);

            var alreadyExists = await db.StageResponsibles.AnyAsync(s => s.StageId == stageId && s.UserId == body.UserId, ct);
            if (alreadyExists) return Results.Problem("Este usuario ya es responsable de la etapa.", statusCode: StatusCodes.Status400BadRequest);

            var nextOrder = await db.StageResponsibles.Where(s => s.StageId == stageId).CountAsync(ct);

            db.StageResponsibles.Add(new StageResponsible
            {
                OrganizationId = stage.OrganizationId,
                StageId = stageId,
                UserId = body.UserId,
                Order = nextOrder
            });

            await db.SaveChangesAsync(ct);
            return Results.Created();
        })
        .WithName("AddStageResponsible");

        /// <summary>Remueve a alguien de la lista de responsables de una etapa.</summary>
        projects.MapDelete("/{key}/stages/{stageId:guid}/responsables/{userId:guid}", async (
            string key,
            Guid stageId,
            Guid userId,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForProjectKeyAsync(me, key, ct);
            if (!permissions.CanAssign) return Results.Problem("No tienes permiso.", statusCode: StatusCodes.Status403Forbidden);

            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == me, ct);

            if (user is not null && !user.CanAssignTasks)
            {
                return Results.Problem(
                    "No tienes permiso configurado para asignar tareas.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var workflowId = await db.Projects.AsNoTracking()
                .Where(p => p.Key == key.ToUpperInvariant())
                .Select(p => p.WorkflowId)
                .FirstOrDefaultAsync(ct);

            var stage = await db.WorkflowStages
                .FirstOrDefaultAsync(s => s.Id == stageId && s.WorkflowId == workflowId, ct);

            if (stage is null) return Results.NotFound();

            var responsible = await db.StageResponsibles
                .FirstOrDefaultAsync(s => s.StageId == stageId && s.UserId == userId, ct);

            if (responsible is null) return Results.NotFound();

            db.StageResponsibles.Remove(responsible);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("RemoveStageResponsible");

        return app;
    }
}

public record StageResponsibleBody(Guid UserId);
