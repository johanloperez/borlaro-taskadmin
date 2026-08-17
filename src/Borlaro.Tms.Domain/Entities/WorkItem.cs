using System.Text.Json;

namespace Borlaro.Tms.Domain.Entities;

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

    /// <summary>Horas estimadas **originales**. Se fija al planificar y no se sobrescribe: lo que
    /// hace falta de más se agrega en `TimeExtensions`.
    ///
    /// Un solo número editable contesta «cuánto falta» y borra «cuánto nos equivocamos», que es la
    /// pregunta que sirve: una tarea de 4 horas que terminó en 20 no es un dato sobre esa tarea,
    /// es un dato sobre cómo estima el equipo.</summary>
    public decimal? Estimate { get; set; }

    /// <summary>Suma de las ampliaciones **no anuladas**, denormalizada. La fuente de verdad es
    /// `TimeExtensions`; esto existe para que el tablero muestre el total sin sumar filas en cada
    /// consulta.
    ///
    /// Nunca se incrementa a mano: se recalcula entero desde la tabla con `RecalcularAddedHours`.
    /// Sumarle de a poco funciona mientras haya un solo camino de escritura, y deja de funcionar
    /// en cuanto aparece el segundo —anular una ampliación— porque ahí ya hay dos lugares que
    /// pueden equivocarse y ninguno se entera del otro.
    ///
    /// El total comprometido es `Estimate + AddedHours`.</summary>
    public decimal AddedHours { get; set; }

    /// <summary>Vuelve a calcular el denormalizado desde las ampliaciones cargadas.
    ///
    /// Quien la llama tiene que haber traído `TimeExtensions`: con la colección vacía por no
    /// haberla incluido, esto pondría el total en cero y borraría el dato sin decir nada.</summary>
    public void RecalcularAddedHours() =>
        AddedHours = TimeExtensions.Where(e => e.VoidedAt is null).Sum(e => e.Hours);

    /// <summary>Qué tan difícil se cree que es. **Obligatoria: nadie crea una tarea sin decirlo.**
    ///
    /// No tiene valor por defecto a propósito. Un default convierte el nivel en «lo que salió»
    /// —«Baja» pasaría a significar tanto «es fácil» como «nadie dijo nada»— y entonces deja de
    /// ser un dato: el agente no puede modular sobre algo que la mitad de las veces no lo eligió
    /// nadie. Quien crea la tarea elige, y punto.
    ///
    /// **Puede cambiar después.** Una tarea que resultó más difícil de lo que parecía se
    /// reclasifica, y el cambio queda en el historial como cualquier otro.
    ///
    /// Cómo se llama cada nivel lo decide el proyecto (`Project.DifficultyLabel*`); lo que se
    /// guarda son siempre estos tres.</summary>
    public WorkItemDifficulty Difficulty { get; set; }

    /// <summary>Cuándo se avisó por última vez de que esta tarea está vencida.
    ///
    /// Vencer no es un hecho que alguien dispare: es que pase el tiempo, así que lo detecta un
    /// barrido. Sin esta marca, cada pasada del barrido vuelve a avisar de las mismas tareas y el
    /// feed se vuelve inservible en un día. Se limpia cuando la tarea se cierra o cambia de fecha,
    /// que es cuando volvería a tener sentido avisar.</summary>
    public DateTimeOffset? OverdueNotifiedAt { get; set; }

    public ICollection<WorkItemTimeExtension> TimeExtensions { get; set; } = new List<WorkItemTimeExtension>();

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

/// <summary>Una ampliación de tiempo sobre una tarea: cuántas horas más, por qué, y quién lo dijo.
///
/// Es una fila y no un campo que se pisa porque lo que importa no es el total vigente sino la
/// historia: tres ampliaciones de dos horas y una de seis cuentan cosas distintas, y un número
/// editable las vuelve indistinguibles.</summary>
public class WorkItemTimeExtension : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    /// <summary>Horas que se agregan. Siempre positivo: esto no es para corregir a la baja, es
    /// para registrar que algo costó más. Bajar una estimación original es editarla, no ampliarla.</summary>
    public decimal Hours { get; set; }

    /// <summary>Por qué. Cuando la ampliación viene de un check-in, son las palabras de la persona
    /// —«me falta el render final»— y no una etiqueta.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Quién lo agregó. `Agent` es lo habitual: casi todas nacen de una conversación.</summary>
    public ActorType ActorType { get; set; } = ActorType.User;
    public Guid? ActorId { get; set; }

    /// <summary>El check-in que la originó, si vino de una conversación. Es lo que permite ir de
    /// «esta tarea creció seis horas» a «acá está lo que dijo la persona ese día».</summary>
    public Guid? CheckInId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Cuándo se anuló, si se anuló. Una ampliación anulada no cuenta para el total pero
    /// sigue estando.
    ///
    /// Hace falta porque **estas filas las escribe el agente solo**: si el modelo entiende mal, o
    /// la persona tira «como seis» y después resulta que era una, sin esto quedan seis horas
    /// registradas para siempre. Y se anula en vez de borrarse porque «acá hubo una ampliación que
    /// resultó estar mal» es información, y borrarla deja el historial contando una versión
    /// prolija de algo que no pasó así.</summary>
    public DateTimeOffset? VoidedAt { get; set; }

    public Guid? VoidedById { get; set; }
    public User? VoidedBy { get; set; }

    /// <summary>Por qué se anuló. Obligatorio al anular: sin motivo, una anulación es
    /// indistinguible de un error de quien anuló.</summary>
    public string? VoidReason { get; set; }

    public bool IsVoided => VoidedAt is not null;
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
