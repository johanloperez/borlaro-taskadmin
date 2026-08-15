using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskAdmin.Api.Auth;
using TaskAdmin.Domain;
using TaskAdmin.Infrastructure;
using TaskAdmin.Infrastructure.Services;

namespace TaskAdmin.Api.Endpoints;

public record ActivityDto(
    Guid Id,
    DateTimeOffset At,
    ActorType ActorType,
    string ActorName,
    string ProjectKey,
    string ItemReadableId,
    string ItemTitle,
    string Field,
    string? OldValue,
    string? NewValue,
    /// <summary>Presente cuando el cambio salió de una conversación del agente. Es lo que hace
    /// auditable a la IA: cada cosa que tocó cuelga del check-in que la originó.</summary>
    Guid? CheckInId);

/// <summary>El registro de quién tocó qué. Ya se guardaba en cada cambio; lo que faltaba era
/// poder mirarlo sin entrar tarea por tarea.
///
/// Respeta la visibilidad de proyectos: no sirve de nada tapar un tablero si el feed de actividad
/// cuenta los títulos de sus tareas.</summary>
public static class ActivityEndpoints
{
    public static IEndpointRouteBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var activity = app.MapGroup("/api/activity").WithTags("Actividad").RequireAuthorization();

        activity.MapGet("/", async (
            ClaimsPrincipal principal,
            ProjectAccess access,
            TaskAdminDbContext db,
            string? projectKey,
            string? actor,
            string? action,
            string? q,
            int? days,
            int? take,
            CancellationToken ct) =>
        {
            var visible = await access.VisibleProjectIdsAsync(principal.UserId(), ct);

            var query = db.WorkItemEvents
                .AsNoTracking()
                .Include(e => e.WorkItem!).ThenInclude(i => i.Project)
                .Where(e => visible == null || visible.Contains(e.WorkItem!.ProjectId));

            if (!string.IsNullOrWhiteSpace(projectKey))
            {
                var upper = projectKey.ToUpperInvariant();
                query = query.Where(e => e.WorkItem!.Project!.Key == upper);
            }

            if (!string.IsNullOrWhiteSpace(actor) && Enum.TryParse<ActorType>(actor, true, out var actorType))
            {
                query = query.Where(e => e.ActorType == actorType);
            }

            // Las acciones se agrupan por lo que significan para quien mira, no por el nombre
            // interno del campo: «asignaciones» son dos campos distintos y a nadie le importa.
            query = action switch
            {
                "stage" => query.Where(e => e.Field == "stage"),
                "assignment" => query.Where(e => e.Field == "assignee"),
                "blocker" => query.Where(e => e.Field == "blocker"),
                "fields" => query.Where(e => e.Field.StartsWith("field:")),
                "created" => query.Where(e => e.Field == "created"),
                _ => query
            };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var needle = q.Trim().ToLower();
                query = query.Where(e =>
                    e.WorkItem!.Title.ToLower().Contains(needle) ||
                    e.WorkItem.Project!.Key.ToLower().Contains(needle) ||
                    (e.NewValue != null && e.NewValue.ToLower().Contains(needle)));
            }

            if (days is > 0)
            {
                var since = DateTimeOffset.UtcNow.AddDays(-days.Value);
                query = query.Where(e => e.CreatedAt >= since);
            }

            var rows = await query
                .OrderByDescending(e => e.CreatedAt)
                .Take(Math.Clamp(take ?? 100, 1, 500))
                .Select(e => new
                {
                    e.Id, e.CreatedAt, e.ActorType, e.ActorId, e.Field, e.OldValue, e.NewValue, e.CheckInId,
                    ProjectKey = e.WorkItem!.Project!.Key,
                    e.WorkItem.Number,
                    e.WorkItem.Title
                })
                .ToListAsync(ct);

            // Los ids de persona se resuelven a nombres de una vez: el actor y, en las
            // asignaciones, el valor viejo y el nuevo, que son ids guardados como texto.
            var ids = rows.Where(r => r.ActorId is not null).Select(r => r.ActorId!.Value).ToList();

            foreach (var row in rows.Where(r => r.Field == "assignee"))
            {
                if (Guid.TryParse(row.OldValue, out var oldId)) ids.Add(oldId);
                if (Guid.TryParse(row.NewValue, out var newId)) ids.Add(newId);
            }

            var names = await db.Users.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

            string? Person(string? raw) =>
                Guid.TryParse(raw, out var id) && names.TryGetValue(id, out var name) ? name : raw;

            return Results.Ok(rows.Select(r => new ActivityDto(
                r.Id,
                r.CreatedAt,
                r.ActorType,
                r.ActorType switch
                {
                    ActorType.Agent => "Agente de IA",
                    ActorType.System => "Sistema",
                    _ => r.ActorId is not null && names.TryGetValue(r.ActorId.Value, out var n) ? n : "—"
                },
                r.ProjectKey,
                $"{r.ProjectKey}-{r.Number}",
                r.Title,
                r.Field,
                // Solo las asignaciones guardan ids; el resto de los campos ya son legibles.
                r.Field == "assignee" ? Person(r.OldValue) : r.OldValue,
                r.Field == "assignee" ? Person(r.NewValue) : r.NewValue,
                r.CheckInId)));
        })
        .WithName("ListActivity");

        return app;
    }
}
