using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using TaskAdmin.Api.Auth;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Services;

namespace TaskAdmin.Api.Endpoints;

public record WorkItemDto(
    Guid Id,
    string ReadableId,
    string ProjectKey,
    int Number,
    string Title,
    string DescriptionMd,
    string Type,
    WorkItemPriority Priority,
    Guid StageId,
    string StageName,
    StageCategory StageCategory,
    Guid? AssigneeId,
    string? AssigneeName,
    decimal? Estimate,
    int ProgressPct,
    DateOnly? DueDate,
    double SortOrder,
    JsonNode? CustomFields,
    bool IsBlocked,
    string? BlockerReason,
    /// <summary>Qué puede hacer el responsable con esta tarea. Lo decide el líder al asignarla.</summary>
    bool AssigneeCanMove,
    bool AssigneeCanEdit,
    bool AssigneeCanDelete,
    DateTimeOffset UpdatedAt);

public record WorkItemEventDto(
    Guid Id,
    ActorType ActorType,
    string? ActorName,
    string Field,
    string? OldValue,
    string? NewValue,
    Guid? CheckInId,
    DateTimeOffset CreatedAt);

public record BoardColumn(StageDto Stage, IReadOnlyList<WorkItemDto> Items);

public record NewWorkItemBody(
    string Title,
    string? DescriptionMd,
    string? Type,
    WorkItemPriority? Priority,
    Guid? AssigneeId,
    decimal? Estimate,
    DateOnly? DueDate,
    JsonObject? CustomFields);

public record UpdateWorkItemBody(
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
    /// <summary>Permisos del responsable. Van con la asignación: solo los toca quien asigna.</summary>
    bool? AssigneeCanMove = null,
    bool? AssigneeCanEdit = null,
    bool? AssigneeCanDelete = null);

public record TransitionBody(Guid ToStageId, string? BlockerReason, double? SortOrder);

public static class WorkItemEndpoints
{
    public static IEndpointRouteBuilder MapWorkItemEndpoints(this IEndpointRouteBuilder app)
    {
        var board = app.MapGroup("/api/projects/{key}").WithTags("Tablero").RequireAuthorization();

        board.MapGet("/board", async (
            string key,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var project = await db.Projects
                .AsNoTracking()
                .Include(p => p.Workflow!).ThenInclude(w => w.Stages).ThenInclude(s => s.AllowedTransitions)
                .FirstOrDefaultAsync(p => p.Key == key.ToUpperInvariant(), ct);

            if (project is null) return Results.NotFound();

            // Ver el tablero de un proyecto ajeno es ver los títulos de todo lo que hace otro
            // equipo. Se corta acá, no en la UI.
            var permissions = await access.ForProjectAsync(principal.UserId(), project.Id, ct);
            if (!permissions.CanView) return Results.NotFound();

            var items = await Project(
                    db.WorkItems.AsNoTracking()
                        .Where(i => i.ProjectId == project.Id && i.ClosedAt == null))
                .ToListAsync(ct);

            var byStage = items.ToLookup(r => r.Item.StageId);

            var columns = project.Workflow!.Stages
                .OrderBy(s => s.Order)
                .Select(s => new BoardColumn(
                    new StageDto(s.Id, s.Name, s.Order, s.Category, s.RequiresBlockerReason, s.OpensReviewRound,
                        s.AllowedTransitions.Select(t => t.ToStageId).ToList()),
                    byStage[s.Id].OrderBy(r => r.Item.SortOrder).Select(ToDto).ToList()))
                .ToList();

            return Results.Ok(columns);
        })
        .WithName("GetBoard");

        board.MapPost("/items", async (
            string key,
            NewWorkItemBody body,
            WorkItemService service,
            ProjectAccess access,
            ClaimsPrincipal principal,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForProjectKeyAsync(me, key, ct);
            if (!permissions.CanView) return Results.NotFound();
            if (!permissions.CanCreateWork) return Forbidden(permissions);

            // Quien no lidera puede crear trabajo, pero solo para sí mismo o sin asignar:
            // asignarle una tarea a otro es distribuir trabajo ajeno, y eso es del líder.
            if (!permissions.CanAssign && body.AssigneeId is not null && body.AssigneeId != me)
            {
                return Results.Problem(
                    "Solo el líder del proyecto puede asignarle trabajo a otra persona. " +
                    "Podés crearla sin asignar o tomarla vos.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                var created = await service.CreateAsync(
                    key,
                    new CreateWorkItemRequest(
                        body.Title, body.DescriptionMd, body.Type,
                        body.Priority ?? WorkItemPriority.Normal,
                        body.AssigneeId, body.Estimate, body.DueDate, body.CustomFields),
                    ActorType.User,
                    principal.UserId(),
                    ct: ct);

                var dto = await Project(db.WorkItems.AsNoTracking().Where(i => i.Id == created.Id))
                    .FirstAsync(ct);
                return Results.Created($"/api/items/{created.Id}", ToDto(dto));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("CreateWorkItem");

        var items = app.MapGroup("/api/items").WithTags("Items").RequireAuthorization();

        items.MapGet("/mine", async (
            ClaimsPrincipal principal,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var userId = principal.UserId();

            // Los sin fecha van al final: ordenar por "tiene fecha" primero evita depender de
            // un valor centinela que Postgres tendría que interpretar.
            var rows = await Project(
                    db.WorkItems.AsNoTracking()
                        .Where(i => i.AssigneeId == userId && i.ClosedAt == null)
                        .OrderBy(i => i.DueDate == null)
                        .ThenBy(i => i.DueDate)
                        .ThenByDescending(i => i.Priority))
                .ToListAsync(ct);

            return Results.Ok(rows.Select(ToDto));
        })
        .WithName("MyWorkItems");

        items.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            if (!(await access.ForWorkItemAsync(principal.UserId(), id, ct)).CanView) return Results.NotFound();

            var row = await Project(db.WorkItems.AsNoTracking().Where(i => i.Id == id)).FirstOrDefaultAsync(ct);
            return row is null ? Results.NotFound() : Results.Ok(ToDto(row));
        })
        .WithName("GetWorkItem");

        items.MapGet("/{id:guid}/events", async (
            Guid id,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            if (!(await access.ForWorkItemAsync(principal.UserId(), id, ct)).CanView) return Results.NotFound();

            var rows = await db.WorkItemEvents
                .AsNoTracking()
                .Where(e => e.WorkItemId == id)
                .OrderByDescending(e => e.CreatedAt)
                .Select(e => new
                {
                    e.Id, e.ActorType, e.ActorId, e.Field, e.OldValue, e.NewValue, e.CheckInId, e.CreatedAt
                })
                .ToListAsync(ct);

            var actorIds = rows.Where(r => r.ActorId is not null).Select(r => r.ActorId!.Value).Distinct().ToList();
            var names = await db.Users.AsNoTracking()
                .Where(u => actorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

            return Results.Ok(rows.Select(r => new WorkItemEventDto(
                r.Id, r.ActorType,
                r.ActorId is not null && names.TryGetValue(r.ActorId.Value, out var n) ? n : null,
                r.Field, r.OldValue, r.NewValue, r.CheckInId, r.CreatedAt)));
        })
        .WithName("GetWorkItemEvents");

        items.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateWorkItemBody body,
            WorkItemService service,
            ProjectAccess access,
            ClaimsPrincipal principal,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForWorkItemAsync(me, id, ct);
            if (!permissions.CanView) return Results.NotFound();

            var flags = await db.WorkItems.AsNoTracking()
                .Where(i => i.Id == id)
                .Select(i => new { i.AssigneeId, i.AssigneeCanEdit })
                .FirstOrDefaultAsync(ct);

            if (flags is null) return Results.NotFound();
            if (!permissions.CanEditItem(flags.AssigneeId, me, flags.AssigneeCanEdit))
            {
                return ForbiddenItem(permissions, "editar");
            }

            // Cambiar de responsable es del líder, incluso sobre la tarea propia: soltarle el
            // trabajo a otro sin que nadie lo decida es exactamente lo que hay que evitar.
            if (!permissions.CanAssign && (body.AssigneeId is not null || body.ClearAssignee))
            {
                return Results.Problem(
                    "Solo el líder del proyecto puede asignar o desasignar personas.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            // El líder también debe tener el permiso CanAssignTasks activado (configurable por usuario)
            if ((body.AssigneeId is not null || body.ClearAssignee) && permissions.CanAssign)
            {
                var user = await db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == me, ct);

                if (user is not null && !user.CanAssignTasks)
                {
                    return Results.Problem(
                        "No tienes permiso configurado para asignar tareas. Contacta al administrador.",
                        statusCode: StatusCodes.Status403Forbidden);
                }
            }

            // Y menos aún ampliarse los permisos a sí mismo: sin este corte, alguien con permiso
            // de editar se daría permiso de borrar.
            var tocaPermisos = body.AssigneeCanMove is not null
                            || body.AssigneeCanEdit is not null
                            || body.AssigneeCanDelete is not null;

            if (tocaPermisos && !permissions.CanAssign)
            {
                return Results.Problem(
                    "Los permisos del responsable los define quien asigna la tarea.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                await service.UpdateAsync(
                    id,
                    new UpdateWorkItemRequest(
                        body.Title, body.DescriptionMd, body.Type, body.Priority,
                        body.AssigneeId, body.ClearAssignee, body.Estimate, body.DueDate,
                        body.ProgressPct, body.CustomFields,
                        body.AssigneeCanMove, body.AssigneeCanEdit, body.AssigneeCanDelete),
                    ActorType.User,
                    principal.UserId(),
                    ct: ct);

                var row = await Project(db.WorkItems.AsNoTracking().Where(i => i.Id == id)).FirstAsync(ct);
                return Results.Ok(ToDto(row));
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("UpdateWorkItem");

        /// Borrar se lleva el historial de la tarea, así que es del líder o del admin —no de
        /// quien la tiene asignada— y hay que repetir su identificador en `confirmar`. Para
        /// trabajo que efectivamente pasó, lo correcto es cerrarlo moviéndolo a una etapa final:
        /// eso conserva el registro.
        items.MapDelete("/{id:guid}", async (
            Guid id,
            string? confirmar,
            WorkItemService service,
            ProjectAccess access,
            ClaimsPrincipal principal,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var permissions = await access.ForWorkItemAsync(principal.UserId(), id, ct);
            if (!permissions.CanView) return Results.NotFound();

            var item = await db.WorkItems.AsNoTracking()
                .Include(i => i.Project)
                .FirstOrDefaultAsync(i => i.Id == id, ct);

            if (item is null) return Results.NotFound();

            if (!permissions.CanDeleteItem(item.AssigneeId, principal.UserId(), item.AssigneeCanDelete))
            {
                return Results.Problem(
                    "Borrar una tarea es del líder del proyecto, salvo que te lo hayan habilitado " +
                    "al asignártela. Si ya no corresponde hacerla, movela a una etapa de cierre.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var readable = $"{item.Project!.Key}-{item.Number}";

            if (!string.Equals(confirmar, readable, StringComparison.OrdinalIgnoreCase))
            {
                return Results.Problem(
                    $"Para borrar «{item.Title}» hay que repetir su identificador ({readable}) en " +
                    "«confirmar». Se va con todo su historial y no hay vuelta atrás.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            try
            {
                return Results.Ok(new { deleted = await service.DeleteAsync(id, ct) });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
            }
        })
        .WithName("DeleteWorkItem");

        items.MapPost("/{id:guid}/transition", async (
            Guid id,
            TransitionBody body,
            WorkItemService service,
            ProjectAccess access,
            ClaimsPrincipal principal,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForWorkItemAsync(me, id, ct);
            if (!permissions.CanView) return Results.NotFound();

            var flags = await db.WorkItems.AsNoTracking()
                .Where(i => i.Id == id)
                .Select(i => new { i.AssigneeId, i.AssigneeCanMove })
                .FirstOrDefaultAsync(ct);

            if (flags is null) return Results.NotFound();
            if (!permissions.CanMoveItem(flags.AssigneeId, me, flags.AssigneeCanMove))
            {
                return ForbiddenItem(permissions, "mover");
            }

            try
            {
                await service.TransitionAsync(
                    id,
                    new TransitionRequest(body.ToStageId, body.BlockerReason, body.SortOrder),
                    ActorType.User,
                    principal.UserId(),
                    ct: ct);

                var row = await Project(db.WorkItems.AsNoTracking().Where(i => i.Id == id)).FirstAsync(ct);
                return Results.Ok(ToDto(row));
            }
            catch (DomainException ex)
            {
                // 409 y no 400: la petición está bien formada, lo que falla es el estado del
                // tablero. El frontend lo usa para revertir el drag y explicar por qué.
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("TransitionWorkItem");

        // ────────────────────────────────────────────────────────────────────────────────────
        // Asignaciones específicas por etapa (override de la lista inteligente)
        // ────────────────────────────────────────────────────────────────────────────────────

        /// <summary>Asigna específicamente alguien a una tarea en una etapa, override de la lista inteligente.</summary>
        items.MapPost("/{id:guid}/stage-assignments", async (
            string key,
            Guid id,
            SetStageAssignmentBody body,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForWorkItemAsync(me, id, ct);
            if (!permissions.CanView) return Results.NotFound();
            if (!permissions.CanAssign) return Forbidden(permissions);

            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == me, ct);

            if (user is not null && !user.CanAssignTasks)
            {
                return Results.Problem(
                    "No tienes permiso configurado para asignar tareas.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var item = await db.WorkItems.FirstOrDefaultAsync(i => i.Id == id, ct);
            if (item is null) return Results.NotFound();

            var stage = await db.WorkflowStages.FirstOrDefaultAsync(s => s.Id == body.StageId, ct);
            if (stage is null) return Results.NotFound();

            if (body.AssignedUserId is { } userId)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);
                if (user is null) return Results.Problem("Usuario no existe o está inactivo.", statusCode: StatusCodes.Status400BadRequest);
            }

            var existing = await db.WorkItemStageAssignments
                .FirstOrDefaultAsync(a => a.WorkItemId == id && a.StageId == body.StageId, ct);

            if (existing is not null)
            {
                existing.AssignedUserId = body.AssignedUserId;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                db.WorkItemStageAssignments.Add(new WorkItemStageAssignment
                {
                    OrganizationId = item.OrganizationId,
                    WorkItemId = id,
                    StageId = body.StageId,
                    AssignedUserId = body.AssignedUserId
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        })
        .WithName("SetStageAssignment");

        /// <summary>Remueve la asignación específica (vuelve a la lista inteligente de la etapa).</summary>
        items.MapDelete("/{id:guid}/stage-assignments/{stageId:guid}", async (
            string key,
            Guid id,
            Guid stageId,
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForWorkItemAsync(me, id, ct);
            if (!permissions.CanView) return Results.NotFound();
            if (!permissions.CanAssign) return Forbidden(permissions);

            var user = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == me, ct);

            if (user is not null && !user.CanAssignTasks)
            {
                return Results.Problem(
                    "No tienes permiso configurado para asignar tareas.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var assignment = await db.WorkItemStageAssignments
                .FirstOrDefaultAsync(a => a.WorkItemId == id && a.StageId == stageId, ct);

            if (assignment is null) return Results.NotFound();

            db.WorkItemStageAssignments.Remove(assignment);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("RemoveStageAssignment");

        return app;
    }

    /// <summary>Asignación específica de un usuario a una etapa para una tarea.</summary>
    public record SetStageAssignmentBody(Guid StageId, Guid? AssignedUserId);

    /// <summary>El 403 dice por qué, porque «no tenés permiso» sin más manda a la persona a
    /// preguntar por chat qué le falta.</summary>
    private static IResult Forbidden(ProjectPermissions permissions) =>
        Results.Problem(
            permissions.UserRole == UserRole.ClientReviewer
                ? "Como revisor podés ver y aprobar entregables, pero no crear ni modificar trabajo."
                : "No tenés trabajo asignado en este proyecto. Pedile al líder que te asigne algo.",
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult ForbiddenItem(ProjectPermissions permissions, string accion) =>
        Results.Problem(
            permissions.Participation == Participation.Assignee
                ? $"No tenés permiso para {accion} esta tarea. Si es tuya, el líder puede " +
                  $"habilitártelo desde el panel de la tarea; si no lo es, la mueve él."
                : permissions.UserRole == UserRole.ClientReviewer
                    ? "Como revisor podés ver y aprobar entregables, pero no tocar el trabajo."
                    : "No tenés trabajo asignado en este proyecto. Pedile al líder que te asigne algo.",
            statusCode: StatusCodes.Status403Forbidden);

    private record ItemRow(
        WorkItem Item,
        string ProjectKey,
        string StageName,
        StageCategory StageCategory,
        string? AssigneeName,
        string? OpenBlockerReason);

    /// <summary>Proyecta a ItemRow. El filtrado y el ordenamiento van SIEMPRE antes de esta
    /// llamada, sobre IQueryable&lt;WorkItem&gt;: una vez proyectado, EF no puede traducir un
    /// Where ni un OrderBy sobre el record y falla en runtime, no al compilar.</summary>
    private static IQueryable<ItemRow> Project(IQueryable<WorkItem> source) =>
        source.Select(i => new ItemRow(
            i,
            i.Project!.Key,
            i.Stage!.Name,
            i.Stage.Category,
            i.Assignee != null ? i.Assignee.Name : null,
            i.Blockers.Where(b => b.ResolvedAt == null).Select(b => b.Reason).FirstOrDefault()));

    private static WorkItemDto ToDto(ItemRow row)
    {
        var i = row.Item;
        return new WorkItemDto(
            i.Id,
            $"{row.ProjectKey}-{i.Number}",
            row.ProjectKey,
            i.Number,
            i.Title,
            i.DescriptionMd,
            i.Type,
            i.Priority,
            i.StageId,
            row.StageName,
            row.StageCategory,
            i.AssigneeId,
            row.AssigneeName,
            i.Estimate,
            i.ProgressPct,
            i.DueDate,
            i.SortOrder,
            i.CustomFields is null ? null : JsonNode.Parse(i.CustomFields.RootElement.GetRawText()),
            row.OpenBlockerReason is not null,
            row.OpenBlockerReason,
            i.AssigneeCanMove,
            i.AssigneeCanEdit,
            i.AssigneeCanDelete,
            i.UpdatedAt);
    }
}

