using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;

namespace Borlaro.Tms.Infrastructure.Services;

/// <summary>Cómo participa alguien en un proyecto.</summary>
public enum Participation
{
    /// <summary>Ni tareas, ni revisiones, ni liderazgo. No lo ve.</summary>
    None = 0,

    /// <summary>Revisa entregables. Mira lo suyo y aprueba; no toca el tablero.</summary>
    Reviewer = 1,

    /// <summary>Tiene o tuvo trabajo asignado acá. Mueve lo suyo.</summary>
    Assignee = 2,

    /// <summary>Lidera el proyecto: mueve todo, asigna y desasigna.</summary>
    Lead = 3
}

public record ProjectPermissions(
    bool CanView,
    /// <summary>Crear trabajo en este proyecto.</summary>
    bool CanCreateWork,
    /// <summary>Mover y editar cualquier tarea, no solo la propia. Es lo que distingue a quien
    /// lidera de quien trabaja.</summary>
    bool CanEditAnyWork,
    /// <summary>Asignar y desasignar personas.</summary>
    bool CanAssign,
    /// <summary>Administrar el proyecto: líderes, formulario público.</summary>
    bool CanManageProject,
    Participation Participation,
    UserRole UserRole)
{
    public static readonly ProjectPermissions None =
        new(false, false, false, false, false, Participation.None, UserRole.ClientReviewer);

    /// <summary>Si esta tarea es mía. No alcanza para hacer cualquier cosa con ella: lo que puedo
    /// hacer lo decide el permiso que me dieron al asignármela.</summary>
    public bool IsMine(Guid? assigneeId, Guid? myId) =>
        Participation == Participation.Assignee && assigneeId is not null && assigneeId == myId;

    /// <summary>Mover de etapa: el líder siempre; el responsable, si se lo habilitaron.</summary>
    public bool CanMoveItem(Guid? assigneeId, Guid? myId, bool assigneeCanMove) =>
        CanEditAnyWork || (IsMine(assigneeId, myId) && assigneeCanMove);

    /// <summary>Editar el contenido: título, descripción, fechas, campos.</summary>
    public bool CanEditItem(Guid? assigneeId, Guid? myId, bool assigneeCanEdit) =>
        CanEditAnyWork || (IsMine(assigneeId, myId) && assigneeCanEdit);

    public bool CanDeleteItem(Guid? assigneeId, Guid? myId, bool assigneeCanDelete) =>
        CanEditAnyWork || (IsMine(assigneeId, myId) && assigneeCanDelete);
}

/// <summary>La autorización por proyecto, en un solo lugar.
///
/// La regla que ordena todo: **participar es tener trabajo asignado**. No hay una lista de
/// miembros que alguien tenga que mantener al día en paralelo a la realidad; si te asignan una
/// tarea, el proyecto aparece en tu lista, y si nunca te asignaron nada, no tenés nada que hacer
/// ahí. Lo único explícito es quién lidera, porque eso no se puede deducir del trabajo y hace
/// falta desde antes de que exista la primera tarea.
///
/// De ahí salen las cuatro reglas:
/// • Admin entra a todo, sea o no participante.
/// • El líder del proyecto mueve todo, asigna y desasigna.
/// • Quien tiene trabajo asignado ve el tablero completo pero solo mueve lo suyo.
/// • El revisor mira y aprueba; no crea ni mueve nada.</summary>
public class ProjectAccess(BorlaroTmsDbContext db)
{
    public async Task<ProjectPermissions> ForProjectAsync(
        Guid? userId,
        Guid projectId,
        CancellationToken ct = default)
    {
        if (userId is null) return ProjectPermissions.None;

        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, ct);

        if (user is null) return ProjectPermissions.None;

        var leads = await db.ProjectMembers.AsNoTracking()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.Role == ProjectRole.Lead, ct);

        // Cerradas incluidas: perder el acceso al proyecto el día que terminás lo último que
        // tenías asignado sería absurdo — te quedarías sin el historial de tu propio trabajo.
        var hasWork = !leads && await db.WorkItems.AsNoTracking()
            .AnyAsync(i => i.ProjectId == projectId && i.AssigneeId == userId, ct);

        var reviews = !leads && !hasWork && await db.ReviewRounds.AsNoTracking()
            .AnyAsync(r => r.ReviewerId == userId && r.WorkItem!.ProjectId == projectId, ct);

        var participation = leads ? Participation.Lead
            : hasWork ? Participation.Assignee
            : reviews ? Participation.Reviewer
            : Participation.None;

        return Resolve(user.Role, participation);
    }

    public async Task<ProjectPermissions> ForProjectKeyAsync(
        Guid? userId,
        string projectKey,
        CancellationToken ct = default)
    {
        var id = await db.Projects.AsNoTracking()
            .Where(p => p.Key == projectKey.ToUpperInvariant())
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        return id is null ? ProjectPermissions.None : await ForProjectAsync(userId, id.Value, ct);
    }

    public async Task<ProjectPermissions> ForWorkItemAsync(
        Guid? userId,
        Guid workItemId,
        CancellationToken ct = default)
    {
        var projectId = await db.WorkItems.AsNoTracking()
            .Where(i => i.Id == workItemId)
            .Select(i => (Guid?)i.ProjectId)
            .FirstOrDefaultAsync(ct);

        return projectId is null ? ProjectPermissions.None : await ForProjectAsync(userId, projectId.Value, ct);
    }

    public static ProjectPermissions Resolve(UserRole userRole, Participation participation)
    {
        // El admin no necesita participar: es quien tiene que poder entrar cuando algo se rompió
        // y no queda nadie más.
        if (userRole == UserRole.Admin)
        {
            return new ProjectPermissions(true, true, true, true, true, participation, userRole);
        }

        // Ver un proyecto es participar de él, y punto. Ser Manager habilita a *poder* liderar
        // —eso lo decide quién crea el proyecto—, no a mirar el tablero de un equipo del que no
        // se forma parte.
        var canView = participation != Participation.None;
        var leads = participation == Participation.Lead;

        var canCreate = leads
            || (participation == Participation.Assignee && userRole != UserRole.ClientReviewer);

        return new ProjectPermissions(
            CanView: canView,
            CanCreateWork: canCreate,
            CanEditAnyWork: leads,
            CanAssign: leads,
            CanManageProject: leads,
            Participation: participation,
            UserRole: userRole);
    }

    /// <summary>Los proyectos que esta persona ve: donde lidera, donde tiene trabajo asignado o
    /// donde revisa. `null` significa «todos», que es distinto de una lista vacía.</summary>
    public async Task<IReadOnlyList<Guid>?> VisibleProjectIdsAsync(
        Guid? userId,
        CancellationToken ct = default)
    {
        if (userId is null) return Array.Empty<Guid>();

        var role = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId && u.IsActive)
            .Select(u => (UserRole?)u.Role)
            .FirstOrDefaultAsync(ct);

        // Solo el admin ve todo, y es a propósito: es quien tiene que poder entrar cuando algo se
        // rompió. Un manager ve los proyectos que lidera y aquellos donde tiene trabajo, igual
        // que cualquiera.
        if (role is UserRole.Admin) return null;
        if (role is null) return Array.Empty<Guid>();

        var lead = db.ProjectMembers.AsNoTracking()
            .Where(m => m.UserId == userId && m.Role == ProjectRole.Lead)
            .Select(m => m.ProjectId);

        var assigned = db.WorkItems.AsNoTracking()
            .Where(i => i.AssigneeId == userId)
            .Select(i => i.ProjectId);

        var reviewing = db.ReviewRounds.AsNoTracking()
            .Where(r => r.ReviewerId == userId)
            .Select(r => r.WorkItem!.ProjectId);

        return await lead.Union(assigned).Union(reviewing).Distinct().ToListAsync(ct);
    }
}
