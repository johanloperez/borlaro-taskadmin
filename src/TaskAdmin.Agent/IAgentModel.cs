using System.Text.Json.Nodes;

namespace TaskAdmin.Agent;

public record ToolDefinition(string Name, string Description, JsonObject InputSchema);

public record ToolCall(string Id, string Name, JsonObject Arguments);

public record ToolResult(string ToolCallId, string Content, bool IsError);

/// <summary>Un turno del modelo: texto para la persona y/o herramientas que quiere ejecutar.</summary>
public record AgentTurn(string? Text, IReadOnlyList<ToolCall> ToolCalls, AgentUsage Usage)
{
    public bool WantsTools => ToolCalls.Count > 0;
}

public record AgentUsage(int InputTokens, int OutputTokens, int CacheReadTokens);

public enum AgentRole { System, User, Assistant, Tool }

public record AgentMessage(AgentRole Role, string? Text, IReadOnlyList<ToolCall>? ToolCalls, ToolResult? ToolResult)
{
    public static AgentMessage FromUser(string text) => new(AgentRole.User, text, null, null);
    public static AgentMessage FromAssistant(string? text, IReadOnlyList<ToolCall>? calls) =>
        new(AgentRole.Assistant, text, calls, null);
    public static AgentMessage FromTool(ToolResult result) => new(AgentRole.Tool, null, null, result);
}

public record AgentRequest(
    string SystemPrompt,
    /// <summary>Contexto que cambia por proyecto y por persona. Va separado del prompt de
    /// sistema porque este último es idéntico en todos los check-ins y es lo que se cachea.</summary>
    string ContextBlock,
    IReadOnlyList<AgentMessage> Messages,
    IReadOnlyList<ToolDefinition> Tools);

/// <summary>El modelo detrás de una interfaz. Permite elegir Anthropic, cualquier API compatible
/// con OpenAI (incluidas las gratuitas y los modelos locales de Ollama o LM Studio) o un modelo
/// guionado para pruebas, sin que el resto del sistema se entere.</summary>
public interface IAgentModel
{
    string ProviderName { get; }

    Task<AgentTurn> CompleteAsync(AgentRequest request, CancellationToken ct = default);
}

public class AgentModelOptions
{
    public const string SectionName = "AgentModel";

    /// <summary>anthropic | openai-compatible | scripted</summary>
    public string Provider { get; set; } = "anthropic";

    public string Model { get; set; } = "claude-opus-5";

    public string? ApiKey { get; set; }

    /// <summary>Solo para openai-compatible. Ejemplos:
    /// Ollama local → http://localhost:11434/v1
    /// LM Studio    → http://localhost:1234/v1
    /// Groq         → https://api.groq.com/openai/v1</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Tope de turnos del modelo en una conversación. Sin esto, un modelo que insiste
    /// en llamar herramientas puede quedarse en un bucle y consumir el presupuesto de un día.</summary>
    public int MaxTurns { get; set; } = 8;

    public int MaxTokens { get; set; } = 2048;

    /// <summary>low | medium | high | xhigh | max. Solo lo usa el proveedor de Anthropic.</summary>
    public string Effort { get; set; } = "medium";
}
