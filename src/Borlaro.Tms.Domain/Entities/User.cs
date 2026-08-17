namespace Borlaro.Tms.Domain.Entities;

public class User : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>Vacío en las cuentas que entran solo por el proveedor externo. No es lo mismo que
    /// «contraseña vacía»: el login por contraseña rechaza estas cuentas antes de comparar nada,
    /// porque una cadena vacía no es un hash de BCrypt válido.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Collaborator;
    public bool IsActive { get; set; } = true;

    /// <summary>IANA, p. ej. "America/Argentina/Buenos_Aires". Determina cuándo se
    /// dispara el check-in de esta persona.</summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>En qué idioma le habla el sistema: «es», «en» o «pt».
    ///
    /// Nulo significa «el del navegador», que es el estado inicial de todo el mundo y el que
    /// acierta casi siempre. Se guarda solo cuando alguien elige explícitamente otro, y entonces
    /// esa elección manda: es la única forma de que quien tiene el navegador en inglés pero
    /// trabaja en portugués no tenga que pelearse con la detección todos los días.
    ///
    /// Determina también en qué idioma le llegan los emails de la escalera, que se arman en el
    /// servidor y no tienen navegador del cual deducirlo.</summary>
    public string? Language { get; set; }

    /// <summary>Hora local del check-in diario.</summary>
    public TimeOnly CheckInTime { get; set; } = new(9, 0);

    /// <summary>Días laborables como banderas de <see cref="DayOfWeek"/>: bit 0 = domingo.
    /// Por defecto lunes a viernes.</summary>
    public int WorkDaysMask { get; set; } = 0b0111110;

    public bool CheckInsEnabled { get; set; } = true;

    /// <summary>Puede asignar tareas a otros o cambiar asignaciones.
    ///
    /// Se suma al permiso del proyecto —hay que liderarlo *y* tener esto— y arranca en `true`
    /// porque es una **restricción, no una concesión**: antes de que existiera, cualquiera que
    /// liderara un proyecto repartía trabajo, y nacer apagado le quitaba esa capacidad a todos de
    /// golpe, sin ninguna pantalla donde devolvérsela. Apagarlo es la excepción: el coordinador
    /// que decide la carga no siempre es quien administra el proyecto.</summary>
    public bool CanAssignTasks { get; set; } = true;

    /// <summary>Puede fijar o mover la fecha de entrega de una tarea.
    ///
    /// Es un permiso propio y no una consecuencia de liderar, porque una fecha es un **compromiso
    /// con un tercero**: o se confía en que esta persona los asuma, o no. Eso no cambia según la
    /// tarea, y por eso va por persona y no por tarjeta como `WorkItem.AssigneeCanMove`.
    ///
    /// Es lo que hace posible el reemplazo por vacaciones sin darle el proyecto entero a alguien:
    /// se lo enciende, y alcanza.
    ///
    /// Encendido por defecto, igual que [CanAssignTasks]: hoy cualquiera que puede editar una
    /// tarea puede ponerle fecha, y nacer apagado se la quitaría a todos de golpe.</summary>
    public bool CanSetDueDate { get; set; } = true;

    /// <summary>Puede crear tareas en los proyectos donde participa.
    ///
    /// Se comprueba **además** del permiso del proyecto, así que encenderlo no mete a nadie en un
    /// tablero ajeno: solo decide si, ahí donde ya entra, puede además cargar trabajo. Existe para
    /// el caso inverso al de siempre —quitárselo a alguien que solo tiene que ejecutar lo que le
    /// dan— y para dárselo a quien tiene libertad sin liderar.</summary>
    public bool CanCreateTasks { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DeviceRegistration> Devices { get; set; } = new List<DeviceRegistration>();

    /// <summary>Las cuentas de proveedores externos vinculadas a esta persona.</summary>
    public ICollection<ExternalIdentity> ExternalIdentities { get; set; } = new List<ExternalIdentity>();

    public bool WorksOn(DayOfWeek day) => (WorkDaysMask & (1 << (int)day)) != 0;
}

/// <summary>Una instalación de la app de escritorio. El heartbeat es lo que decide si la
/// escalera de entrega puede usar el canal Desktop o debe saltar directo a email.</summary>
public class DeviceRegistration : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Token de dispositivo emitido desde la web y pegado en la app.</summary>
    public string Token { get; set; } = string.Empty;

    public string? AppVersion { get; set; }
    public string? OsInfo { get; set; }
    public string? MachineName { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Se considera vivo si latió dentro de la ventana de tolerancia
    /// (tres heartbeats perdidos con el intervalo de 60 s del plan).</summary>
    public bool IsAlive(DateTimeOffset now, TimeSpan tolerance) =>
        RevokedAt is null && LastHeartbeatAt is not null && now - LastHeartbeatAt.Value <= tolerance;
}
