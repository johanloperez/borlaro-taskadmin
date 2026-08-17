using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Borlaro.Tms.Agent.Providers;

/// <summary>Adaptador sobre el SDK oficial de Anthropic. Es el proveedor recomendado porque el
/// producto depende de que el tool calling sea fiable: una llamada mal formada no actualiza el
/// tablero, y el agente deja de servir.</summary>
public class AnthropicAgentModel : IAgentModel
{
    private readonly AgentModelOptions _options;
    private readonly ILogger<AnthropicAgentModel> _logger;
    private readonly AnthropicClient _client;

    public string ProviderName => "anthropic";

    public AnthropicAgentModel(IOptions<AgentModelOptions> options, ILogger<AnthropicAgentModel> logger)
    {
        _options = options.Value;
        _logger = logger;

        _client = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = _options.ApiKey };
    }

    public async Task<AgentTurn> CompleteAsync(AgentRequest request, CancellationToken ct = default)
    {
        var messages = new List<MessageParam>();

        foreach (var message in request.Messages)
        {
            switch (message.Role)
            {
                case AgentRole.User:
                    messages.Add(new MessageParam
                    {
                        Role = Role.User,
                        Content = message.Text ?? string.Empty
                    });
                    break;

                case AgentRole.Assistant:
                {
                    var blocks = new List<ContentBlockParam>();
                    if (!string.IsNullOrWhiteSpace(message.Text))
                    {
                        blocks.Add(new TextBlockParam { Text = message.Text });
                    }
                    foreach (var call in message.ToolCalls ?? [])
                    {
                        blocks.Add(new ToolUseBlockParam
                        {
                            ID = call.Id,
                            Name = call.Name,
                            Input = ToInputDictionary(call.Arguments)
                        });
                    }
                    messages.Add(new MessageParam { Role = Role.Assistant, Content = blocks });
                    break;
                }

                case AgentRole.Tool:
                {
                    var result = message.ToolResult!;
                    messages.Add(new MessageParam
                    {
                        Role = Role.User,
                        Content = new List<ContentBlockParam>
                        {
                            new ToolResultBlockParam
                            {
                                ToolUseID = result.ToolCallId,
                                Content = result.Content,
                                IsError = result.IsError
                            }
                        }
                    });
                    break;
                }
            }
        }

        // El contexto va como primer mensaje del usuario, no en el system: el system es el prefijo
        // cacheado e idéntico en todos los check-ins, y meterle acá el tablero de una persona
        // invalidaría la caché en cada conversación.
        if (!string.IsNullOrWhiteSpace(request.ContextBlock))
        {
            messages.Insert(0, new MessageParam
            {
                Role = Role.User,
                Content = "Contexto de hoy:\n\n" + request.ContextBlock
            });
            messages.Insert(1, new MessageParam
            {
                Role = Role.Assistant,
                Content = "Listo, tengo el contexto."
            });
        }

        var tools = request.Tools
            .Select(t =>
            {
                var properties = new Dictionary<string, JsonElement>();
                if (t.InputSchema["properties"] is JsonObject props)
                {
                    foreach (var pair in props)
                    {
                        properties[pair.Key] = JsonSerializer.Deserialize<JsonElement>(pair.Value!.ToJsonString());
                    }
                }

                var required = (t.InputSchema["required"] as JsonArray)?
                    .Select(n => n!.GetValue<string>())
                    .ToList() ?? [];

                return new ToolUnion(new Tool
                {
                    Name = t.Name,
                    Description = t.Description,
                    // Schema estricto: los argumentos validan exactamente contra el esquema,
                    // en vez de llegar con campos inventados que después hay que filtrar.
                    Strict = true,
                    InputSchema = new() { Properties = properties, Required = required }
                });
            })
            .ToArray();

        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig { Effort = ParseEffort(_options.Effort) },
            // El prompt de sistema es idéntico en todos los check-ins: marcarlo para caché hace
            // que desde el segundo se pague ~10%. El contexto variable va después, en el primer
            // mensaje del usuario, para no invalidar el prefijo.
            System = new List<TextBlockParam>
            {
                new()
                {
                    Text = request.SystemPrompt,
                    CacheControl = new CacheControlEphemeral()
                }
            },
            Tools = tools,
            Messages = messages
        };

        var response = await _client.Messages.Create(parameters, cancellationToken: ct);

        if (response.StopReason == "refusal")
        {
            _logger.LogWarning("El modelo rechazó la solicitud: {Detalle}", response.StopDetails?.Explanation);
            return new AgentTurn(
                "No puedo continuar con esta conversación.",
                [],
                new AgentUsage(0, 0, 0));
        }

        string? text = null;
        var calls = new List<ToolCall>();

        foreach (var block in response.Content)
        {
            if (block.TryPickText(out var textBlock))
            {
                text = string.IsNullOrEmpty(text) ? textBlock.Text : text + "\n" + textBlock.Text;
            }
            else if (block.TryPickToolUse(out var toolUse))
            {
                calls.Add(new ToolCall(
                    toolUse.ID,
                    toolUse.Name,
                    FromInputDictionary(toolUse.Input)));
            }
        }

        return new AgentTurn(
            text,
            calls,
            new AgentUsage(
                (int)response.Usage.InputTokens,
                (int)response.Usage.OutputTokens,
                (int)(response.Usage.CacheReadInputTokens ?? 0)));
    }

    private static Effort ParseEffort(string value) => value.ToLowerInvariant() switch
    {
        "low" => Effort.Low,
        "medium" => Effort.Medium,
        "max" => Effort.Max,
        _ => Effort.High
    };

    private static Dictionary<string, JsonElement> ToInputDictionary(JsonObject arguments)
    {
        var result = new Dictionary<string, JsonElement>();
        foreach (var pair in arguments)
        {
            result[pair.Key] = JsonSerializer.Deserialize<JsonElement>(pair.Value?.ToJsonString() ?? "null");
        }
        return result;
    }

    private static JsonObject FromInputDictionary(IReadOnlyDictionary<string, JsonElement> input)
    {
        var result = new JsonObject();
        foreach (var pair in input)
        {
            result[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
        }
        return result;
    }
}
