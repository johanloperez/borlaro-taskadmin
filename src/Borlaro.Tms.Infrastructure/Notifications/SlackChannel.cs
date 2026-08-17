using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Settings;

namespace Borlaro.Tms.Infrastructure.Notifications;

/// <summary>El agente hablando por Slack.
///
/// Existe porque la app de bandeja es WPF —o sea Windows— y los equipos no son homogéneos: el
/// proyecto de video trabaja en Mac, el de desarrollo mezcla todo. Slack cubre los tres sistemas
/// y además el teléfono, sin que haya que empaquetar, firmar ni distribuir nada (§20).
///
/// Es un `INotificationChannel` más: la escalera no sabe con cuál está hablando, así que sumarlo
/// fue registrarlo. Y como todo canal personal, si no puede entregar lo dice y la escalera sigue
/// de largo hacia el correo en vez de esperar un acuse imposible.</summary>
public class SlackChannel(
    BorlaroTmsDbContext db,
    OrganizationSettings organizationSettings,
    IHttpClientFactory clients,
    ILogger<SlackChannel> logger) : INotificationChannel
{
    public NotificationChannel Kind => NotificationChannel.Slack;

    /// <summary>Puede entregar si la organización tiene el bot configurado y sabemos quién es
    /// esta persona en Slack.
    ///
    /// Lo segundo se resuelve solo: si no está vinculada, se le pregunta a Slack por su email de
    /// trabajo y se guarda el resultado. Es lo que evita el trabajo manual de copiar treinta
    /// identificadores —tarea que nadie termina, y un canal a medias vinculado es peor que
    /// ninguno—. Cuando el email no coincide, un administrador lo corrige a mano.</summary>
    public async Task<bool> CanDeliverAsync(User user, CancellationToken ct = default)
    {
        var slack = await organizationSettings.SlackAsync(ct);
        if (!slack.Enabled) return false;

        if (!string.IsNullOrWhiteSpace(user.SlackUserId)) return true;

        var encontrado = await BuscarPorEmailAsync(slack.BotToken!, user.Email, ct);
        if (encontrado is null) return false;

        // Se guarda para no volver a preguntar en cada peldaño de cada check-in. El
        // `ExecuteUpdate` va directo y no por el rastreo de cambios: acá estamos decidiendo si un
        // canal sirve, y arrastrar esa escritura hasta el `SaveChanges` de quien llame mezclaría
        // el vínculo con lo que esa operación esté guardando.
        user.SlackUserId = encontrado;

        await db.Users
            .Where(u => u.Id == user.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.SlackUserId, encontrado), ct);

        logger.LogInformation("Slack: {Email} vinculado a {SlackId}", user.Email, encontrado);
        return true;
    }

    public async Task<DeliveryResult> SendAsync(
        User user,
        NotificationPayload payload,
        CancellationToken ct = default)
    {
        var slack = await organizationSettings.SlackAsync(ct);

        if (!slack.Enabled) return DeliveryResult.Failed("Slack no está configurado");
        if (string.IsNullOrWhiteSpace(user.SlackUserId)) return DeliveryResult.Failed("sin identidad de Slack");

        // El enlace se arma con la URL pública del sistema y va en el mismo mensaje: quien quiera
        // la pantalla completa entra de un clic, y quien no, contesta ahí mismo.
        var texto = $"*{payload.Title}*\n{payload.Body}";

        var respuesta = await LlamarAsync(
            slack.BotToken!,
            "chat.postMessage",
            new { channel = user.SlackUserId, text = texto },
            ct);

        if (respuesta is null) return DeliveryResult.Failed("Slack no respondió");

        // Slack contesta 200 con `ok:false` cuando algo falla, así que mirar el código HTTP no
        // alcanza: sin esto, un token revocado se vería como una entrega exitosa y la escalera se
        // detendría creyendo que la persona ya fue avisada.
        if (!respuesta.Value.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
        {
            var error = respuesta.Value.TryGetProperty("error", out var e) ? e.GetString() : "desconocido";
            logger.LogWarning("Slack rechazó el mensaje a {Email}: {Error}", user.Email, error);
            return DeliveryResult.Failed($"Slack: {error}");
        }

        return DeliveryResult.Ok();
    }

    private async Task<string?> BuscarPorEmailAsync(string token, string email, CancellationToken ct)
    {
        var respuesta = await LlamarAsync(token, $"users.lookupByEmail?email={Uri.EscapeDataString(email)}",
            body: null, ct);

        if (respuesta is null) return null;
        if (!respuesta.Value.TryGetProperty("ok", out var ok) || !ok.GetBoolean()) return null;

        return respuesta.Value.TryGetProperty("user", out var u) && u.TryGetProperty("id", out var id)
            ? id.GetString()
            : null;
    }

    /// <summary>Una llamada a la API de Slack. Con `body` nulo va como GET, que es lo que espera
    /// `users.lookupByEmail`; con cuerpo, POST en JSON.
    ///
    /// Nunca tira: un canal caído no puede tumbar el barrido de check-ins de toda la instalación,
    /// así que un fallo se devuelve como «no se pudo» y la escalera sigue al peldaño siguiente.</summary>
    private async Task<JsonElement?> LlamarAsync(
        string token,
        string metodo,
        object? body,
        CancellationToken ct)
    {
        try
        {
            var http = clients.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(10);

            using var request = new HttpRequestMessage(
                body is null ? HttpMethod.Get : HttpMethod.Post,
                $"https://slack.com/api/{metodo}");

            request.Headers.Authorization = new("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body);

            using var response = await http.SendAsync(request, ct);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            return json;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falló la llamada a Slack {Metodo}", metodo);
            return null;
        }
    }
}
