using Microsoft.EntityFrameworkCore;
using TaskAdmin.Agent;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;

namespace TaskAdmin.Infrastructure.Services;

public record PendingActionDto(
    Guid Id,
    Guid CheckInId,
    string PersonName,
    DateOnly CheckInDate,
    string ToolName,
    string? Description,
    string? WorkItemReadableId,
    string? WorkItemTitle,
    DateTimeOffset CreatedAt);

/// <summary>La cola de aprobaciones. Dos de las nueve herramientas no se aplican solas —mover una
/// fecha comprometida y crear trabajo nuevo— porque las dos afectan a alguien que no está en la
/// conversación. El agente las propone; acá una persona decide.
///
/// El efecto se aplica recién al aprobar, no antes: una propuesta pendiente no cambió nada del
/// tablero, y rechazarla no requiere deshacer nada.</summary>
public class AgentApprovalService(TaskAdminDbContext db, WorkItemService workItems)
{
    public async Task<IReadOnlyList<PendingActionDto>> PendingAsync(CancellationToken ct = default)
    {
        var rows = await db.AgentActions
            .AsNoTracking()
            .Where(a => a.Status == AgentActionStatus.PendingApproval)
            .Include(a => a.CheckIn!).ThenInclude(c => c.User)
            .Include(a => a.WorkItem!).ThenInclude(i => i.Project)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(a => new PendingActionDto(
            a.Id,
            a.CheckInId,
            a.CheckIn?.User?.Name ?? "—",
            a.CheckIn?.LocalDate ?? default,
            a.ToolName,
            a.Result,
            a.WorkItem is null ? null : $"{a.WorkItem.Project!.Key}-{a.WorkItem.Number}",
            a.WorkItem?.Title,
            a.CreatedAt)).ToList();
    }

    /// <summary>Aprueba la propuesta y recién ahí la ejecuta. El cambio queda en el historial con
    /// origen Agent y el check-in que lo originó, igual que el resto: aprobarlo no lo convierte
    /// en un cambio manual.</summary>
    public async Task<string> ApproveAsync(Guid actionId, Guid approverId, CancellationToken ct = default)
    {
        var action = await db.AgentActions
            .Include(a => a.CheckIn)
            .FirstOrDefaultAsync(a => a.Id == actionId, ct)
            ?? throw new DomainException("La acción no existe.");

        if (action.Status != AgentActionStatus.PendingApproval)
        {
            throw new DomainException("Esta acción ya se resolvió.");
        }

        var result = action.ToolName switch
        {
            AgentTools.RequestDateChange => await ApplyDateChangeAsync(action, ct),
            AgentTools.CreateFollowupTask => await ApplyFollowupAsync(action, ct),
            _ => throw new DomainException($"«{action.ToolName}» no requiere aprobación.")
        };

        action.Status = AgentActionStatus.Applied;
        action.ApprovedById = approverId;
        action.AppliedAt = DateTimeOffset.UtcNow;
        action.Result = result;

        await db.SaveChangesAsync(ct);
        return result;
    }

    public async Task RejectAsync(
        Guid actionId,
        Guid approverId,
        string? reason,
        CancellationToken ct = default)
    {
        var action = await db.AgentActions.FirstOrDefaultAsync(a => a.Id == actionId, ct)
            ?? throw new DomainException("La acción no existe.");

        if (action.Status != AgentActionStatus.PendingApproval)
        {
            throw new DomainException("Esta acción ya se resolvió.");
        }

        action.Status = AgentActionStatus.Rejected;
        action.ApprovedById = approverId;
        action.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Sin motivo indicado." : reason.Trim();

        await db.SaveChangesAsync(ct);
    }

    private async Task<string> ApplyDateChangeAsync(AgentAction action, CancellationToken ct)
    {
        var args = action.Arguments?.RootElement
            ?? throw new DomainException("La propuesta no tiene argumentos.");

        var raw = args.TryGetProperty("new_due_date", out var date) ? date.GetString() : null;

        if (!DateOnly.TryParse(raw, out var newDate))
        {
            throw new DomainException($"La fecha propuesta («{raw}») no es válida.");
        }

        var itemId = action.WorkItemId
            ?? throw new DomainException("La propuesta no apunta a ninguna tarea.");

        await workItems.UpdateAsync(
            itemId,
            new UpdateWorkItemRequest(null, null, null, null, null, false, null, newDate, null, null),
            ActorType.Agent, null, action.CheckInId, ct);

        return $"Fecha objetivo movida al {newDate:dd/MM/yyyy}.";
    }

    private async Task<string> ApplyFollowupAsync(AgentAction action, CancellationToken ct)
    {
        var args = action.Arguments?.RootElement
            ?? throw new DomainException("La propuesta no tiene argumentos.");

        var projectKey = args.TryGetProperty("project_key", out var k) ? k.GetString() : null;
        var title = args.TryGetProperty("title", out var t) ? t.GetString() : null;
        var description = args.TryGetProperty("description", out var d) ? d.GetString() : null;

        if (string.IsNullOrWhiteSpace(projectKey) || string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("La propuesta no tiene proyecto o título.");
        }

        // La tarea nueva queda asignada a la misma persona del check-in: salió de su conversación
        // y es trabajo suyo hasta que alguien decida otra cosa.
        var assigneeId = await db.CheckIns
            .Where(c => c.Id == action.CheckInId)
            .Select(c => (Guid?)c.UserId)
            .FirstOrDefaultAsync(ct);

        var item = await workItems.CreateAsync(
            projectKey,
            new CreateWorkItemRequest(title, description, null, WorkItemPriority.Normal, assigneeId, null, null, null),
            ActorType.Agent, null, action.CheckInId, ct);

        action.WorkItemId = item.Id;

        var project = await db.Projects.FirstAsync(p => p.Id == item.ProjectId, ct);
        return $"Tarea {project.Key}-{item.Number} «{item.Title}» creada.";
    }
}
