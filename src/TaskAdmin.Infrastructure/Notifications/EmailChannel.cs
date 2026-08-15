using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure.Settings;

namespace TaskAdmin.Infrastructure.Notifications;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string FromAddress { get; set; } = "taskadmin@localhost";
    public string FromName { get; set; } = "TaskAdmin";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>Sin SmtpHost, los mensajes se escriben como archivos .eml acá. Permite
    /// desarrollar y probar la escalera sin montar un SMTP ni mandar correo de verdad a nadie.</summary>
    public string PickupDirectory { get; set; } = "outbox";
}

/// <summary>Las opciones se resuelven en cada envío y no al arrancar: el admin puede cambiar el
/// SMTP desde la interfaz y el próximo email tiene que salir con la configuración nueva, sin
/// reiniciar el servidor. Y se resuelven **por organización**: cada empresa puede mandar desde su
/// propio servidor, o heredar el de la plataforma si no configuró ninguno.</summary>
public class EmailChannel(
    OrganizationSettings organizationSettings,
    IOptionsMonitor<NotificationOptions> notificationOptions,
    ILogger<EmailChannel> logger) : INotificationChannel
{
    /// <summary>El SMTP efectivo de la organización a la que se le está mandando: el suyo si lo
    /// configuró, y si no el de la plataforma. Se resuelve por envío y no al arrancar, que es lo
    /// mismo que ya hacía el monitor, con una capa más.</summary>
    private async Task<EmailOptions> EmailAsync(CancellationToken ct) =>
        await organizationSettings.EmailAsync(ct);

    private NotificationOptions _notifications => notificationOptions.CurrentValue;

    public NotificationChannel Kind => NotificationChannel.Email;

    /// <summary>El email siempre puede intentar entregar. Que la persona lo lea es otra cosa —
    /// por eso es el penúltimo peldaño y no el primero.</summary>
    public Task<bool> CanDeliverAsync(User user, CancellationToken ct = default) =>
        Task.FromResult(!string.IsNullOrWhiteSpace(user.Email));

    public Task<DeliveryResult> SendAsync(
        User user,
        NotificationPayload payload,
        CancellationToken ct = default) =>
        SendToAsync(user.Name, user.Email, payload.Title, payload.Body, payload.LinkPath, ct);

    /// <summary>Un email a una dirección suelta, sin usuario detrás.
    ///
    /// Existe por el alta: al que se está registrando hay que escribirle **antes** de que exista
    /// su cuenta —esa es toda la gracia de verificar la dirección— y para eso no sirve una firma
    /// que pide un <see cref="User"/>.</summary>
    public async Task<DeliveryResult> SendToAsync(
        string toName,
        string toEmail,
        string subject,
        string body,
        string? linkPath,
        CancellationToken ct = default)
    {
        var email = await EmailAsync(ct);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email.FromName, email.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        var link = linkPath is null
            ? null
            : $"{_notifications.PublicBaseUrl.TrimEnd('/')}{linkPath}";

        message.Body = new BodyBuilder
        {
            TextBody = link is null
                ? body
                : $"{body}\n\n{link}",
            HtmlBody = link is null
                ? $"<p>{body}</p>"
                : $"<p>{body}</p><p><a href=\"{link}\">Abrir en TaskAdmin</a></p>"
        }.ToMessageBody();

        try
        {
            if (string.IsNullOrWhiteSpace(email.SmtpHost))
            {
                await WriteToPickupDirectoryAsync(email, message, ct);
                logger.LogInformation("Email escrito a disco para {Email} (sin SMTP configurado)", toEmail);
                return DeliveryResult.Ok();
            }

            using var client = new SmtpClient();
            await client.ConnectAsync(
                email.SmtpHost,
                email.SmtpPort,
                email.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
                ct);

            if (!string.IsNullOrWhiteSpace(email.Username))
            {
                await client.AuthenticateAsync(email.Username, email.Password, ct);
            }

            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            return DeliveryResult.Ok();
        }
        catch (Exception ex)
        {
            // Un fallo de SMTP no puede tumbar el barrido de la escalera: se registra en el
            // intento y el peldaño siguiente sigue su curso.
            logger.LogError(ex, "Falló el envío de email a {Email}", toEmail);
            return DeliveryResult.Failed(ex.Message);
        }
    }

    private static async Task WriteToPickupDirectoryAsync(
        EmailOptions email,
        MimeMessage message,
        CancellationToken ct)
    {
        Directory.CreateDirectory(email.PickupDirectory);
        var path = Path.Combine(
            email.PickupDirectory,
            $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");

        await using var stream = File.Create(path);
        await message.WriteToAsync(stream, ct);
    }
}
