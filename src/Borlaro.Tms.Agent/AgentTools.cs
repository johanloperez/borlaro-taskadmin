using System.Text.Json.Nodes;

namespace Borlaro.Tms.Agent;

/// <summary>Las diez herramientas del agente. Los esquemas son estrictos y los enums cerrados
/// a propósito: cuanto menos margen tenga el modelo para inventar, menos depende el producto de
/// que el modelo sea bueno. Un modelo chico corriendo local puede equivocarse; el dominio
/// rechaza lo inválido y la conversación sigue.</summary>
public static class AgentTools
{
    public const string GetAssignedTasks = "get_assigned_tasks";
    public const string UpdateTaskStatus = "update_task_status";
    public const string LogProgress = "log_progress";
    public const string SetCustomField = "set_custom_field";
    public const string FlagBlocker = "flag_blocker";
    public const string ReprioritizeDay = "reprioritize_day";
    public const string RequestDateChange = "request_date_change";
    public const string EscalateToManager = "escalate_to_manager";
    public const string CreateFollowupTask = "create_followup_task";
    public const string AddTimeEstimate = "add_time_estimate";

    /// <summary>Herramientas cuyo efecto NO se aplica solo: tocan fechas comprometidas o crean
    /// trabajo nuevo, así que se proponen y las aprueba una persona.</summary>
    public static readonly HashSet<string> RequireApproval =
    [
        RequestDateChange,
        CreateFollowupTask
    ];

    public static IReadOnlyList<ToolDefinition> All() =>
    [
        new(GetAssignedTasks,
            "Lee el trabajo asignado a la persona con la que estás hablando, con su etapa, fecha " +
            "objetivo, avance y bloqueos. Usalo al principio si necesitás refrescar el estado.",
            Object(new JsonObject(), [])),

        new(UpdateTaskStatus,
            "Mueve una tarea a otra etapa del flujo. Solo se permiten las transiciones válidas " +
            "del proyecto: si la rechaza, leé el error y probá una etapa alcanzable.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea, por ejemplo DEV-142."),
                ["stage_name"] = Str("Nombre exacto de la etapa destino, tal como figura en el tablero."),
                ["blocker_reason"] = Str("Motivo del bloqueo. Obligatorio solo si la etapa destino es de bloqueo.")
            }, ["task_id", "stage_name"])),

        new(LogProgress,
            "Registra el avance de una tarea y una nota corta de lo que la persona contó.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea."),
                ["progress_pct"] = Int("Porcentaje de avance, de 0 a 100.", 0, 100),
                ["note"] = Str("Resumen en una o dos frases de lo que dijo la persona.")
            }, ["task_id", "progress_pct"])),

        new(AddTimeEstimate,
            "Registra cuántas horas MÁS necesita una tarea que no se terminó en lo estimado. " +
            "Usalo cuando la persona diga que no llegó y te haya dicho cuánto más le falta. " +
            "No pises la estimación original: esto se suma. Si no te dijo un número, preguntáselo " +
            "antes de llamar a esta herramienta — no lo inventes ni lo redondees por tu cuenta.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea."),
                ["hours"] = Num("Horas adicionales que la persona calcula que le faltan. Mayor que cero.", 0.25, 200),
                ["reason"] = Str("Por qué necesita más tiempo, en las palabras de la persona.")
            }, ["task_id", "hours", "reason"])),

        new(SetCustomField,
            "Actualiza un campo personalizado del proyecto, como «versión de corte» o «entorno». " +
            "Usá exactamente la clave del campo tal como te la pasaron en el contexto.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea."),
                ["field_key"] = Str("Clave del campo, no su etiqueta visible."),
                ["value"] = Str("Valor nuevo. Para campos de lista, una de las opciones válidas.")
            }, ["task_id", "field_key", "value"])),

        new(FlagBlocker,
            "Marca que una tarea está trabada y por qué. Si depende de una persona del equipo, " +
            "indicá su nombre para que quede registrado de quién se está esperando.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea."),
                ["reason"] = Str("Qué está trabando el avance, en las palabras de la persona."),
                ["blocked_by"] = Str("Nombre de quien tiene que destrabarlo, si aplica.")
            }, ["task_id", "reason"])),

        new(ReprioritizeDay,
            "Reordena en qué va a enfocarse la persona hoy, según lo que te contó.",
            Object(new JsonObject
            {
                ["task_ids"] = new JsonObject
                {
                    ["type"] = "array",
                    ["description"] = "Identificadores legibles, del más importante al menos importante.",
                    ["items"] = new JsonObject { ["type"] = "string" }
                }
            }, ["task_ids"])),

        new(RequestDateChange,
            "Propone mover la fecha objetivo de una tarea. NO se aplica solo: queda pendiente de " +
            "que lo apruebe un responsable, porque es un compromiso con alguien más.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea."),
                ["new_due_date"] = Str("Fecha propuesta en formato aaaa-mm-dd."),
                ["reason"] = Str("Por qué no llega a la fecha original.")
            }, ["task_id", "new_due_date", "reason"])),

        new(EscalateToManager,
            "Levanta una alerta para el responsable del equipo: un riesgo, un bloqueo que lleva " +
            "días o algo que la persona no puede resolver sola.",
            Object(new JsonObject
            {
                ["task_id"] = Str("Identificador legible de la tarea, si la alerta es sobre una."),
                ["summary"] = Str("Qué tiene que saber el responsable, en una o dos frases."),
                ["severity"] = Enum("Qué tan urgente es.", ["info", "riesgo", "urgente"])
            }, ["summary", "severity"])),

        new(CreateFollowupTask,
            "Crea una tarea nueva que surgió de la conversación. NO se aplica sola: queda " +
            "pendiente de aprobación, porque crear trabajo afecta la carga del equipo.",
            Object(new JsonObject
            {
                ["project_key"] = Str("Clave del proyecto donde va la tarea, por ejemplo DEV."),
                ["title"] = Str("Título de la tarea nueva."),
                ["description"] = Str("Contexto necesario para que alguien la entienda sin esta charla."),
                ["difficulty"] = Enum(
                    "Qué tan difícil parece, según lo que te contaron. Es obligatorio: ninguna " +
                    "tarea se crea sin nivel. Quien aprueba lo va a ver y puede corregirlo.",
                    ["Baja", "Media", "Alta"])
            }, ["project_key", "title", "difficulty"]))
    ];

    private static JsonObject Object(JsonObject properties, string[] required) => new()
    {
        ["type"] = "object",
        ["properties"] = properties,
        ["required"] = new JsonArray(required.Select(r => (JsonNode)r!).ToArray()),
        // additionalProperties en false es lo que hace que el schema sea realmente estricto:
        // sin esto el modelo puede colar campos que nadie valida.
        ["additionalProperties"] = false
    };

    private static JsonObject Str(string description) => new()
    {
        ["type"] = "string",
        ["description"] = description
    };

    private static JsonObject Num(string description, double min, double max) => new()
    {
        ["type"] = "number",
        ["description"] = description,
        ["minimum"] = min,
        ["maximum"] = max
    };

    private static JsonObject Int(string description, int min, int max) => new()
    {
        ["type"] = "integer",
        ["description"] = description,
        ["minimum"] = min,
        ["maximum"] = max
    };

    private static JsonObject Enum(string description, string[] values) => new()
    {
        ["type"] = "string",
        ["description"] = description,
        ["enum"] = new JsonArray(values.Select(v => (JsonNode)v!).ToArray())
    };
}
