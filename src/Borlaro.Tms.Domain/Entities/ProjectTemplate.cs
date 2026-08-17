namespace Borlaro.Tms.Domain.Entities;

/// <summary>Plantilla de disciplina (Desarrollo, Diseño, Edición, Contenido, Genérico).
/// Es el mecanismo que hace que un solo sistema sirva a cualquier equipo: define el
/// workflow, los campos, los tipos de trabajo, el vocabulario y el contexto del agente.
/// Vive en la base como dato, no en código — se edita y se crean nuevas desde la UI.</summary>
public class ProjectTemplate : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Clave estable para el seed y para referencias en código ("development", "design"…).</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    /// <summary>Etapas y transiciones válidas que se clonarán al crear un proyecto.</summary>
    public List<TemplateStage> Stages { get; set; } = new();

    /// <summary>Definiciones de campos personalizados a clonar.</summary>
    public List<TemplateField> Fields { get; set; } = new();

    /// <summary>Tipos de trabajo del oficio ("Bug", "Pieza", "Reel", "Artículo"…).</summary>
    public List<string> WorkItemTypes { get; set; } = new();

    /// <summary>Vocabulario de la disciplina para la UI: cómo llamar a una unidad de trabajo
    /// en singular y plural ("tarea"/"tareas", "pieza"/"piezas", "video"/"videos").</summary>
    public string ItemNounSingular { get; set; } = "tarea";
    public string ItemNounPlural { get; set; } = "tareas";

    /// <summary>Contexto que se inyecta al prompt del agente para que hable el idioma del
    /// oficio. Va DESPUÉS del corte de caché del prompt, porque varía por plantilla.</summary>
    public string AgentContext { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Etapa dentro de una plantilla. Se materializa como <see cref="WorkflowStage"/>
/// al crear un proyecto, de modo que editar la plantilla no rompe proyectos existentes.</summary>
public class TemplateStage
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public StageCategory Category { get; set; }

    /// <summary>Si es true, entrar a esta etapa obliga a declarar el motivo del bloqueo.
    /// Ese motivo es una de las señales que alimenta al agente.</summary>
    public bool RequiresBlockerReason { get; set; }

    /// <summary>Si es true, entrar a esta etapa abre una ronda de revisión.</summary>
    public bool OpensReviewRound { get; set; }

    /// <summary>Nombres de etapas a las que se puede pasar desde esta. Vacío = cualquiera.</summary>
    public List<string> AllowedNext { get; set; } = new();
}

public class TemplateField
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public CustomFieldType Type { get; set; }
    public List<string> Options { get; set; } = new();
    public bool Required { get; set; }
    public int Order { get; set; }

    /// <summary>Pista para el agente sobre cuándo actualizar este campo en una conversación.</summary>
    public string? AgentHint { get; set; }
}
