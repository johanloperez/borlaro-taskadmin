using System.Text.Json;

namespace Borlaro.Tms.Domain.Entities;

/// <summary>Una conversación diaria del agente con una persona. Es una máquina de estados con
/// doble acuse: DeliveredAt ("el toast salió") y OpenedAt ("lo vio") son cosas distintas, y la
/// escalera de entrega avanza solo mientras falte el segundo.</summary>
public class CheckIn : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Momento en que correspondía disparar el check-in, en UTC, ya resuelto desde la
    /// hora local y la zona horaria de la persona.</summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>Día laboral al que corresponde, en hora local. Evita duplicados si el scheduler
    /// corre dos veces.</summary>
    public DateOnly LocalDate { get; set; }

    public CheckInStatus Status { get; set; } = CheckInStatus.Pending;

    /// <summary>Canal por el que efectivamente se entregó.</summary>
    public NotificationChannel? DeliveredVia { get; set; }

    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Peldaño alcanzado por la escalera de entrega.</summary>
    public EscalationStep EscalationStep { get; set; } = EscalationStep.FirstDesktopToast;

    /// <summary>Cuándo corresponde evaluar el próximo peldaño. El scheduler solo mira filas
    /// con este campo vencido, así no recorre toda la tabla.</summary>
    public DateTimeOffset? NextEscalationAt { get; set; }

    /// <summary>True cuando la persona cerró con "todo igual que ayer". Sin esta salida rápida,
    /// simplemente cierran la ventana y el dato se pierde.</summary>
    public bool ClosedAsNoChanges { get; set; }

    public string? Summary { get; set; }

    /// <summary>Transcript autoritativo de la conversación, incluyendo los bloques de
    /// herramienta. Vive acá aunque el transporte haya sido Slack.</summary>
    public JsonDocument? Transcript { get; set; }

    public int TurnCount { get; set; }

    /// <summary>Tokens consumidos, para el panel de gasto.</summary>
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int CacheReadTokens { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AgentAction> Actions { get; set; } = new List<AgentAction>();
    public ICollection<NotificationAttempt> Attempts { get; set; } = new List<NotificationAttempt>();

    /// <summary>Un check-in abierto a medias se retoma donde quedó.</summary>
    public bool IsResumable => Status is CheckInStatus.Opened or CheckInStatus.Partial;
}

/// <summary>Un peldaño concreto de la escalera, con su resultado. Permite auditar por qué una
/// persona no recibió el check-in y en qué canal se cayó.</summary>
public class NotificationAttempt : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? CheckInId { get; set; }
    public CheckIn? CheckIn { get; set; }

    public Guid? NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public NotificationChannel Channel { get; set; }
    public EscalationStep Step { get; set; }

    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? OpenedAt { get; set; }
    public string? Error { get; set; }
}

public class Notification : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public NotificationChannel Channel { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>Una ejecución de herramienta por parte del agente. Lo que toca fechas comprometidas
/// o crea trabajo nuevo queda en PendingApproval; el resto se aplica y queda reversible.</summary>
public class AgentAction : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CheckInId { get; set; }
    public CheckIn? CheckIn { get; set; }

    public string ToolName { get; set; } = string.Empty;
    public JsonDocument? Arguments { get; set; }

    public Guid? WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public AgentActionStatus Status { get; set; } = AgentActionStatus.Applied;

    public Guid? ApprovedById { get; set; }
    public User? ApprovedBy { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Resultado devuelto al modelo, o el error si la herramienta falló.</summary>
    public string? Result { get; set; }
    public bool IsError { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
