using Microsoft.EntityFrameworkCore;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure.Storage;

namespace TaskAdmin.Infrastructure.Services;

public record NewVersionResult(Deliverable Deliverable, DeliverableVersion Version);

/// <summary>Entregables, versiones y rondas de revisión. Es el equivalente del pull request
/// para equipos de diseño y edición: la pregunta que importa no es "¿está hecho?" sino
/// "¿qué versión se aprobó y cuántas vueltas costó?".</summary>
public class DeliverableService(TaskAdminDbContext db, IFileStore files)
{
    public async Task<NewVersionResult> AddFileVersionAsync(
        Guid workItemId,
        string? deliverableName,
        Guid? deliverableId,
        Stream content,
        string fileName,
        string contentType,
        Guid uploaderId,
        string? notes,
        CancellationToken ct = default)
    {
        var deliverable = await ResolveDeliverableAsync(
            workItemId, deliverableId, deliverableName ?? fileName, DeliverableKind.File, ct);

        var stored = await files.SaveAsync(content, fileName, contentType, ct);

        var version = new DeliverableVersion
        {
            DeliverableId = deliverable.Id,
            Version = deliverable.CurrentVersion + 1,
            FilePath = stored.Path,
            FileName = stored.OriginalName,
            ContentType = stored.ContentType,
            SizeBytes = stored.SizeBytes,
            Notes = notes,
            UploadedById = uploaderId
        };

        deliverable.CurrentVersion = version.Version;
        db.DeliverableVersions.Add(version);

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = workItemId,
            ActorType = ActorType.User,
            ActorId = uploaderId,
            Field = "deliverable",
            NewValue = $"{deliverable.Name} v{version.Version}"
        });

        await db.SaveChangesAsync(ct);
        return new NewVersionResult(deliverable, version);
    }

    public async Task<NewVersionResult> AddLinkVersionAsync(
        Guid workItemId,
        string name,
        Guid? deliverableId,
        string url,
        Guid uploaderId,
        string? notes,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("El enlace debe ser una URL http o https absoluta.");
        }

        var deliverable = await ResolveDeliverableAsync(
            workItemId, deliverableId, name, DeliverableKind.Link, ct);

        var version = new DeliverableVersion
        {
            DeliverableId = deliverable.Id,
            Version = deliverable.CurrentVersion + 1,
            Url = uri.ToString(),
            Notes = notes,
            UploadedById = uploaderId
        };

        deliverable.CurrentVersion = version.Version;
        db.DeliverableVersions.Add(version);

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = workItemId,
            ActorType = ActorType.User,
            ActorId = uploaderId,
            Field = "deliverable",
            NewValue = $"{deliverable.Name} v{version.Version}"
        });

        await db.SaveChangesAsync(ct);
        return new NewVersionResult(deliverable, version);
    }

    private async Task<Deliverable> ResolveDeliverableAsync(
        Guid workItemId,
        Guid? deliverableId,
        string name,
        DeliverableKind kind,
        CancellationToken ct)
    {
        if (deliverableId is not null)
        {
            var existing = await db.Deliverables
                .FirstOrDefaultAsync(d => d.Id == deliverableId && d.WorkItemId == workItemId, ct)
                ?? throw new DomainException("El entregable no existe en este item.");

            if (existing.Kind != kind)
            {
                throw new DomainException(
                    "No se puede mezclar archivos y enlaces en el mismo entregable: creá uno nuevo.");
            }

            return existing;
        }

        if (!await db.WorkItems.AnyAsync(i => i.Id == workItemId, ct))
        {
            throw new DomainException("El item no existe.");
        }

        var created = new Deliverable
        {
            WorkItemId = workItemId,
            Name = string.IsNullOrWhiteSpace(name) ? "Entregable" : name.Trim(),
            Kind = kind,
            CurrentVersion = 0
        };

        db.Deliverables.Add(created);
        return created;
    }

    /// <summary>Abre una ronda de revisión sobre una versión concreta. No sobre "el entregable":
    /// si después se sube otra versión, la ronda anterior tiene que seguir apuntando a lo que
    /// realmente se revisó.</summary>
    public async Task<ReviewRound> OpenReviewAsync(
        Guid versionId,
        Guid reviewerId,
        Guid requestedById,
        CancellationToken ct = default)
    {
        var version = await db.DeliverableVersions
            .Include(v => v.Deliverable)
            .FirstOrDefaultAsync(v => v.Id == versionId, ct)
            ?? throw new DomainException("La versión no existe.");

        var workItemId = version.Deliverable!.WorkItemId;

        var alreadyOpen = await db.ReviewRounds
            .AnyAsync(r => r.DeliverableVersionId == versionId && r.ClosedAt == null, ct);

        if (alreadyOpen)
        {
            throw new DomainException("Esa versión ya tiene una revisión abierta.");
        }

        if (!await db.Users.AnyAsync(u => u.Id == reviewerId, ct))
        {
            throw new DomainException("El revisor no existe.");
        }

        var round = new ReviewRound
        {
            WorkItemId = workItemId,
            DeliverableVersionId = versionId,
            ReviewerId = reviewerId,
            Status = ReviewStatus.Pending
        };

        db.ReviewRounds.Add(round);
        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = workItemId,
            ActorType = ActorType.User,
            ActorId = requestedById,
            Field = "review_requested",
            NewValue = $"{version.Deliverable.Name} v{version.Version}"
        });

        await db.SaveChangesAsync(ct);
        return round;
    }

    public async Task<ReviewRound> SubmitReviewAsync(
        Guid roundId,
        ReviewStatus decision,
        string? comments,
        Guid actorId,
        CancellationToken ct = default)
    {
        if (decision is not (ReviewStatus.Approved or ReviewStatus.ChangesRequested))
        {
            throw new DomainException("La decisión debe ser aprobar o pedir cambios.");
        }

        var round = await db.ReviewRounds
            .Include(r => r.DeliverableVersion!).ThenInclude(v => v.Deliverable)
            .FirstOrDefaultAsync(r => r.Id == roundId, ct)
            ?? throw new DomainException("La ronda de revisión no existe.");

        if (round.ClosedAt is not null)
        {
            throw new DomainException("Esa ronda ya está cerrada.");
        }

        if (round.ReviewerId != actorId)
        {
            throw new DomainException("Solo el revisor asignado puede resolver esta ronda.");
        }

        if (decision == ReviewStatus.ChangesRequested && string.IsNullOrWhiteSpace(comments))
        {
            // Pedir cambios sin decir cuáles obliga a una vuelta extra solo para preguntar.
            throw new DomainException("Para pedir cambios hay que indicar qué cambiar.");
        }

        round.Status = decision;
        round.CommentsMd = comments;
        round.ClosedAt = DateTimeOffset.UtcNow;

        var deliverable = round.DeliverableVersion!.Deliverable!;

        if (decision == ReviewStatus.Approved)
        {
            deliverable.ApprovedVersionId = round.DeliverableVersionId;
        }

        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = round.WorkItemId,
            ActorType = ActorType.User,
            ActorId = actorId,
            Field = decision == ReviewStatus.Approved ? "review_approved" : "review_changes",
            NewValue = $"{deliverable.Name} v{round.DeliverableVersion.Version}"
        });

        await db.SaveChangesAsync(ct);
        return round;
    }
}
