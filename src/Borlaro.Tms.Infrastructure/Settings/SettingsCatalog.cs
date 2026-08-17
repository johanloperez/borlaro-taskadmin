namespace Borlaro.Tms.Infrastructure.Settings;

public enum SettingKind { Text, Number, Boolean, Secret, Select }

/// <summary>De quién es la decisión que representa un ajuste.</summary>
public enum SettingScope
{
    /// <summary>De quien hospeda la instalación. Una sola respuesta para todas las empresas: no
    /// tendría sentido que cada una tuviera su propio login con Google o su propia duración de
    /// sesión, y algunos —como la URL pública— ni siquiera son configurables por empresa porque
    /// hay un solo servidor.</summary>
    Platform,

    /// <summary>De cada empresa. La plataforma pone un valor por defecto y cada organización
    /// puede reemplazarlo; sin reemplazo, hereda.</summary>
    Organization
}

public record SettingDefinition(
    string Key,
    string Group,
    string Label,
    string Help,
    SettingKind Kind,
    IReadOnlyList<string>? Options = null)
{
    public SettingScope Scope => SettingsCatalog.ScopeOf(Key);
}

/// <summary>El catálogo de lo que se puede configurar, y la regla que lo gobierna: si algo es
/// configurable, está acá y se edita desde la interfaz. Un knob que solo existe en
/// `appsettings.json` obliga a tener acceso al servidor para cambiar el horario de un check-in.
///
/// Lo único que queda deliberadamente afuera es la cadena de conexión a la base y la clave de
/// firma de los JWT: las dos se necesitan *antes* de poder leer la tabla de ajustes, así que
/// viven en variables de entorno. Son dos, se tocan una vez al instalar, y están documentadas.</summary>
public static class SettingsCatalog
{
    public const string CheckInsGroup = "Check-ins";
    public const string LadderGroup = "Escalera de entrega";
    public const string EmailGroup = "Email";
    public const string ModelGroup = "Modelo de IA";
    public const string StorageGroup = "Almacenamiento";
    public const string LinksGroup = "Enlaces";
    public const string SessionGroup = "Sesión";
    public const string LoginGroup = "Inicio de sesión";
    public const string RegistrationGroup = "Altas";

    public static readonly IReadOnlyList<SettingDefinition> All =
    [
        new("CheckIns:Mode", CheckInsGroup, "Cuándo escribe el agente",
            "«diario» le escribe todos los días laborables a quien tenga trabajo abierto. " +
            "«cuando-hace-falta» escribe solo si hay algo que lo justifique: una tarea vencida, " +
            "una bloqueada, o una sin novedades hace demasiado. Es la diferencia entre un ritual " +
            "y una intervención — y lo segundo se ignora menos.",
            SettingKind.Select, ["diario", "cuando-hace-falta"]),

        new("CheckIns:StaleDays", CheckInsGroup, "Días sin actualizar que disparan el check-in",
            "Cuántos días puede quedarse quieta una tarea antes de que el agente pregunte. " +
            "Solo aplica en modo «cuando-hace-falta».",
            SettingKind.Number),

        new("Defaults:CheckInTime", CheckInsGroup, "Hora del check-in por defecto",
            "Hora local que se propone al dar de alta a una persona. Cada una puede tener la suya.",
            SettingKind.Text),

        new("Defaults:WorkDaysMask", CheckInsGroup, "Días laborables por defecto",
            "Banderas de día como número; 62 es lunes a viernes. Se ajusta por persona en su ficha.",
            SettingKind.Number),

        new("Notifications:HeartbeatToleranceSeconds", LadderGroup, "Tolerancia del heartbeat (s)",
            "Cuánto puede pasar sin latido antes de dar por muerta la app de escritorio y saltar a email.",
            SettingKind.Number),

        new("Notifications:SweepIntervalSeconds", LadderGroup, "Intervalo del barrido (s)",
            "Cada cuánto revisa el servidor si hay check-ins que crear o peldaños vencidos.",
            SettingKind.Number),

        new("Notifications:SecondToastAfterMinutes", LadderGroup, "Segundo aviso a los (min)",
            "Minutos desde el disparo del check-in hasta el segundo toast, si no lo abrió.",
            SettingKind.Number),

        new("Notifications:FirstEmailAfterMinutes", LadderGroup, "Primer email a los (min)",
            "Cuándo se cae a email. Sin heartbeat vivo, se salta acá sin esperar los toasts.",
            SettingKind.Number),

        new("Notifications:SecondEmailAfterMinutes", LadderGroup, "Segundo email a los (min)",
            "Segundo email y entrada en el feed del manager como «check-in sin entregar».",
            SettingKind.Number),

        new("Notifications:MarkMissedAfterMinutes", LadderGroup, "Marcar perdido a los (min)",
            "Cuándo se da por perdido el check-in y se cuenta en la métrica de cobertura.",
            SettingKind.Number),

        new("Notifications:PublicBaseUrl", LinksGroup, "URL pública del sistema",
            "Base de los enlaces que van en los emails. Si está mal, los enlaces no abren nada.",
            SettingKind.Text),

        new("Email:SmtpHost", EmailGroup, "Servidor SMTP",
            "Vacío = modo disco: los mensajes se escriben como archivos .eml y no sale correo real.",
            SettingKind.Text),

        new("Email:SmtpPort", EmailGroup, "Puerto", "587 con STARTTLS es lo habitual.", SettingKind.Number),
        new("Email:UseStartTls", EmailGroup, "Usar STARTTLS", "Cifra la conexión con el servidor.", SettingKind.Boolean),
        new("Email:Username", EmailGroup, "Usuario", "Vacío si el servidor no pide autenticación.", SettingKind.Text),
        new("Email:Password", EmailGroup, "Contraseña", "Se guarda cifrada y nunca se devuelve.", SettingKind.Secret),
        new("Email:FromAddress", EmailGroup, "Remitente", "Dirección desde la que salen los avisos.", SettingKind.Text),
        new("Email:FromName", EmailGroup, "Nombre del remitente", "Cómo se ve en la bandeja de quien recibe.", SettingKind.Text),

        new("Email:PickupDirectory", EmailGroup, "Carpeta del modo disco",
            "Dónde se escriben los .eml cuando no hay SMTP configurado.", SettingKind.Text),

        new("AgentModel:Provider", ModelGroup, "Proveedor",
            "anthropic usa Claude. openai-compatible sirve para Ollama, LM Studio o Groq. " +
            "scripted es un modelo de prueba sin red, para verificar la maquinaria sin gastar tokens.",
            SettingKind.Select, ["anthropic", "openai-compatible", "scripted"]),

        new("AgentModel:Model", ModelGroup, "Modelo", "Por ejemplo claude-opus-5, o el nombre del modelo local.", SettingKind.Text),
        new("AgentModel:ApiKey", ModelGroup, "Clave de API", "Se guarda cifrada y nunca se devuelve.", SettingKind.Secret),

        new("AgentModel:BaseUrl", ModelGroup, "URL base",
            "Solo para openai-compatible. Con la API en Docker y Ollama en la máquina, usá " +
            "http://host.docker.internal:11434/v1 — «localhost» ahí adentro es el propio " +
            "contenedor y no hay nadie escuchando.",
            SettingKind.Text),

        new("AgentModel:MaxTurns", ModelGroup, "Tope de turnos",
            "Máximo de turnos del modelo en una conversación. Es el freno de mano del costo.",
            SettingKind.Number),

        new("AgentModel:MaxTokens", ModelGroup, "Tope de tokens por respuesta", "Corta respuestas largas.", SettingKind.Number),

        new("AgentModel:Effort", ModelGroup, "Esfuerzo",
            "Cuánto piensa el modelo antes de responder. Para un check-in diario, medium alcanza.",
            SettingKind.Select, ["low", "medium", "high", "max"]),

        new("Storage:MaxBytes", StorageGroup, "Tamaño máximo de archivo (bytes)",
            "Tope por entregable subido. 209715200 son 200 MB.", SettingKind.Number),

        // Quién puede abrir una organización. Es de plataforma por el `ScopeOf` de abajo —el
        // prefijo `Tenancy:` no está entre los de organización— y eso importa: si un admin de
        // empresa pudiera tocarlo, estaría decidiendo por toda la instalación.
        //
        // `Tenancy:Mode` queda a propósito fuera del catálogo. Pasar a Single desde la interfaz
        // esconde la consola de plataforma, y el operador se quedaría sin la pantalla desde la
        // cual volver atrás. Ese interruptor se toca en el `.env`, una vez, al instalar.
        new("Tenancy:AllowRegistration", RegistrationGroup, "Permitir que se creen organizaciones",
            "Con esto en sí, cualquiera puede abrir su empresa desde la pantalla de entrada —con " +
            "email o con el proveedor externo— y queda como su administrador. Con esto en no, el " +
            "botón desaparece y las organizaciones las crea únicamente el operador desde su " +
            "consola. No afecta a las que ya existen ni a quienes ya tienen cuenta.",
            SettingKind.Boolean),

        new("Tenancy:AllowedRegistrationDomains", RegistrationGroup, "Dominios habilitados",
            "Separados por coma —«acme.com, acme.com.ar»—. Vacío deja entrar a cualquier dirección. " +
            "Es la diferencia entre publicar la instalación para un grupo conocido y publicarla " +
            "para internet. Solo aplica mientras las altas estén permitidas.",
            SettingKind.Text),

        new("Oidc:Enabled", LoginGroup, "Entrar con un proveedor externo",
            "Muestra el botón en la pantalla de entrada. Mientras esté en no, todo lo de acá abajo " +
            "queda guardado pero inerte. La contraseña sigue funcionando en los dos casos.",
            SettingKind.Boolean),

        new("Oidc:Authority", LoginGroup, "Autoridad (issuer)",
            "La URL del proveedor: https://accounts.google.com para Google, " +
            "https://login.microsoftonline.com/<tenant>/v2.0 para Entra, o la de tu Keycloak o " +
            "Authentik. De ahí se lee el documento de descubrimiento; no hace falta cargar las " +
            "URLs de autorización y token a mano.",
            SettingKind.Text),

        new("Oidc:ClientId", LoginGroup, "ID de cliente",
            "El identificador de la aplicación que te dio el proveedor.", SettingKind.Text),

        new("Oidc:ClientSecret", LoginGroup, "Secreto de cliente",
            "Se guarda cifrado y nunca se devuelve. Lo usa el servidor para canjear el código; " +
            "no viaja nunca al navegador.", SettingKind.Secret),

        new("Oidc:ButtonLabel", LoginGroup, "Texto del botón",
            "Lo que dice el botón de la pantalla de entrada. Por ejemplo «Entrar con Google».",
            SettingKind.Text),

        new("Oidc:Scopes", LoginGroup, "Permisos (scopes)",
            "«openid email profile» alcanza: hace falta el email para encontrar a quién vincular " +
            "la cuenta. Sin el scope email, el proveedor no lo manda y no se puede resolver nadie.",
            SettingKind.Text),

        new("Oidc:AllowedDomains", LoginGroup, "Dominios permitidos",
            "Separados por coma, por ejemplo «tuempresa.com». Vacío = cualquier dominio. Es una " +
            "restricción extra: para entrar hace falta igual estar dado de alta en Personas.",
            SettingKind.Text),

        new("Jwt:AccessTokenMinutes", SessionGroup, "Duración de la sesión (min)",
            "Cuánto vale el token antes de tener que volver a entrar. 720 son 12 horas. " +
            "Aplica a las sesiones nuevas; las abiertas siguen con su vencimiento original.",
            SettingKind.Number)
    ];

    public static SettingDefinition? Find(string key) =>
        All.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));

    /// <summary>De quién es cada ajuste, deducido del prefijo de la clave y no anotado uno por
    /// uno: los prefijos ya agrupan por tema, y una lista paralela de treinta entradas es una
    /// lista que se desactualiza el día que alguien agrega la treinta y uno.
    ///
    /// La regla en una frase: lo que hace a *cómo trabaja un equipo* es de la organización; lo
    /// que hace a *cómo está montado el servidor* es de la plataforma.</summary>
    public static SettingScope ScopeOf(string key)
    {
        // La URL pública es del servidor, no del equipo: hay una sola y de ella salen los enlaces
        // de todos los emails. Va antes que la regla general de Notifications:.
        if (key.Equals("Notifications:PublicBaseUrl", StringComparison.OrdinalIgnoreCase))
        {
            return SettingScope.Platform;
        }

        return key.Split(':')[0].ToLowerInvariant() switch
        {
            // El modelo y el SMTP son los dos que una empresa querría cambiar: para apuntar el
            // agente a su propio Ollama, o para que los avisos salgan desde su dominio.
            "agentmodel" or "email" => SettingScope.Organization,

            // Cuándo y cómo se le habla a la gente: horarios, días laborables, los peldaños de la
            // escalera. Cada equipo trabaja distinto.
            "checkins" or "defaults" or "notifications" => SettingScope.Organization,

            // Oidc, Jwt, Storage: uno solo por instalación.
            _ => SettingScope.Platform
        };
    }
}
