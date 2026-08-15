namespace TaskAdmin.Domain.Entities;

public class Project : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Prefijo de los IDs legibles: DEV-142, DIS-88, VID-31.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Guid? TemplateId { get; set; }
    public ProjectTemplate? Template { get; set; }

    public Guid WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }

    /// <summary>Vocabulario heredado de la plantilla, editable por proyecto.</summary>
    public string ItemNounSingular { get; set; } = "tarea";
    public string ItemNounPlural { get; set; } = "tareas";
    public List<string> WorkItemTypes { get; set; } = new();

    /// <summary>Contexto de disciplina para el agente, heredado de la plantilla.</summary>
    public string AgentContext { get; set; } = string.Empty;

    /// <summary>Nativo (la tarea vive acá) o GitHub Issues (GitHub es dueño del issue y
    /// nosotros del metadata de gestión). Se decide por proyecto, no globalmente.</summary>
    public IssueProviderKind IssueProvider { get; set; } = IssueProviderKind.Native;

    public string? RepoUrl { get; set; }
    public string? RepoExternalId { get; set; }

    /// <summary>Formulario público de intake. Apagado por defecto: un endpoint anónimo que
    /// escribe en la base solo debe existir donde alguien lo pidió explícitamente.</summary>
    public bool IntakeEnabled { get; set; }

    /// <summary>Token secreto que va en la URL pública. No se usa la clave del proyecto porque
    /// es adivinable: quien sepa que existe «DIS» podría postear sin haber recibido el enlace.
    /// Regenerarlo invalida los enlaces repartidos, que es justo lo que se quiere si uno se filtra.</summary>
    public string? IntakeToken { get; set; }

    /// <summary>Texto que ve quien completa el formulario, para explicarle qué se espera.</summary>
    public string? IntakeInstructions { get; set; }

    /// <summary>Contador para asignar el próximo número legible. Se incrementa dentro de la
    /// transacción que crea el work item, para que no haya huecos ni duplicados.</summary>
    public int NextItemNumber { get; set; } = 1;

    /// <summary>Un proyecto archivado sigue existiendo y consultable; deja de aparecer en la
    /// lista por defecto y no admite trabajo nuevo. No se borra: su historial es el registro de
    /// lo que hizo el equipo.</summary>
    public bool IsArchived { get; set; }

    /// <summary>Quién y cuándo lo dio por terminado. Sin esto, «este proyecto está archivado» no
    /// se le puede reclamar a nadie.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid? ArchivedById { get; set; }
    public User? ArchivedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<CustomFieldDef> CustomFields { get; set; } = new List<CustomFieldDef>();
    public ICollection<WorkItem> Items { get; set; } = new List<WorkItem>();
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
}

public class ProjectMember : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Miembro o líder de este proyecto. El liderazgo es por proyecto, no por persona:
    /// la misma editora puede liderar la campaña de verano y ser un miembro más en la de
    /// invierno.</summary>
    public ProjectRole Role { get; set; } = ProjectRole.Member;

    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Workflow : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ICollection<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
}

public class WorkflowStage : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkflowId { get; set; }
    public Workflow? Workflow { get; set; }

    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
    public StageCategory Category { get; set; }
    public bool RequiresBlockerReason { get; set; }
    public bool OpensReviewRound { get; set; }

    /// <summary>Quién se hace cargo del trabajo que cae en esta etapa. Al mover una tarea acá, se
    /// le asigna a esta persona y se le avisa.
    ///
    /// **Va en la etapa y no en la tarea**, que es la decisión que hace que esto se use: un
    /// responsable por etapa *por tarea* obliga a llenar tantos campos como etapas tenga el
    /// workflow, cada vez que alguien crea una tarjeta. Nadie lo sostiene, y a la semana las
    /// notificaciones van a la persona equivocada — que es peor que no tenerlas. Acá se configura
    /// una vez al armar el proyecto, y cada tarea lo hereda al pasar.
    ///
    /// La excepción no necesita modelo propio: quien quiera otra persona en una tarea puntual la
    /// reasigna a mano después, y esa asignación manda.
    ///
    /// Nulo significa «esta etapa no cambia de manos»: la tarea sigue con quien la tenía. Es lo
    /// correcto para etapas de tránsito —«Bloqueado», «Pendiente»— donde el trabajo no pasa a
    /// nadie nuevo.</summary>
    public Guid? DefaultAssigneeId { get; set; }
    public User? DefaultAssignee { get; set; }

    /// <summary>Transiciones permitidas desde esta etapa. Vacío = sin restricción.
    /// Se valida en el dominio, no en el prompt del agente: la IA no puede saltársela.</summary>
    public ICollection<WorkflowTransition> AllowedTransitions { get; set; } = new List<WorkflowTransition>();

    /// <summary>Lista de responsables que pueden recibir trabajo en esta etapa.
    /// Si está vacía, se usa el DefaultAssigneeId antiguo (compatibilidad atrás).</summary>
    public ICollection<StageResponsible> Responsibles { get; set; } = new List<StageResponsible>();
}

public class WorkflowTransition : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromStageId { get; set; }
    public WorkflowStage? FromStage { get; set; }
    public Guid ToStageId { get; set; }
    public WorkflowStage? ToStage { get; set; }
}

/// <summary>Un responsable en la lista de una etapa. Cuando la tarea llega,
/// se elige de esta lista según carga de trabajo.</summary>
public class StageResponsible : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StageId { get; set; }
    public WorkflowStage? Stage { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public int Order { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Asignación específica de un usuario a una tarea para una etapa específica.
/// Si existe, manda sobre la lista de responsables de la etapa.</summary>
public class WorkItemStageAssignment : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public Guid StageId { get; set; }
    public WorkflowStage? Stage { get; set; }

    /// <summary>A quién asigno específicamente en esta tarea para esta etapa.
    /// Nulo = usa la lista de responsables de la etapa inteligentemente.</summary>
    public Guid? AssignedUserId { get; set; }
    public User? AssignedUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class CustomFieldDef : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Clave usada en el jsonb de <see cref="WorkItem.CustomFields"/>.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;
    public CustomFieldType Type { get; set; }
    public List<string> Options { get; set; } = new();
    public bool Required { get; set; }
    public int Order { get; set; }

    /// <summary>Se expone al agente para que sepa cuándo actualizar este campo.</summary>
    public string? AgentHint { get; set; }
}
