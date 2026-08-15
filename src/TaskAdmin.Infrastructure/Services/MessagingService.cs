using Microsoft.EntityFrameworkCore;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure.Notifications;

namespace TaskAdmin.Infrastructure.Services;

public record ThreadSummary(
    Guid UserId,
    string Name,
    string Email,
    UserRole Role,
    string? LastMessage,
    DateTimeOffset? LastAt,
    bool LastWasMine,
    int Unread);

public record MessageDto(
    Guid Id,
    Guid FromUserId,
    string FromName,
    bool Mine,
    string Body,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

/// <summary>Mensajes directos entre personas del equipo.
///
/// La entrega usa los mismos canales que el resto del sistema y en el mismo orden: si la app de
/// escritorio está viva, toast; si no, email con el enlace. No hay escalera de cinco peldaños
/// acá a propósito — un mensaje no es un check-in, y perseguir a alguien por un «¿podés mirar
/// esto?» convierte la herramienta en algo que se silencia.</summary>
public class MessagingService(
    TaskAdminDbContext db,
    IEnumerable<INotificationChannel> channels)
{
    /// <summary>A quién puede escribirle esta persona por iniciativa propia: a quienes tienen
    /// trabajo asignado en los proyectos que lidera.
    ///
    /// La regla sale de para qué existe el canal: escribirle a alguien es sobre el trabajo que
    /// le diste. Sin esto, cualquiera con rol de manager podía escribirle a toda la instancia,
    /// que es como se convierte una herramienta de trabajo en una de ruido.
    ///
    /// El admin queda afuera de la restricción por la misma razón que en todo lo demás: es quien
    /// tiene que poder hablarle a cualquiera cuando algo se rompió.</summary>
    public async Task<IReadOnlyList<Guid>> ReachableUserIdsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var role = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (UserRole?)u.Role)
            .FirstOrDefaultAsync(ct);

        if (role == UserRole.Admin)
        {
            return await db.Users.AsNoTracking()
                .Where(u => u.IsActive && u.Id != userId)
                .Select(u => u.Id)
                .ToListAsync(ct);
        }

        var liderados = db.ProjectMembers
            .Where(m => m.UserId == userId && m.Role == ProjectRole.Lead)
            .Select(m => m.ProjectId);

        return await db.WorkItems.AsNoTracking()
            .Where(i => liderados.Contains(i.ProjectId)
                     && i.AssigneeId != null
                     && i.AssigneeId != userId
                     && i.Assignee!.IsActive)
            .Select(i => i.AssigneeId!.Value)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ThreadSummary>> ThreadsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var mine = await db.DirectMessages
            .AsNoTracking()
            .Where(m => m.FromUserId == userId || m.ToUserId == userId)
            .ToListAsync(ct);

        var counterparts = mine
            .Select(m => m.FromUserId == userId ? m.ToUserId : m.FromUserId)
            .ToHashSet();

        // Se listan dos cosas distintas: los hilos que ya existen —que siempre se pueden
        // continuar— y las personas a las que puedo escribirle de cero, que son las que tienen
        // trabajo en los proyectos que lidero.
        var alcanzables = (await ReachableUserIdsAsync(userId, ct)).ToHashSet();

        var visible = await db.Users
            .AsNoTracking()
            .Where(u => u.Id != userId && u.IsActive
                     && (alcanzables.Contains(u.Id) || counterparts.Contains(u.Id)))
            .OrderBy(u => u.Name)
            .ToListAsync(ct);

        return visible.Select(person =>
        {
            var thread = mine
                .Where(m => m.FromUserId == person.Id || m.ToUserId == person.Id)
                .OrderByDescending(m => m.CreatedAt)
                .ToList();

            var last = thread.FirstOrDefault();

            return new ThreadSummary(
                person.Id, person.Name, person.Email, person.Role,
                last?.Body,
                last?.CreatedAt,
                last is not null && last.FromUserId == userId,
                thread.Count(m => m.ToUserId == userId && m.ReadAt == null));
        })
        .OrderByDescending(t => t.Unread > 0)
        .ThenByDescending(t => t.LastAt ?? DateTimeOffset.MinValue)
        .ToList();
    }

    /// <summary>El hilo con una persona. Abrirlo marca como leídos los que me mandó: es el
    /// momento exacto en que los vi.</summary>
    public async Task<IReadOnlyList<MessageDto>> ThreadAsync(
        Guid userId,
        Guid otherId,
        CancellationToken ct = default)
    {
        var messages = await db.DirectMessages
            .Include(m => m.FromUser)
            .Where(m => (m.FromUserId == userId && m.ToUserId == otherId)
                     || (m.FromUserId == otherId && m.ToUserId == userId))
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var unread = messages.Where(m => m.ToUserId == userId && m.ReadAt == null).ToList();
        if (unread.Count > 0)
        {
            foreach (var message in unread) message.ReadAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        return messages.Select(m => new MessageDto(
            m.Id, m.FromUserId, m.FromUser?.Name ?? "—", m.FromUserId == userId,
            m.Body, m.CreatedAt, m.ReadAt)).ToList();
    }

    public async Task<MessageDto> SendAsync(
        Guid fromId,
        Guid toId,
        string body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body)) throw new DomainException("El mensaje viene vacío.");
        if (fromId == toId) throw new DomainException("No podés escribirte a vos mismo.");

        var recipient = await db.Users.FirstOrDefaultAsync(u => u.Id == toId && u.IsActive, ct)
            ?? throw new DomainException("La persona no existe o está desactivada.");

        // Responder siempre se puede: si alguien te escribió, la conversación ya existe. Lo que
        // se restringe es empezarla.
        var yaTeEscribio = await db.DirectMessages
            .AnyAsync(m => m.FromUserId == toId && m.ToUserId == fromId, ct);

        if (!yaTeEscribio && !(await ReachableUserIdsAsync(fromId, ct)).Contains(toId))
        {
            throw new DomainException(
                $"Solo podés escribirle a quien tiene trabajo asignado en los proyectos que " +
                $"liderás, o responderle a quien te escribió. Si necesitás hablar con " +
                $"{recipient.Name}, asignale una tarea o pedile al líder de su proyecto que lo haga.");
        }

        var sender = await db.Users.FirstAsync(u => u.Id == fromId, ct);

        var message = new DirectMessage
        {
            FromUserId = fromId,
            ToUserId = toId,
            Body = body.Trim()
        };

        db.DirectMessages.Add(message);

        db.Notifications.Add(new Notification
        {
            UserId = toId,
            Channel = NotificationChannel.InApp,
            Kind = "direct_message",
            Title = $"Mensaje de {sender.Name}",
            Body = message.Body.Length > 140 ? message.Body[..140] + "…" : message.Body,
            LinkUrl = $"/mensajes/{fromId}"
        });

        await db.SaveChangesAsync(ct);

        message.DeliveredVia = await DeliverAsync(recipient, sender.Name, message, ct);
        await db.SaveChangesAsync(ct);

        return new MessageDto(message.Id, fromId, sender.Name, true, message.Body, message.CreatedAt, null);
    }

    /// <summary>Escritorio si la app está viva; si no, email. Que ninguno de los dos funcione no
    /// puede perder el mensaje: queda en la bandeja de la web igual.</summary>
    private async Task<NotificationChannel?> DeliverAsync(
        User recipient,
        string senderName,
        DirectMessage message,
        CancellationToken ct)
    {
        var payload = new NotificationPayload(
            Kind: "message",
            Title: $"Mensaje de {senderName}",
            Body: message.Body.Length > 200 ? message.Body[..200] + "…" : message.Body,
            LinkPath: $"/mensajes/{message.FromUserId}",
            CheckInId: null);

        foreach (var kind in new[] { NotificationChannel.Desktop, NotificationChannel.Email })
        {
            var channel = channels.FirstOrDefault(c => c.Kind == kind);
            if (channel is null || !await channel.CanDeliverAsync(recipient, ct)) continue;

            var result = await channel.SendAsync(recipient, payload, ct);
            if (result.Delivered) return kind;
        }

        return null;
    }

    public Task<int> UnreadCountAsync(Guid userId, CancellationToken ct = default) =>
        db.DirectMessages.CountAsync(m => m.ToUserId == userId && m.ReadAt == null, ct);
}
