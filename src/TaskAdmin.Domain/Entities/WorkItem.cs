using System.Text.Json;

namespace TaskAdmin.Domain.Entities;

/// <summary>Unidad de trabajo. Se llama WorkItem y no Task para no colisionar con
/// System.Threading.Tasks.Task, y porque el producto no es solo para software: una pieza
/// de diseño o un corte de video son work items igual que un bug.</summary>
public class WorkItem : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Número dentro del proyecto. Junto a Project.Key forma el ID legible (DEV-142).</summary>
    public int Number { get; set; }

    public string Title { get; set; } = string.Empty;
    public string DescriptionMd { get; set; } = string.Empty;

    /// <summary>Uno de los tipos definidos por la plantilla del proyecto.</summary>
    public string Type { get; set; } = string.Empty;

    public WorkItemPriority Priority { get; set; } = WorkItemPriority.Normal;

    public Guid StageId { get; set; }
    public WorkflowStage? Stage { get; set; }

    public Guid? AssigneeId { get; set; }
    public User? Assignee { get; set; }

    /// <summary>Qué puede hacer el responsable con su propia tarea. Se decide al asignarla y lo
    /// cambia el líder cuando quiere.
    ///
    /// Los valores por defecto no son neutros: mover el estado es el trabajo diario de quien la
    /// ejecuta —y es lo que alimenta al agente—, mientras que editar el enunciado o borrar la
    /// tarea es cambiar el encargo, y eso es de quien lo dio. Un responsable que puede reescribir
    /// su propia tarea puede hacer que siempre parezca cumplida.</summary>
    public bool AssigneeCanMove { get; set; } = true;

    public bool AssigneeCanEdit { get; set; }

    public bool AssigneeCanDelete { get; set; }
    public Guid? ReporterId { get; set; }
    public User? Reporter { get; set; }

    /// <summary>Horas o puntos según configuración del proyecto.</summary>
    public decimal? Estimate { get; set; }

    public int ProgressPct { get; set; }
    public DateOnly? DueDate { get; set; }

    /// <summary>Orden dentro de la columna del Kanban y del backlog priorizado.</summary>
    public double SortOrder { get; set; }

    /// <summary>Campos personalizados del proyecto, en jsonb con índice GIN. Evita tablas EAV
    /// y permite filtrar por campo arbitrario sin migraciones.</summary>
    public JsonDocument? CustomFields { get; set; }

    /// <summary>Identidad en el sistema externo cuando el proyecto usa GitHub Issues.</summary>
    public string? ExternalRef { get; set; }

    /// <summary>Quién envió el brief cuando el item entró por el formulario público. Van acá y
    /// no en ReporterId porque esa persona no tiene cuenta en el sistema.</summary>
    public string? SubmitterName { get; set; }
    public string? SubmitterEmail { get; set; }

    /// <summary>Quien tenia la tarea antes (para devoluciones).</summary>
    public Guid? PreviousAssigneeId { get; set; }
    public User? PreviousAssignee { get; set; }

    public Guid? PreviousStageId { get; set; }
    public bool AssignedBySystem { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }

    public ICollection<WorkItemStageAssignment> StageAssignments { get; set; } = new List<WorkItemStageAssignment>();

    public ICollection<WorkItemLabel> Labels { get; set; } = new List<WorkItemLabel>();
    public ICollection<WorkItemComment> Comments { get; set; } = new List<WorkItemComment>();
    public ICollection<WorkItemEvent> Events { get; set; } = new List<WorkItemEvent>();
    public ICollection<Blocker> Blockers { get; set; } = new List<Blocker>();
    public ICollection<Deliverable> Deliverables { get; set; } = new List<Deliverable>();
    public ICollection<RepoLink> RepoLinks { get; set; } = new List<RepoLink>();

    public string ReadableId(string projectKey) => $"{projectKey}-{Number}";
}

public class Label : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#94a3b8";
}

public class WorkItemLabel : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public Guid LabelId { get; set; }
    public Label? Label { get; set; }
}

public class WorkItemComment : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }
    public Guid AuthorId { get; set; }
    public User? Author { get; set; }
    public string BodyMd { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? EditedAt { get; set; }
}

public class WorkItemDependency : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BlockedItemId { get; set; }
    public WorkItem? BlockedItem { get; set; }
    public Guid BlockingItemId { get; set; }
    public WorkItem? BlockingItem { get; set; }
}

/// <summary>Historial completo y auditoría de la IA. Cada cambio deja una fila con quién lo
/// hizo; cuando ActorType es Agent, CheckInId apunta a la conversación que lo originó, lo que
/// hace toda acción del agente trazable y reversible.</summary>
public class WorkItemEvent : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public ActorType ActorType { get; set; }
    public Guid? ActorId { get; set; }

    /// <summary>Nombre del campo modificado, o un verbo para eventos que no son de campo
    /// ("created", "comment_added", "deliverable_uploaded").</summary>
    public string Field { get; set; } = string.Empty;

    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public Guid? CheckInId { get; set; }
    public CheckIn? CheckIn { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Bloqueo declarado. El motivo es obligatorio en las etapas marcadas como bloqueo,
/// y es una de las señales más valiosas que consume el agente.</summary>
public class Blocker : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>De quién depende el desbloqueo, si es una persona del sistema.</summary>
    public Guid? BlockedByUserId { get; set; }
    public User? BlockedByUser { get; set; }

    /// <summary>Dependencia externa cuando no es una persona registrada ("cliente", "proveedor").</summary>
    public string? BlockedByExternal { get; set; }

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }

    public bool IsOpen => ResolvedAt is null;
}

public class RepoLink : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public RepoLinkKind Kind { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? State { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
