namespace Borlaro.Tms.Domain;

/// <summary>Rol dentro de la organización. Define qué puede hacer alguien a nivel de toda su
/// empresa, no dentro de un proyecto puntual — para eso está <see cref="ProjectRole"/>.</summary>
public enum UserRole
{
    /// <summary>Administrador de la organización: la configura, gestiona a su gente y sus
    /// plantillas, y entra a cualquier proyecto de la organización sin ser miembro. No ve nada de
    /// las otras organizaciones.</summary>
    Admin = 0,

    /// <summary>Líder de equipo: crea proyectos (queda como líder de los que crea) y ve el
    /// panorama de toda la organización. Sobre un proyecto que no lidera, es un miembro más.</summary>
    Manager = 1,

    /// <summary>Trabaja en los proyectos de los que es miembro. Puede liderar uno sin ser
    /// Manager: el liderazgo de proyecto es una responsabilidad, no un ascenso.</summary>
    Collaborator = 2,

    /// <summary>Cliente o revisor externo: ve y aprueba lo suyo. No crea ni edita trabajo.</summary>
    ClientReviewer = 3,

    /// <summary>Quien opera la instalación: da de alta organizaciones, las suspende y las
    /// reactiva. **No ve el contenido de ninguna** —ni proyectos, ni tareas, ni conversaciones—,
    /// porque su cuenta vive en su propia organización y el filtro la acota igual que a
    /// cualquiera. Es un rol distinto de <see cref="Admin"/> justamente para poder decir «este
    /// administrador no puede ver la empresa de al lado»: con un solo rol para las dos cosas, esa
    /// frase no se puede escribir en el código.</summary>
    PlatformOperator = 4
}

/// <summary>Rol dentro de un proyecto concreto. Es lo que hace que «líder» sea una
/// responsabilidad sobre un proyecto y no un rango en la instancia: alguien puede liderar el
/// proyecto de video y ser un miembro más en el de desarrollo.</summary>
public enum ProjectRole
{
    /// <summary>Trabaja en el proyecto: crea, edita y mueve trabajo.</summary>
    Member = 0,

    /// <summary>Además administra el proyecto: arma el equipo, abre el formulario público y
    /// resuelve las propuestas del agente sobre este tablero.</summary>
    Lead = 1
}

/// <summary>Familia semántica de una etapa. El agente y los reportes razonan sobre esto,
/// no sobre el nombre de la etapa, que cambia según la disciplina.</summary>
public enum StageCategory
{
    Backlog = 0,
    Todo = 1,
    InProgress = 2,
    Blocked = 3,
    InReview = 4,
    Done = 5
}

public enum WorkItemPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>Qué tan difícil se cree que es la tarea. Tres niveles y no puntos Fibonacci: el
/// producto sirve a varias disciplinas y los puntos son jerga de una sola.
///
/// No está para reportar, está para modular al agente. Una tarea Alta que se atrasa es esperable
/// y amerita preguntar cuánto más falta; una Baja que se atrasa significa que pasó algo que nadie
/// previó, y eso amerita avisar antes.</summary>
public enum WorkItemDifficulty
{
    Baja = 0,
    Media = 1,
    Alta = 2
}

public enum CustomFieldType
{
    Text = 0,
    LongText = 1,
    Number = 2,
    Select = 3,
    MultiSelect = 4,
    Date = 5,
    Checkbox = 6,
    Url = 7,
    User = 8
}

/// <summary>Quién originó un cambio. `Agent` es la clave de la auditoría de la IA.</summary>
public enum ActorType
{
    User = 0,
    Agent = 1,
    System = 2
}

public enum IssueProviderKind
{
    Native = 0,
    GitHubIssues = 1
}

public enum DeliverableKind
{
    File = 0,
    Link = 1
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    ChangesRequested = 2
}

/// <summary>Ciclo de vida del check-in. `Partial` es lo que permite retomar una
/// conversación abandonada a mitad en vez de volver a empezar.</summary>
public enum CheckInStatus
{
    Pending = 0,
    Delivered = 1,
    Opened = 2,
    Partial = 3,
    Completed = 4,
    Missed = 5
}

public enum NotificationChannel
{
    InApp = 0,
    Desktop = 1,
    Email = 2,
    Slack = 3
}

/// <summary>Peldaño de la escalera de entrega. El orden importa: el scheduler avanza
/// al siguiente solo si el anterior no obtuvo acuse de apertura.</summary>
public enum EscalationStep
{
    FirstDesktopToast = 0,
    SecondDesktopToast = 1,
    FirstEmail = 2,
    SecondEmailAndManagerFeed = 3,
    MarkedMissed = 4
}

public enum AgentActionStatus
{
    Applied = 0,
    PendingApproval = 1,
    Rejected = 2
}

public enum RepoLinkKind
{
    Commit = 0,
    PullRequest = 1,
    Branch = 2
}
