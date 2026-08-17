using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Borlaro.Tms.Agent.Providers;

namespace Borlaro.Tms.Agent;

/// <summary>Los modelos que expone el servidor local, o el motivo por el que no se pudieron
/// listar. El error viaja como dato y no como excepción: «no pude conectarme» es información
/// útil para quien está configurando, no una falla del servidor.</summary>
public record LocalModelsResult(IReadOnlyList<string> Models, string BaseUrl, string? Error);

/// <summary>Elige el proveedor en cada conversación y no una sola vez al arrancar.
///
/// Esa diferencia es lo que permite que cambiar de Claude a un modelo local —o cargar la clave
/// por primera vez— sea un guardado en la pantalla de configuración y no un redespliegue. Sin
/// clave configurada cae al modelo guionado: es preferible a fallar en el primer check-in del
/// día con un 500 que nadie va a ver hasta que alguien reclame.</summary>
/// <summary>Qué modelo está corriendo de verdad, que no siempre es el que dice la configuración.
/// Existe porque la caída al modelo guionado es silenciosa por diseño —es preferible a fallar en
/// el primer check-in del día— pero silenciosa y además invisible sería una pantalla que
/// miente.</summary>
public record AgentModelStatus(
    string Configured,
    string Effective,
    string Model,
    bool UsingFallback,
    string Explanation);

public class AgentModelFactory(
    IOptionsMonitor<AgentModelOptions> options,
    IHttpClientFactory httpFactory,
    ILoggerFactory loggers)
{
    /// <summary>Con la configuración de la plataforma. La usan los caminos que no están dentro de
    /// ninguna organización.</summary>
    public IAgentModel Create() => Create(options.CurrentValue);

    /// <summary>Con la configuración de una organización concreta: es lo que permite que una
    /// empresa apunte el agente a su propio Ollama sin que cambie el de las demás.</summary>
    public IAgentModel Create(AgentModelOptions current)
    {
        var snapshot = Options.Create(current);

        return current.Provider?.ToLowerInvariant() switch
        {
            "openai-compatible" => new OpenAiCompatibleAgentModel(
                snapshot, httpFactory, loggers.CreateLogger<OpenAiCompatibleAgentModel>()),

            "scripted" => new ScriptedAgentModel(),

            _ => HasAnthropicKey(current)
                ? new AnthropicAgentModel(snapshot, loggers.CreateLogger<AnthropicAgentModel>())
                : new ScriptedAgentModel()
        };
    }

    public AgentModelStatus Status() => Status(options.CurrentValue);

    public AgentModelStatus Status(AgentModelOptions current)
    {
        var configured = string.IsNullOrWhiteSpace(current.Provider) ? "anthropic" : current.Provider.ToLowerInvariant();

        return configured switch
        {
            "scripted" => new AgentModelStatus(
                configured, "scripted", "—", false,
                "Modelo de prueba, sin red: contesta con un guion fijo. Sirve para verificar la " +
                "maquinaria de check-ins sin gastar tokens, no para conversar."),

            "openai-compatible" => new AgentModelStatus(
                configured, "openai-compatible",
                current.Model,
                false,
                string.IsNullOrWhiteSpace(current.BaseUrl)
                    ? "Falta la URL base; se va a intentar contra http://localhost:11434/v1 (Ollama)."
                    : $"Apunta a {current.BaseUrl}."),

            _ when !HasAnthropicKey(current) => new AgentModelStatus(
                configured, "scripted", "—", true,
                "Está configurado Anthropic pero no hay clave de API, así que el agente está " +
                "respondiendo con el modelo de prueba. Cargá la clave acá abajo para que use el " +
                "modelo de verdad."),

            _ => new AgentModelStatus(
                configured, "anthropic", current.Model, false,
                $"Conversaciones reales con {current.Model}.")
        };
    }

    /// <summary>Los modelos que expone el servidor local configurado. Se pregunta en vez de
    /// hacer escribir el nombre a mano: un typo en «qwen3-coder:30b-32k» falla recién en el
    /// primer check-in del día, con un 404 del proveedor que no dice nada útil.</summary>
    public Task<LocalModelsResult> AvailableModelsAsync(CancellationToken ct = default) =>
        AvailableModelsAsync(options.CurrentValue.BaseUrl, ct);

    public async Task<LocalModelsResult> AvailableModelsAsync(
        string? configuredBaseUrl,
        CancellationToken ct = default) =>
        await AvailableModelsAsync(configuredBaseUrl, apiKey: null, ct);

    /// <summary>Los modelos que ofrece un servidor compatible con OpenAI, sea local o en la nube.
    ///
    /// `GET /models` es parte del estándar, así que la misma llamada sirve para Ollama, LM Studio,
    /// Groq o cualquier otro: lo único que cambia es que los de la nube piden autorización, y sin
    /// la clave responden 401 en vez de la lista.
    ///
    /// Se pregunta en vez de hacer escribir el nombre a mano porque un typo en el modelo no falla
    /// al guardar: falla en el primer check-in del día, con un 404 del proveedor que no explica
    /// nada.</summary>
    public async Task<LocalModelsResult> AvailableModelsAsync(
        string? configuredBaseUrl,
        string? apiKey,
        CancellationToken ct = default)
    {
        var baseUrl = configuredBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl)) baseUrl = DefaultLocalBaseUrl;

        var http = httpFactory.CreateClient("agent-model");
        http.Timeout = TimeSpan.FromSeconds(8);

        var url = baseUrl.TrimEnd('/') + "/models";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Los servidores locales no piden clave y los de la nube no responden sin ella. Se
            // manda cuando la hay y listo: el mismo botón sirve para los dos casos.
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new("Bearer", apiKey);
            }

            using var response = await http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                // 401 y 403 tienen una causa concreta y una salida concreta, y decirlo evita la
                // ronda de «respondió 401» → «¿y eso qué significa?».
                var detalle = (int)response.StatusCode switch
                {
                    401 or 403 => " Falta la clave de API o no es válida para este proveedor.",
                    404 => " Esa URL no expone /models. Revisá que termine en /v1.",
                    _ => string.Empty
                };

                return new LocalModelsResult([], baseUrl,
                    $"El servidor en {baseUrl} respondió {(int)response.StatusCode}.{detalle}");
            }

            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));

            var models = (json?["data"] as JsonArray)?
                .Select(m => m?["id"]?.GetValue<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .OrderBy(id => id)
                .ToList() ?? [];

            return new LocalModelsResult(models, baseUrl, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // El caso más habitual y el más confuso: la API corre en un contenedor y Ollama en el
            // host, así que «localhost» es el propio contenedor y no hay nadie escuchando.
            var hint = baseUrl.Contains("localhost") || baseUrl.Contains("127.0.0.1")
                ? " Si la API corre en Docker y Ollama en la máquina, usá " +
                  "http://host.docker.internal:11434/v1 en vez de localhost."
                : string.Empty;

            return new LocalModelsResult([], baseUrl, $"No se pudo conectar a {baseUrl}.{hint}");
        }
    }

    public const string DefaultLocalBaseUrl = "http://host.docker.internal:11434/v1";

    /// <summary>El SDK también toma la clave de la variable de entorno, así que no alcanza con
    /// mirar la configuración de la app para saber si hay con qué hablarle a Claude.</summary>
    private static bool HasAnthropicKey(AgentModelOptions options) =>
        !string.IsNullOrWhiteSpace(options.ApiKey) ||
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY"));
}
