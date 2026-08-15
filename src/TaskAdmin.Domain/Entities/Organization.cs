namespace TaskAdmin.Domain.Entities;

/// <summary>Una empresa dentro de la instalación: la dueña de todo lo demás. Personas,
/// proyectos, plantillas, entregables y conversaciones del agente pertenecen a una organización
/// y no se ven entre organizaciones.
///
/// Es una entidad y no «la cuenta del que se registró» a propósito. Si los proyectos colgaran de
/// una persona, esa persona no se podría ir de la empresa sin que fuera un incidente, no habría
/// forma de tener dos dueños, y la facturación y el dominio de email no tendrían dónde vivir.
/// GitHub, GitLab y Azure DevOps hacen lo mismo por las mismas razones.
///
/// En una instalación on-premise (`Tenancy:Mode = Single`) existe exactamente una y nadie la ve:
/// es el mismo código funcionando con una sola fila.</summary>
public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Identificador corto y estable para URLs y para nombrarla en soporte —«acme»—.
    /// Separado del nombre porque el nombre se edita: cambiar «Acme SRL» por «Acme» no puede
    /// romper enlaces repartidos.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Ruta relativa del logo en el almacenamiento de archivos. Null = la organización
    /// no cargó logo y la interfaz muestra el nombre. Se guarda al crear la organización y no
    /// vuelve a tocarse: quien quiera cambiar su logo pasa por el alta.</summary>
    public string? LogoPath { get; set; }

    /// <summary>Una organización suspendida existe y conserva sus datos, pero su gente no puede
    /// entrar. Es lo que permite cortar el acceso por falta de pago o por abuso sin borrar el
    /// trabajo de nadie — un borrado sería irreversible y casi nunca es lo que se quiere.</summary>
    public bool IsSuspended { get; set; }

    public DateTimeOffset? SuspendedAt { get; set; }
    public string? SuspendedReason { get; set; }

    /// <summary>La organización donde viven las cuentas de quien opera la instalación. No es un
    /// cliente: no aparece en el listado de organizaciones ni se puede suspender. Existe porque
    /// toda cuenta pertenece a una organización, también la del operador — y así el filtro la
    /// acota igual que a cualquiera, que es lo que garantiza que no vea contenido ajeno.</summary>
    public bool IsPlatform { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Marca a las entidades que pertenecen a una organización. El DbContext la usa para
/// dos cosas: aplicar el filtro global que impide leer filas de otra organización, y completar
/// <see cref="OrganizationId"/> al guardar.
///
/// La lleva incluso lo que podría deducir su organización por el padre —un <c>WorkItem</c> la
/// tiene por su proyecto—, y esa redundancia es justamente el punto: un `Include` o un `Any()`
/// escrito sin pensar en el aislamiento sigue estando filtrado, porque el filtro no depende de
/// que cada consulta se acuerde de acotarlo.</summary>
public interface IOrganizationScoped
{
    Guid OrganizationId { get; set; }
}
