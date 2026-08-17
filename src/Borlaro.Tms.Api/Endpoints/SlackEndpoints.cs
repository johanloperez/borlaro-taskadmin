using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;
using Borlaro.Tms.Infrastructure.Settings;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Api.Endpoints;

/// <summary>La vuelta de Slack: lo que la persona contesta allá entra al bucle del agente acá.
///
/// Es la mitad que hace que Slack valga la pena. Solo con la ida tendríamos notificaciones lindas
/// y la gente abriendo la web igual, que es lo que ya pasa con el globo del escritorio.
///
/// **Es la segunda superficie anónima con escritura del sistema**, después del formulario de
/// intake, así que está acotada igual de fuerte: sin firma válida no se procesa nada, y lo único
/// que un evento puede hacer es responder un check-in abierto de la persona que lo mandó.</summary>
public static class SlackEndpoints
{
    /// <summary>Cuánto puede desfasarse el reloj antes de rechazar un evento. Slack recomienda
    /// cinco minutos: sin este corte, alguien que capture un pedido válido puede repetirlo para
    /// siempre y el agente escribiría dos veces lo mismo.</summary>
    private static readonly TimeSpan ToleranciaDeReloj = TimeSpan.FromMinutes(5);

    public static IEndpointRouteBuilder MapSlackEndpoints(this IEndpointRouteBuilder app)
    {
        // Anónimo a propósito: lo llama Slack, que no tiene sesión. La autenticación es la firma.
        app.MapPost("/api/slack/events", async (
            HttpRequest request,
            BorlaroTmsDbContext db,
            OrganizationSettings settings,
            CheckInConversation conversation,
            ILoggerFactory logs,
            CancellationToken ct) =>
        {
            var logger = logs.CreateLogger("Slack");

            // El cuerpo crudo, sin deserializar: la firma se calcula sobre los bytes exactos que
            // mandó Slack. Volver a serializar el objeto cambia espacios y orden, y la firma
            // deja de coincidir por algo que no tiene nada que ver con la seguridad.
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var crudo = await reader.ReadToEndAsync(ct);
            request.Body.Position = 0;

            var firma = request.Headers["X-Slack-Signature"].ToString();
            var marca = request.Headers["X-Slack-Request-Timestamp"].ToString();

            if (string.IsNullOrEmpty(firma) || string.IsNullOrEmpty(marca))
            {
                return Results.Unauthorized();
            }

            if (!long.TryParse(marca, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch) ||
                DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(epoch) > ToleranciaDeReloj)
            {
                logger.LogWarning("Slack: evento con marca de tiempo vieja o ilegible");
                return Results.Unauthorized();
            }

            // Qué organización mandó esto no viaja en el evento de forma utilizable, y el secreto
            // con el que se verifica es de cada empresa. Así que se prueban los secretos
            // configurados y gana el que valide: si ninguno lo hace, el evento no vino de ningún
            // workspace nuestro y se descarta sin tocar nada.
            var organizacion = await OrganizacionQueFirmaAsync(db, settings, crudo, marca, firma, ct);

            if (organizacion is null)
            {
                logger.LogWarning("Slack: firma inválida, evento descartado");
                return Results.Unauthorized();
            }

            using var scope = OrganizationScope.Use(organizacion.Value);

            var evento = JsonDocument.Parse(crudo).RootElement;
            var tipo = evento.TryGetProperty("type", out var t) ? t.GetString() : null;

            // El apretón de manos con el que Slack valida la URL al configurarla. Llega una sola
            // vez, antes de que exista ningún evento real.
            if (tipo == "url_verification")
            {
                return Results.Text(evento.GetProperty("challenge").GetString() ?? "");
            }

            if (tipo != "event_callback" || !evento.TryGetProperty("event", out var interno))
            {
                return Results.Ok();
            }

            var subtipo = interno.TryGetProperty("type", out var st) ? st.GetString() : null;
            var esBot = interno.TryGetProperty("bot_id", out _);

            // Solo mensajes directos de personas. Sin el corte del bot, el mensaje que el propio
            // agente acaba de mandar vuelve como evento y se contestaría a sí mismo en un bucle.
            if (subtipo != "message" || esBot) return Results.Ok();

            var slackUserId = interno.TryGetProperty("user", out var u) ? u.GetString() : null;
            var texto = interno.TryGetProperty("text", out var x) ? x.GetString() : null;

            if (string.IsNullOrWhiteSpace(slackUserId) || string.IsNullOrWhiteSpace(texto))
            {
                return Results.Ok();
            }

            var persona = await db.Users.FirstOrDefaultAsync(p => p.SlackUserId == slackUserId, ct);
            if (persona is null) return Results.Ok();

            // Su check-in abierto de hoy. Sin uno, no hay conversación que continuar: se ignora en
            // silencio en vez de inventar una: escribirle porque escribió convertiría el canal en
            // un chat general, y el agente solo existe para el check-in.
            var checkIn = await db.CheckIns
                .Where(c => c.UserId == persona.Id
                         && c.Status != CheckInStatus.Completed
                         && c.Status != CheckInStatus.Missed)
                .OrderByDescending(c => c.LocalDate)
                .FirstOrDefaultAsync(ct);

            if (checkIn is null) return Results.Ok();

            try
            {
                await conversation.ReplyAsync(checkIn.Id, persona.Id, texto!, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Slack reintenta lo que no responde 200, y reintentar una conversación que ya se
                // procesó a medias la duplicaría. Se registra y se acusa recibo.
                logger.LogError(ex, "Slack: falló procesar la respuesta de {Email}", persona.Email);
            }

            return Results.Ok();
        })
        .AllowAnonymous()
        .WithName("SlackEvents")
        .WithTags("Slack");

        return app;
    }

    /// <summary>Cuál de las organizaciones configuradas firmó este pedido, si alguna.
    ///
    /// Se recorren las que tienen Slack configurado y se prueba su secreto. Son pocas y la
    /// comparación es un HMAC, así que es barato; y es lo que permite que cada empresa tenga su
    /// propio workspace sin pedirle a Slack que nos diga a cuál pertenece.</summary>
    private static async Task<Guid?> OrganizacionQueFirmaAsync(
        BorlaroTmsDbContext db,
        OrganizationSettings settings,
        string cuerpo,
        string marca,
        string firma,
        CancellationToken ct)
    {
        List<Guid> candidatas;

        using (OrganizationScope.UseSystem())
        {
            candidatas = await db.OrganizationSettings
                .AsNoTracking()
                .Where(s => s.Key == "Slack:SigningSecret")
                .Select(s => s.OrganizationId)
                .Distinct()
                .ToListAsync(ct);
        }

        foreach (var id in candidatas)
        {
            using var scope = OrganizationScope.Use(id);

            var slack = await settings.SlackAsync(ct);
            if (string.IsNullOrWhiteSpace(slack.SigningSecret)) continue;

            if (FirmaValida(slack.SigningSecret, cuerpo, marca, firma)) return id;
        }

        return null;
    }

    /// <summary>El esquema de firma de Slack: HMAC-SHA256 de «v0:marca:cuerpo» con el signing
    /// secret, comparado en tiempo constante.
    ///
    /// La comparación en tiempo constante no es ceremonia: comparar cadenas con `==` corta en el
    /// primer carácter distinto, y ese tiempo distinto es suficiente para adivinar la firma byte
    /// a byte.</summary>
    private static bool FirmaValida(string secreto, string cuerpo, string marca, string firma)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto));

        // En minúsculas: Slack manda la firma así, y `ToHexString` devuelve mayúsculas.
        var calculada = "v0=" + Convert.ToHexString(
            hmac.ComputeHash(Encoding.UTF8.GetBytes($"v0:{marca}:{cuerpo}"))).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(calculada),
            Encoding.UTF8.GetBytes(firma));
    }
}
