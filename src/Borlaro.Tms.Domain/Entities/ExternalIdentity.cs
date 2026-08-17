namespace Borlaro.Tms.Domain.Entities;

/// <summary>La cuenta de una persona en un proveedor de identidad externo (Google, Microsoft
/// Entra, Keycloak, Authentik). Es lo que permite entrar con las credenciales que la persona ya
/// tiene, sin que Borlaro TMS guarde otra contraseña más.
///
/// La identidad se ancla al <see cref="Subject"/> del proveedor, no al email: el email cambia
/// —una persona se casa, la empresa migra de dominio, un alias se reasigna— y anclarse a él
/// significaría que quien herede una dirección hereda la cuenta. El email solo se usa la primera
/// vez, para encontrar a quién vincular.</summary>
public class ExternalIdentity : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>El `iss` del proveedor, tal como viene en el id_token. Con el issuer en la clave,
    /// dos proveedores distintos pueden emitir el mismo subject sin pisarse.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>El `sub` del id_token: el identificador estable de la persona en el proveedor.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>El email que traía el proveedor al vincular. Informativo: para que el admin vea
    /// en /personas con qué cuenta entra cada uno, no para resolver el login.</summary>
    public string Email { get; set; } = string.Empty;

    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginAt { get; set; }
}

/// <summary>Un intento de login por OIDC, de punta a punta. La fila nace cuando la persona hace
/// clic en el botón y muere cuando el frontend canjea el ticket.
///
/// Existe porque el flujo tiene estado que no puede viajar por la URL: el `code_verifier` de PKCE
/// y el `nonce` no pueden ir en el `state` —quien intercepte el redirect los tendría— y el token
/// de sesión no puede volver en el query string, donde queda en el historial del navegador y en
/// los logs de cualquier proxy. Por eso el callback deja un ticket de un solo uso y el frontend
/// lo canjea por POST.</summary>
public class OidcLoginAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>El `state` de OAuth: viaja hasta el proveedor y vuelve. Es lo que ata la respuesta
    /// a este intento y corta el CSRF de login.</summary>
    public string State { get; set; } = string.Empty;

    /// <summary>El verificador de PKCE. Solo el servidor lo conoce, así que un `code` robado del
    /// redirect no se puede canjear sin él.</summary>
    public string CodeVerifier { get; set; } = string.Empty;

    /// <summary>Se compara contra el claim `nonce` del id_token: ata el token a este intento y no
    /// a uno anterior reproducido.</summary>
    public string Nonce { get; set; } = string.Empty;

    /// <summary>A dónde volver en la app después de entrar. Siempre una ruta relativa: una URL
    /// completa acá convierte al login en un redirector abierto hacia cualquier dominio.</summary>
    public string ReturnPath { get; set; } = "/";

    /// <summary>Ticket de un solo uso que el callback deja para el frontend. Nulo hasta que el
    /// intento resuelve en un usuario.</summary>
    public string? Ticket { get; set; }

    /// <summary>Cuándo se emitió el ticket. Su ventana se cuenta desde acá y no desde
    /// <see cref="CreatedAt"/>: entre las dos puede haber tardado diez minutos en escribir la
    /// contraseña del proveedor, y eso no debería comerse el tiempo del canje.</summary>
    public DateTimeOffset? TicketIssuedAt { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Cuando el proveedor devuelve a alguien que no tiene cuenta y el registro está
    /// abierto, el intento no muere: queda esperando a que esa persona le ponga nombre a su
    /// organización. Estos tres campos son lo que hace falta para crearla del otro lado, y viven
    /// acá y no en el navegador porque el navegador podría cambiarlos —y entonces cualquiera
    /// abriría una organización a nombre de otro—.</summary>
    public string? PendingEmail { get; set; }
    public string? PendingIssuer { get; set; }
    public string? PendingSubject { get; set; }

    /// <summary>El nombre que traía el proveedor, para no pedírselo de nuevo.</summary>
    public string? PendingName { get; set; }

    public bool IsRegistration => PendingEmail is not null && UserId is null;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Cuándo se canjeó el ticket. Un intento consumido no se vuelve a canjear.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    /// <summary>Ventana para completar la vuelta por el proveedor. Diez minutos alcanzan para
    /// tipear una contraseña y un segundo factor, y no dejan intentos abiertos toda la tarde.</summary>
    public static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    /// <summary>El ticket vive lo que tarda un redirect y un fetch. No hay razón para más.</summary>
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);

    /// <summary>El del registro dura más porque del otro lado hay una persona pensando cómo se
    /// llama su organización, no un fetch automático. Dos minutos ahí serían un formulario que
    /// vence mientras se completa.</summary>
    public static readonly TimeSpan RegistrationTicketLifetime = TimeSpan.FromMinutes(15);

    public TimeSpan LifetimeForTicket => IsRegistration ? RegistrationTicketLifetime : TicketLifetime;
}
