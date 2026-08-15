namespace TaskAdmin.Domain.Entities;

/// <summary>Un alta con email y contraseña que todavía no probó que la dirección existe.
///
/// La organización **no** se crea acá: se crea al confirmar. La diferencia importa porque este
/// formulario es público y crea empresas, que es la superficie más golosa que tiene el producto.
/// Sin este paso intermedio, cualquiera con un script deja la instalación con mil organizaciones
/// a nombre de direcciones que no existen, y limpiarlas después es entrar a la base a mano.
///
/// Con el proveedor de identidad no hace falta nada de esto: Google ya verificó la dirección, y
/// por eso ese camino entra directo.</summary>
public class PendingRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;

    /// <summary>Ruta del logo que se subió junto con el alta. Se guarda en el almacenamiento de
    /// archivos al enviar el formulario y se cuelga de la organización recién al confirmar: un
    /// logo sin empresa confirmada es un archivo huérfano, que es el costo menor de este flujo.</summary>
    public string? LogoPath { get; set; }

    /// <summary>Ya hasheada. La contraseña en claro no se guarda ni por un rato: si esta tabla se
    /// filtrara, sería una lista de credenciales de gente que ni siquiera llegó a tener cuenta.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Lo que viaja en el enlace del email. Es la única prueba de que quien se registró
    /// controla esa dirección.</summary>
    public string Token { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>Un día alcanza para revisar el correo, y no deja altas a medias dando vueltas una
    /// semana. Vencida, se registra de nuevo.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    public bool IsUsable(DateTimeOffset now) =>
        ConfirmedAt is null && now - CreatedAt <= Lifetime;
}
