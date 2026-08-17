using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;

namespace Borlaro.Tms.Infrastructure.Notifications;

public record NotificationPayload(
    string Kind,
    string Title,
    string Body,
    string? LinkPath,
    Guid? CheckInId);

public record DeliveryResult(bool Delivered, string? Error)
{
    public static DeliveryResult Ok() => new(true, null);
    public static DeliveryResult Failed(string error) => new(false, error);
}

/// <summary>Un canal por el que se le puede hablar a una persona. Desktop, Email y —en la fase
/// que viene— Slack implementan lo mismo, así que agregar un canal es aditivo: la escalera de
/// entrega no sabe con cuál está hablando.</summary>
public interface INotificationChannel
{
    NotificationChannel Kind { get; }

    /// <summary>Si el canal puede llegarle a esta persona ahora. Desktop responde que no cuando
    /// no hay heartbeat vivo, y eso es lo que hace que la escalera salte a email sin esperar el
    /// timeout de apertura.</summary>
    Task<bool> CanDeliverAsync(User user, CancellationToken ct = default);

    Task<DeliveryResult> SendAsync(User user, NotificationPayload payload, CancellationToken ct = default);
}

/// <summary>Cuándo le escribe el agente a la gente.</summary>
public class CheckInOptions
{
    public const string SectionName = "CheckIns";

    /// <summary>«diario» o «cuando-hace-falta».</summary>
    public string Mode { get; set; } = "diario";

    /// <summary>Días sin movimiento que convierten una tarea en motivo para preguntar.</summary>
    public int StaleDays { get; set; } = 3;

    public bool OnlyWhenNeeded => Mode.Equals("cuando-hace-falta", StringComparison.OrdinalIgnoreCase);
}

public class NotificationOptions
{
    public const string SectionName = "Notifications";

    /// <summary>Base pública para armar los enlaces de los emails.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>Cuánto puede pasar sin heartbeat antes de dar por muerto un dispositivo.
    /// Tres latidos perdidos con el intervalo de 60 s de la app.</summary>
    public int HeartbeatToleranceSeconds { get; set; } = 195;

    /// <summary>Cada cuánto barre el scheduler. Configurable sobre todo para poder probar la
    /// escalera comprimida sin esperar horas.</summary>
    public int SweepIntervalSeconds { get; set; } = 30;

    /// <summary>Minutos hasta cada peldaño, contados desde el disparo del check-in.
    /// Configurables para poder probar la escalera comprimida en segundos.</summary>
    public int SecondToastAfterMinutes { get; set; } = 15;
    public int FirstEmailAfterMinutes { get; set; } = 45;
    public int SecondEmailAfterMinutes { get; set; } = 120;
    public int MarkMissedAfterMinutes { get; set; } = 240;
}
