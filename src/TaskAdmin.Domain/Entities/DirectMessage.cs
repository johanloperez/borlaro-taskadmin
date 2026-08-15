namespace TaskAdmin.Domain.Entities;

/// <summary>Un mensaje de una persona a otra. Es el canal para lo que no cabe en un comentario
/// de tarea ni en el check-in: «necesito que priorices esto», «¿podés cubrir a Ana el jueves?».
///
/// Se entrega por los mismos canales que todo lo demás —toast del escritorio si la app está
/// viva, email si no— porque un mensaje que solo existe adentro de la web es un mensaje que se
/// lee tres días después.</summary>
public class DirectMessage : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid FromUserId { get; set; }
    public User? FromUser { get; set; }

    public Guid ToUserId { get; set; }
    public User? ToUser { get; set; }

    public string Body { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Cuándo lo abrió quien lo recibió. Null es «todavía no lo vio».</summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>Por qué canal se lo avisamos. Sirve para saber si la persona pudo haberse
    /// enterado antes de entrar a la web.</summary>
    public NotificationChannel? DeliveredVia { get; set; }
}
