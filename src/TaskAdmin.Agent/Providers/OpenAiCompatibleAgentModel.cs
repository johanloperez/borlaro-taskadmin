using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TaskAdmin.Agent.Providers;

/// <summary>Adaptador para cualquier API con forma OpenAI. Con un solo adaptador quedan
/// cubiertos los modelos locales (Ollama en :11434/v1, LM Studio en :1234/v1) y las APIs
/// gratuitas o baratas que exponen ese mismo contrato.
///
/// Se habla HTTP directo a propósito: traer un SDK de terceros para un contrato de dos
/// endpoints agrega dependencias sin ganar nada.</summary>
public class OpenAiCompatibleAgentModel : IAgentModel
{
    private readonly AgentModelOptions _options;
    private readonly ILogger<OpenAiCompatibleAgentModel> _logger;
    private readonly HttpClient _http;

    public string ProviderName => $"openai-compatible ({_options.Model})";

    public OpenAiCompatibleAgentModel(
        IOptions<AgentModelOptions> options,
        IHttpClientFactory httpFactory,
        ILogger<OpenAiCompatibleAgentModel> logger)
    {
        _options = options.Value;
        _logger = logger;

        _http = httpFactory.CreateClient("agent-model");
        _http.BaseAddress = new Uri((_options.BaseUrl ?? "http://localhost:11434/v1").TrimEnd('/') + "/");

        // Un modelo local en CPU puede tardar bastante en un turno con herramientas: el timeout
        // por defecto de 100 s corta conversaciones que iban bien.
        _http.Timeout = TimeSpan.FromMinutes(5);

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
    }

    public async Task<AgentTurn> CompleteAsync(AgentRequest request, CancellationToken ct = default)
    {
        var messages = new JsonArray
        {
            new JsonObject
            {
                ["role"] = "system",
                ["content"] = request.SystemPrompt + "\n\n" + request.ContextBlock
            }
        };

        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case AgentRole.User:
                    messages.Add(new JsonObject { ["role"] = "user", ["content"] = message.Text ?? "" });
                    break;

                case AgentRole.Assistant:
                {
                    var node = new JsonObject { ["role"] = "assistant" };
                    node["content"] = message.Text ?? "";

                    if (message.ToolCalls is { Count: > 0 })
                    {
                        var serializedCalls = new JsonArray();
                        foreach (var call in message.ToolCalls)
                        {
                            serializedCalls.Add(new JsonObject
                            {
                                ["id"] = call.Id,
                                ["type"] = "function",
                                ["function"] = new JsonObject
                                {
                                    ["name"] = call.Name,
                                    ["arguments"] = call.Arguments.ToJsonString()
                                }
                            });
                        }
                        node["tool_calls"] = serializedCalls;
                    }

                    messages.Add(node);
                    break;
                }

                case AgentRole.Tool:
                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = message.ToolResult!.ToolCallId,
                        ["content"] = message.ToolResult.Content
                    });
                    break;
            }
        }

        var tools = new JsonArray();
        foreach (var tool in request.Tools)
        {
            tools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description,
                    ["parameters"] = JsonNode.Parse(tool.InputSchema.ToJsonString())
                }
            });
        }

        var body = new JsonObject
        {
            ["model"] = _options.Model,
            ["messages"] = messages,
            ["tools"] = tools,
            ["max_tokens"] = _options.MaxTokens,
            ["stream"] = false
        };

        using var response = await _http.PostAsJsonAsync("chat/completions", body, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("El proveedor respondió {Status}: {Error}", (int)response.StatusCode, error);
            throw new InvalidOperationException($"El modelo respondió {(int)response.StatusCode}: {error}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Respuesta vacía del modelo.");

        var choice = json["choices"]?[0]?["message"];
        var text = choice?["content"]?.GetValue<string>();

        var calls = new List<ToolCall>();
        if (choice?["tool_calls"] is JsonArray toolCalls)
        {
            foreach (var call in toolCalls)
            {
                var name = call?["function"]?["name"]?.GetValue<string>();
                var rawArgs = call?["function"]?["arguments"]?.GetValue<string>();
                if (name is null) continue;

                JsonObject arguments;
                try
                {
                    arguments = string.IsNullOrWhiteSpace(rawArgs)
                        ? new JsonObject()
                        : JsonNode.Parse(rawArgs)!.AsObject();
                }
                catch (JsonException)
                {
                    // Un modelo chico a veces emite argumentos que no son JSON válido. No se
                    // descarta el turno: se manda como error de herramienta para que reintente
                    // con el formato correcto.
                    _logger.LogWarning("Argumentos no parseables en {Tool}: {Args}", name, rawArgs);
                    arguments = new JsonObject { ["__parse_error"] = rawArgs ?? "" };
                }

                calls.Add(new ToolCall(
                    call?["id"]?.GetValue<string>() ?? Guid.NewGuid().ToString("N"),
                    name,
                    arguments));
            }
        }

        var usage = json["usage"];

        return new AgentTurn(
            string.IsNullOrWhiteSpace(text) ? null : text,
            calls,
            new AgentUsage(
                usage?["prompt_tokens"]?.GetValue<int>() ?? 0,
                usage?["completion_tokens"]?.GetValue<int>() ?? 0,
                0));
    }
}
