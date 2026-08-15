using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskAdmin.Desktop;

/// <summary>Un turno de la conversación, tal como lo devuelve la API.</summary>
public record ChatTurn(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("at")] DateTimeOffset At);

public record ConversationView(
    [property: JsonPropertyName("checkInId")] Guid CheckInId,
    [property: JsonPropertyName("isClosed")] bool IsClosed,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("turns")] IReadOnlyList<ChatTurn> Turns,
    [property: JsonPropertyName("appliedActions")] IReadOnlyList<string> AppliedActions,
    [property: JsonPropertyName("pendingActions")] IReadOnlyList<string> PendingActions);

public record TodayCheckIn(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("status")] string Status);

/// <summary>Señal de que la sesión ya no sirve, para que la ventana muestre la entrada en vez de
/// un error genérico.</summary>
public class SessionExpiredException() : Exception("La sesión venció.");

/// <summary>El cliente del check-in. Existe para que la app hable con la API directamente, en vez
/// de embeber la aplicación web.
///
/// Embeberla era el error: la web tiene su propio router y su propio guardia de sesión, así que
/// cuando la sesión inyectada no le convencía, redirigía al login **adentro** de la ventana —
/// una persona que ya había entrado en la app veía una segunda pantalla de entrada. No hay forma
/// de arreglar eso desde afuera sin pelearle a la aplicación de al lado en cada release.
///
/// Contra la API el contrato es explícito: un 401 es un 401 y lo maneja la ventana.</summary>
public class AgentApi(AppSettings settings)
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private HttpRequestMessage Build(HttpMethod metodo, string ruta, object? cuerpo = null)
    {
        var request = new HttpRequestMessage(metodo, $"{settings.ServerUrl.TrimEnd('/')}{ruta}");
        request.Headers.Authorization = new("Bearer", settings.AccessToken);
        if (cuerpo is not null) request.Content = JsonContent.Create(cuerpo);
        return request;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new SessionExpiredException();
        if (response.StatusCode == HttpStatusCode.NoContent) return default;

        if (!response.IsSuccessStatusCode)
        {
            // El cuerpo del error del servidor dice algo útil («este check-in ya está cerrado»);
            // el código solo, no.
            var detalle = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(Resumir(detalle, (int)response.StatusCode));
        }

        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
    }

    /// <summary>Saca el texto legible de un ProblemDetails y descarta el resto.</summary>
    private static string Resumir(string cuerpo, int codigo)
    {
        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            foreach (var campo in new[] { "detail", "title", "message" })
            {
                if (doc.RootElement.TryGetProperty(campo, out var valor) &&
                    valor.GetString() is { Length: > 0 } texto)
                {
                    return texto;
                }
            }
        }
        catch (JsonException)
        {
            // No era JSON; se cae al mensaje genérico.
        }

        return $"El servidor respondió {codigo}.";
    }

    /// <summary>El check-in pendiente de hoy, o null si no hay ninguno.</summary>
    public Task<TodayCheckIn?> TodayAsync(CancellationToken ct = default) =>
        SendAsync<TodayCheckIn>(Build(HttpMethod.Get, "/api/checkins/today"), ct);

    /// <summary>Abre la conversación. Si es la primera vez, el agente arranca solo, así que esta
    /// llamada tarda lo que tarde el modelo.</summary>
    public Task<ConversationView?> OpenAsync(Guid checkInId, CancellationToken ct = default) =>
        SendAsync<ConversationView>(
            Build(HttpMethod.Post, $"/api/checkins/{checkInId}/conversation"), ct);

    public Task<ConversationView?> ReplyAsync(Guid checkInId, string texto, CancellationToken ct = default) =>
        SendAsync<ConversationView>(
            Build(HttpMethod.Post, $"/api/checkins/{checkInId}/messages", new { text = texto }), ct);

    public Task<ConversationView?> NoChangesAsync(Guid checkInId, CancellationToken ct = default) =>
        SendAsync<ConversationView>(
            Build(HttpMethod.Post, $"/api/checkins/{checkInId}/no-changes"), ct);
}
