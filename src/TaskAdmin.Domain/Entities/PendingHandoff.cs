namespace TaskAdmin.Domain.Entities;

/// <summary>Un relevo esperando a ser avisado.
///
/// Existe para poder agrupar. Sin esto, cuatro tareas que caen en la misma etapa en diez minutos
/// son cuatro globos seguidos, y cuatro globos seguidos se cierran sin leer — que es peor que uno
/// solo, porque además entrena a ignorar los que vengan después.
///
/// Vive en la base y no en memoria a propósito. Un aviso pendiente que se pierde en un reinicio
/// es una persona que nunca se entera de que le llegó trabajo, y eso es exactamente la falla que
/// el relevo viene a resolver. Una cola en memoria hubiera sido menos código y habría fallado
/// justo en el caso que importa.
///
/// Se guarda el nombre y la clave de la tarea copiados, no una referencia: el aviso cuenta lo que
/// pasó en el momento en que pasó, y si la tarea se renombra después, el mensaje que se manda
/// —o el que ya se mandó— sigue siendo el que corresponde a ese momento.</summary>
public class PendingHandoff : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Quién recibe el trabajo. Es la clave por la que se agrupa.</summary>
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid WorkItemId { get; set; }

    public string ItemKey { get; set; } = string.Empty;
    public string ItemTitle { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;

    /// <summary>Quién venía de tenerla. Null cuando la tarea no tenía responsable.</summary>
    public string? FromName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Cuándo se avisó. Null = todavía en cola. No se borra la fila al avisar: sirve para
    /// entender después por qué alguien recibió lo que recibió.</summary>
    public DateTimeOffset? NotifiedAt { get; set; }
}
