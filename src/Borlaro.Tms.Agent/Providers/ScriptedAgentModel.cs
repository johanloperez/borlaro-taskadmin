using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Borlaro.Tms.Agent.Providers;

/// <summary>Un modelo guionado, sin red y sin clave de API. No sirve para conversar: sirve para
/// probar que el resto de la maquinaria funciona —el bucle de turnos, la ejecución de
/// herramientas, la cola de aprobaciones, el transcript, el presupuesto de turnos— sin gastar
/// tokens ni depender de que haya conexión. Es lo que permite verificar la Fase 4 en una máquina
/// sin clave.
///
/// Las reglas por palabra clave de abajo son deliberadamente tontas: son un doble de prueba, no
/// un intento de comprensión. Con una clave configurada, este archivo no se instancia.</summary>
public partial class ScriptedAgentModel : IAgentModel
{
    public string ProviderName => "scripted";

    public Task<AgentTurn> CompleteAsync(AgentRequest request, CancellationToken ct = default)
    {
        var usage = new AgentUsage(0, 0, 0);

        var yaLeyoElTablero = request.Messages.Any(m =>
            m.Role == AgentRole.Assistant &&
            (m.ToolCalls ?? []).Any(c => c.Name == AgentTools.GetAssignedTasks));

        // Primer turno: siempre se lee el tablero antes de abrir la boca.
        if (!yaLeyoElTablero)
        {
            return Task.FromResult(new AgentTurn(
                null,
                [Call(AgentTools.GetAssignedTasks, new JsonObject())],
                usage));
        }

        var tablero = request.Messages
            .LastOrDefault(m => m.Role == AgentRole.Tool)?.ToolResult?.Content;

        var ultimoDeLaPersona = request.Messages
            .LastOrDefault(m => m.Role == AgentRole.User)?.Text ?? "";

        // El primer mensaje de rol User es la instrucción de arranque, no algo que la persona
        // haya escrito: mientras siga siendo el único, seguimos en la apertura.
        var mensajesDeLaPersona = request.Messages.Count(m => m.Role == AgentRole.User);

        // El turno posterior a leer el tablero: se lo muestra y se pregunta.
        if (request.Messages[^1].Role == AgentRole.Tool && mensajesDeLaPersona <= 1)
        {
            return Task.FromResult(new AgentTurn(
                tablero is null
                    ? "No pude leer tu trabajo asignado."
                    : $"Esto es lo que tenés abierto:\n{tablero}\n¿Cómo viene?",
                [],
                usage));
        }

        // Ya se ejecutó la herramienta que pidió el turno anterior: se cierra y se deja de pedir.
        if (request.Messages[^1].Role == AgentRole.Tool)
        {
            return Task.FromResult(new AgentTurn(
                $"Listo: {request.Messages[^1].ToolResult?.Content}", [], usage));
        }

        var tarea = IdLegible().Match(ultimoDeLaPersona + "\n" + tablero).Value;
        var texto = ultimoDeLaPersona.ToLowerInvariant();

        if (tarea.Length > 0 && (texto.Contains("trabad") || texto.Contains("bloque")))
        {
            return Task.FromResult(new AgentTurn(
                "Lo dejo anotado como bloqueo.",
                [Call(AgentTools.FlagBlocker, new JsonObject
                {
                    ["task_id"] = tarea,
                    ["reason"] = ultimoDeLaPersona
                })],
                usage));
        }

        if (tarea.Length > 0 && (texto.Contains("no llego") || texto.Contains("mover la fecha")))
        {
            return Task.FromResult(new AgentTurn(
                "Propongo la fecha nueva; la tiene que aprobar un responsable.",
                [Call(AgentTools.RequestDateChange, new JsonObject
                {
                    ["task_id"] = tarea,
                    ["new_due_date"] = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd"),
                    ["reason"] = ultimoDeLaPersona
                })],
                usage));
        }

        if (tarea.Length > 0 && (texto.Contains("listo") || texto.Contains("termin")))
        {
            return Task.FromResult(new AgentTurn(
                "Registro el avance.",
                [Call(AgentTools.LogProgress, new JsonObject
                {
                    ["task_id"] = tarea,
                    ["progress_pct"] = 100,
                    ["note"] = ultimoDeLaPersona
                })],
                usage));
        }

        return Task.FromResult(new AgentTurn("Anotado. Cualquier cosa, mañana seguimos.", [], usage));
    }

    private static ToolCall Call(string name, JsonObject arguments) =>
        new($"scripted-{Guid.NewGuid():N}", name, arguments);

    [GeneratedRegex(@"[A-Z]{2,}\d*-\d+")]
    private static partial Regex IdLegible();
}
