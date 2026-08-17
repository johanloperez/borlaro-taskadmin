using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Api.Endpoints;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Seeding;
using Borlaro.Tms.Infrastructure.Services;
using Borlaro.Tms.Infrastructure.Notifications;
using Borlaro.Tms.Infrastructure.Realtime;
using Borlaro.Tms.Infrastructure.Storage;
using Borlaro.Tms.Api.Realtime;
using Borlaro.Tms.Api.Tenancy;
using Borlaro.Tms.Infrastructure.Tenancy;
using Borlaro.Tms.Agent;
using Borlaro.Tms.Infrastructure.Settings;

var builder = WebApplication.CreateBuilder(args);

// ── Configuración ────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// Fallar al arrancar es preferible a arrancar con una clave de firma vacía y descubrirlo
// cuando alguien falsifique un token.
if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
{
    throw new InvalidOperationException(
        "Falta Jwt:SigningKey o es demasiado corta (mínimo 32 caracteres). " +
        "En desarrollo usá `dotnet user-secrets set \"Jwt:SigningKey\" \"<clave>\"`; " +
        "en producción, la variable de entorno Jwt__SigningKey.");
}

// ── Ajustes editables desde la interfaz ──────────────────────────────────────
// Se agrega como última fuente de configuración, así lo que el admin guarda desde la pantalla
// de Configuración pisa a appsettings.json y al entorno. Al revés, guardar no tendría efecto y
// la pantalla mentiría.
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Falta la cadena de conexión ConnectionStrings:Default.");

// El material de cifrado de los secretos no puede vivir en la base que estamos cifrando. Sale de
// Secrets:Key si está, y si no de la clave de firma de los JWT, que ya es obligatoria.
var secretKeyMaterial = builder.Configuration["Secrets:Key"];
if (string.IsNullOrWhiteSpace(secretKeyMaterial)) secretKeyMaterial = jwt.SigningKey;

var protector = new SecretProtector(secretKeyMaterial);
builder.Services.AddSingleton(protector);

// Vía IConfigurationBuilder: ConfigurationManager expone `Add` solo por esa interfaz.
((IConfigurationBuilder)builder.Configuration)
    .Add(new DatabaseConfigurationSource(connectionString, () => protector));

// ── Servicios ────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<BorlaroTmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<TokenService>();

// ── Entrar con el proveedor de identidad de la organización ──────────────────
// Por monitor y no por valor: el proveedor se configura desde la pantalla de Configuración, así
// que cargar el ID de cliente o cambiar de autoridad no puede pedir un redespliegue.
builder.Services.Configure<OidcOptions>(builder.Configuration.GetSection(OidcOptions.SectionName));
builder.Services.AddHttpClient(OidcService.HttpClientName);
builder.Services.AddSingleton<OidcDiscoveryCache>();
builder.Services.AddScoped<OidcService>();

builder.Services.Configure<TenancyOptions>(builder.Configuration.GetSection(TenancyOptions.SectionName));

builder.Services.AddScoped<OrganizationService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<TemplateService>();
builder.Services.AddScoped<ProjectAccess>();
builder.Services.AddScoped<WorkItemService>();
builder.Services.AddScoped<DeliverableService>();
builder.Services.AddScoped<IntakeService>();
builder.Services.AddScoped<MessagingService>();

// Límite de tasa para el formulario público. Es la única ruta anónima que escribe en la base,
// así que sin esto un script deja el backlog inutilizable en minutos. La ventana es por IP.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // El alta manda correo a una dirección que escribe quien llama: sin tope, el formulario sirve
    // para inundar el buzón de un tercero. Más estricto que el intake por eso mismo.
    options.AddPolicy(RegistrationEndpoints.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));

    options.AddPolicy(IntakeEndpoints.RateLimitPolicy, context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.Configure<FileStoreOptions>(builder.Configuration.GetSection(FileStoreOptions.SectionName));
builder.Services.AddSingleton<IFileStore, LocalFileStore>();

// ── Notificaciones y escalera de entrega ─────────────────────────────────────
builder.Services.Configure<NotificationOptions>(builder.Configuration.GetSection(NotificationOptions.SectionName));
builder.Services.Configure<CheckInOptions>(builder.Configuration.GetSection(CheckInOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

// El filtro abre el contexto de organización en cada invocación del hub: las llamadas de
// SignalR no vuelven a pasar por el pipeline HTTP, así que el middleware no las alcanza.
builder.Services.AddSignalR(options => options.AddFilter(new OrganizationHubFilter()));

// El orden importa: la escalera recorre los canales por tipo, pero registrar Desktop primero
// deja claro cuál es el canal por defecto del producto.
builder.Services.AddScoped<INotificationChannel, DesktopChannel>();

// Los cambios del tablero salen en vivo al grupo del proyecto. El dominio solo conoce la
// interfaz; sin este registro usaría NoBoardEvents y todo seguiría funcionando, mudo.
builder.Services.AddScoped<IBoardEvents, SignalRBoardEvents>();

// El canal de email se registra además por su tipo concreto, y la escalera resuelve ese mismo:
// el alta necesita mandar un correo a alguien que todavía no es usuario, y para eso pide
// EmailChannel directamente. Con dos registros independientes serían dos instancias, y la de la
// escalera dejaría de ser la que se configura.
builder.Services.AddScoped<EmailChannel>();
builder.Services.AddScoped<INotificationChannel>(sp => sp.GetRequiredService<EmailChannel>());

// Slack. Registrarlo es todo lo que hizo falta para que la escalera lo use: los dos primeros
// peldanos prueban los canales personales en orden y toman el primero que pueda entregar (§20).
// Sin token configurado responde que no puede, y la escalera sigue de largo.
builder.Services.AddScoped<SlackChannel>();
builder.Services.AddScoped<INotificationChannel>(sp => sp.GetRequiredService<SlackChannel>());
builder.Services.AddScoped<EscalationService>();

// El aviso de relevo: cuando una tarea cambia de manos al pasar de etapa. Es otra cosa que la
// escalera —no insiste ni escala— y por eso es un servicio aparte.
builder.Services.AddScoped<HandoffService>();
builder.Services.AddScoped<FeedService>();
builder.Services.AddHostedService<CheckInScheduler>();

// ── El agente ────────────────────────────────────────────────────────────────
builder.Services.Configure<AgentModelOptions>(builder.Configuration.GetSection(AgentModelOptions.SectionName));

builder.Services.AddHttpClient("agent-model");

// El proveedor se resuelve por conversación, no al arrancar: cambiar de Claude a un modelo local
// —o cargar la clave por primera vez— es un guardado en la pantalla de Configuración, no un
// redespliegue.
builder.Services.AddSingleton<AgentModelFactory>();

builder.Services.AddScoped<SettingsService>();

// El resolvedor de dos capas: lo que decidió la organización, y si no, lo de la plataforma. Lo
// usan el agente y el canal de email, que son los dos ajustes que una empresa querría cambiar.
builder.Services.AddScoped<OrganizationSettings>();
builder.Services.AddScoped<AgentToolExecutor>();
builder.Services.AddScoped<AgentApprovalService>();
builder.Services.AddScoped<CheckInConversation>();

// Enums como texto en el JSON. Con el valor numérico, un reordenamiento del enum cambia
// silenciosamente el significado de las respuestas ya integradas por los clientes, y el
// frontend tendría que mantener un mapa de números a nombres.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // SignalR no puede mandar el header Authorization en el handshake de WebSocket:
        // el token viaja como query string en las rutas del hub.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.IsAdmin, p => p.RequireRole(Roles.Admin))
    .AddPolicy(Policies.CanManage, p => p.RequireRole(Roles.Admin, Roles.Manager))
    .AddPolicy(Policies.IsTeamMember, p => p.RequireRole(Roles.Admin, Roles.Manager, Roles.Collaborator))
    .AddPolicy(Policies.IsPlatformOperator, p => p.RequireRole(Roles.PlatformOperator));

builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Borlaro TMS API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ── Arranque: migrar y sembrar plantillas ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BorlaroTmsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    await db.Database.MigrateAsync();

    var bootstrap = builder.Configuration.GetSection(BootstrapOptions.SectionName).Get<BootstrapOptions>()
                    ?? new BootstrapOptions();
    var organizations = scope.ServiceProvider.GetRequiredService<OrganizationService>();
    await BootstrapAdmin.EnsureAsync(db, organizations, bootstrap, logger);

    var tenancy = builder.Configuration.GetSection(TenancyOptions.SectionName).Get<TenancyOptions>()
                  ?? new TenancyOptions();
    var platform = builder.Configuration.GetSection(PlatformOptions.SectionName).Get<PlatformOptions>()
                   ?? new PlatformOptions();
    await BootstrapOperator.EnsureAsync(db, platform, tenancy, logger);

    // El seed va después del bootstrap y por organización: las plantillas de fábrica son de cada
    // empresa —se editan y se borran— así que cada una necesita su copia. Repasa todas en cada
    // arranque para que una versión nueva del producto pueda agregar una plantilla y que la
    // reciban también las organizaciones que ya existían.
    using (OrganizationScope.UseSystem())
    {
        foreach (var organizationId in await db.Organizations.Select(o => o.Id).ToListAsync())
        {
            await ProjectTemplateSeeder.SeedAsync(db, organizationId);
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("web");
app.UseRateLimiter();
app.UseAuthentication();

// Después de autenticar y antes de autorizar: el claim ya está leído, y todo lo que venga
// después —políticas, endpoints, servicios— corre dentro de la organización de quien llama.
app.UseMiddleware<OrganizationScopeMiddleware>();

app.UseAuthorization();

app.MapAuthEndpoints();
app.MapOidcEndpoints();
app.MapRegistrationEndpoints();
app.MapProjectEndpoints();
app.MapWorkItemEndpoints();
app.MapDeliverableEndpoints();
app.MapIntakeEndpoints();
app.MapDeviceEndpoints();
app.MapAgentEndpoints();
app.MapUserEndpoints();
app.MapSettingsEndpoints();
app.MapPlatformEndpoints();
app.MapMessageEndpoints();
app.MapFeedEndpoints();
app.MapSlackEndpoints();
app.MapTemplateEndpoints();
app.MapActivityEndpoints();
app.MapHub<AgentHub>("/hubs/agent");
app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous().WithTags("Health");

app.Run();
