using TaskAdmin.Domain.Entities;

namespace TaskAdmin.Domain;

public enum TransitionRejection
{
    None = 0,
    DifferentWorkflow,
    NotAllowed,
    BlockerReasonRequired
}

public readonly record struct TransitionCheck(bool IsAllowed, TransitionRejection Reason, string? Message)
{
    public static TransitionCheck Ok() => new(true, TransitionRejection.None, null);
    public static TransitionCheck Fail(TransitionRejection reason, string message) => new(false, reason, message);
}

/// <summary>Reglas de movimiento entre etapas. Son funciones puras sobre entidades ya cargadas:
/// no tocan la base ni dependen de quién pide el cambio.
///
/// Vive acá y no en el prompt del agente a propósito. Si la validación estuviera en el prompt,
/// la IA podría saltársela por error o porque el usuario la convenza; acá no hay forma de
/// evitarla — el agente usa las mismas herramientas que la UI y choca con el mismo muro.</summary>
public static class WorkflowRules
{
    public static TransitionCheck CanTransition(
        WorkflowStage from,
        WorkflowStage to,
        string? blockerReason)
    {
        if (from.WorkflowId != to.WorkflowId)
        {
            return TransitionCheck.Fail(
                TransitionRejection.DifferentWorkflow,
                "La etapa destino pertenece a otro workflow.");
        }

        // Quedarse en la misma etapa siempre es válido: es lo que pasa al reordenar dentro
        // de una columna del tablero.
        if (from.Id == to.Id)
        {
            return TransitionCheck.Ok();
        }

        // Sin transiciones declaradas, la etapa es libre. Permite plantillas simples sin
        // obligar a enumerar todo el grafo.
        var declared = from.AllowedTransitions;
        if (declared.Count > 0 && declared.All(t => t.ToStageId != to.Id))
        {
            return TransitionCheck.Fail(
                TransitionRejection.NotAllowed,
                $"No se puede pasar de «{from.Name}» a «{to.Name}».");
        }

        if (to.RequiresBlockerReason && string.IsNullOrWhiteSpace(blockerReason))
        {
            return TransitionCheck.Fail(
                TransitionRejection.BlockerReasonRequired,
                $"Mover a «{to.Name}» exige declarar el motivo del bloqueo.");
        }

        return TransitionCheck.Ok();
    }

    /// <summary>Etapas a las que se puede ir desde la actual. La UI la usa para no ofrecer
    /// movimientos que el backend va a rechazar.</summary>
    public static IEnumerable<WorkflowStage> AllowedTargets(WorkflowStage from, IEnumerable<WorkflowStage> all)
    {
        var declared = from.AllowedTransitions;
        if (declared.Count == 0)
        {
            return all.Where(s => s.WorkflowId == from.WorkflowId);
        }

        var ids = declared.Select(t => t.ToStageId).ToHashSet();
        return all.Where(s => ids.Contains(s.Id));
    }

    public static bool ClosesWork(WorkflowStage stage) => stage.Category == StageCategory.Done;

    public static bool OpensBlocker(WorkflowStage stage) =>
        stage.Category == StageCategory.Blocked || stage.RequiresBlockerReason;
}
