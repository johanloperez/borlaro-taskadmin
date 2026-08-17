using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Infrastructure.Notifications;

/// <summary>Crea el check-in diario de cada persona a su hora local y hace avanzar la escalera
/// de los que vencieron. Un solo BackgroundService, sin Redis ni broker: para el volumen de un
/// equipo, un barrido cada minuto sobre un índice es de sobra.</summary>
public class CheckInScheduler(
    IServiceScopeFactory scopeFactory,
    Microsoft.Extensions.Options.IOptionsMonitor<NotificationOptions> options,
    Microsoft.Extensions.Options.IOptionsMonitor<CheckInOptions> checkInOptions,
    ILogger<CheckInScheduler> logger) : BackgroundService
{
    // Se relee en cada vuelta: cambiar el intervalo desde la interfaz no puede exigir reiniciar
    // el servicio que dispara los check-ins.
    private TimeSpan SweepInterval => TimeSpan.FromSeconds(Math.Max(1, options.CurrentValue.SweepIntervalSeconds));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Un respiro al arranque: la API todavía está aplicando migraciones.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BorlaroTmsDbContext>();
                var escalation = scope.ServiceProvider.GetRequiredService<EscalationService>();

                await CreateDueCheckInsAsync(db, escalation, stoppingToken);
                await escalation.RunDueAsync(stoppingToken);

                // Los relevos que ya cumplieron su ventana de agrupación. Va en este barrido y no
                // en uno propio porque comparte la única condición que importa —que algo haya
                // vencido— y un segundo temporizador para lo mismo son dos cosas que mantener.
                using (OrganizationScope.UseSystem())
                {
                    await scope.ServiceProvider
                        .GetRequiredService<HandoffService>()
                        .FlushAsync(stoppingToken);

                    // Vencer no es un hecho que alguien dispare, así que si no se barre no se
                    // entera nadie. Va acá por lo mismo que el relevo: la condición es que haya
                    // pasado el tiempo, y un segundo temporizador para eso es otra cosa que
                    // mantener.
                    var feed = scope.ServiceProvider.GetRequiredService<FeedService>();
                    await feed.BarrerVencidasAsync(stoppingToken);

                    // La poda va en el mismo barrido y no en uno diario: es una sentencia que casi
                    // siempre no borra nada, y un temporizador propio para eso no se paga.
                    await feed.PodarLeidasAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Un error en un barrido no puede matar el servicio: si se cae, nadie recibe
                // check-ins y el producto deja de existir en silencio.
                logger.LogError(ex, "Error en el barrido de check-ins");
            }

            try
            {
                await Task.Delay(SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task CreateDueCheckInsAsync(
        BorlaroTmsDbContext db,
        EscalationService escalation,
        CancellationToken ct)
    {
        // A quién le habla el agente: a quien ejecuta el trabajo.
        //
        // No a los admins ni a los líderes de proyecto — el check-in existe para preguntarle a
        // la persona que tiene la tarea en la mano cómo viene. Al líder el agente lo ayuda del
        // otro lado: escribiéndole a cada responsable y avisándole lo que sale de esas charlas.
        // Preguntarle a quien reparte el trabajo «¿cómo venís?» no tiene a quién responderle.
        //
        // Y solo a quien tiene algo abierto: sin trabajo asignado el check-in es una
        // interrupción diaria sin tema.
        var lideres = db.ProjectMembers
            .Where(m => m.Role == ProjectRole.Lead)
            .Select(m => m.UserId);

        // Sin filtro de organización: el barrido es de la instalación entera. Cada persona se
        // atiende después dentro de la suya.
        List<User> users;
        using (OrganizationScope.UseSystem())
        {
            users = await db.Users
                .Where(u => u.IsActive
                         && u.CheckInsEnabled
                         && u.Role != UserRole.Admin
                         && !lideres.Contains(u.Id)
                         && db.WorkItems.Any(i => i.AssigneeId == u.Id
                                               && i.ClosedAt == null
                                               && !i.Project!.IsArchived))
                .ToListAsync(ct);
        }

        var checkIns = checkInOptions.CurrentValue;

        var nowUtc = DateTimeOffset.UtcNow;

        foreach (var user in users)
        {
            using var scope = OrganizationScope.Use(user.OrganizationId);

            var zone = ResolveTimeZone(user.TimeZoneId);
            var localNow = TimeZoneInfo.ConvertTime(nowUtc, zone);
            var localDate = DateOnly.FromDateTime(localNow.DateTime);

            if (!user.WorksOn(localNow.DayOfWeek)) continue;
            if (TimeOnly.FromDateTime(localNow.DateTime) < user.CheckInTime) continue;

            // El índice único (UserId, LocalDate) hace idempotente al scheduler: si el barrido
            // corre dos veces en el mismo minuto, no se duplica el check-in.
            var exists = await db.CheckIns.AnyAsync(c => c.UserId == user.Id && c.LocalDate == localDate, ct);
            if (exists) continue;

            // En modo «cuando hace falta» el agente no aparece porque sí: aparece cuando el
            // tablero dejó de contar la verdad. Si todo está al día, no hay nada que preguntar y
            // callarse vale más que el ritual — un bot que interrumpe sin motivo se ignora, y
            // con él se ignora el día que sí importa.
            if (checkIns.OnlyWhenNeeded && !await SomethingNeedsAskingAsync(db, user.Id, checkIns.StaleDays, ct))
            {
                continue;
            }

            var scheduledLocal = new DateTimeOffset(
                localDate.Year, localDate.Month, localDate.Day,
                user.CheckInTime.Hour, user.CheckInTime.Minute, 0,
                zone.GetUtcOffset(localNow));

            var checkIn = new CheckIn
            {
                UserId = user.Id,
                User = user,
                ScheduledAt = scheduledLocal.ToUniversalTime(),
                LocalDate = localDate,
                Status = CheckInStatus.Pending
            };

            db.CheckIns.Add(checkIn);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Check-in creado para {User} ({Fecha})", user.Email, localDate);
            await escalation.StartAsync(checkIn, ct);
        }
    }

    /// <summary>Si hay algo que justifique interrumpir a esta persona hoy.
    ///
    /// Las primeras tres señales son las que hacen que un tablero mienta: algo que venció y sigue
    /// abierto, algo trabado sin resolver, y algo que nadie tocó en días. Ninguna se arregla sola,
    /// y todas se descubren tarde si nadie pregunta.
    ///
    /// La cuarta es distinta: **no tiene nada en curso pero sí cosas por hacer**. Ahí no hay nada
    /// roto todavía; lo que hay es una persona a punto de elegir en qué gasta el día, y ese es el
    /// único momento en que preguntar cambia algo. Si además no tiene nada por hacer, el agente no
    /// escribe: no hay conversación que valga la pena.</summary>
    private static async Task<bool> SomethingNeedsAskingAsync(
        BorlaroTmsDbContext db,
        Guid userId,
        int staleDays,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stale = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, staleDays));

        var abiertas = db.WorkItems.AsNoTracking()
            .Where(i => i.AssigneeId == userId && i.ClosedAt == null && !i.Project!.IsArchived);

        var algoAndaMal = await abiertas
            .AnyAsync(i => (i.DueDate != null && i.DueDate < today)
                        || i.UpdatedAt < stale
                        || i.Blockers.Any(b => b.ResolvedAt == null), ct);

        if (algoAndaMal) return true;

        var enCurso = await abiertas.AnyAsync(i => i.Stage!.Category == StageCategory.InProgress, ct);
        if (enCurso) return false;

        return await abiertas.AnyAsync(i => i.Stage!.Category == StageCategory.Todo, ct);
    }

    /// <summary>Una zona horaria mal escrita en un perfil no puede impedir que el resto del
    /// equipo reciba su check-in.</summary>
    private TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger.LogWarning("Zona horaria desconocida «{Zona}», se usa UTC", id);
            return TimeZoneInfo.Utc;
        }
    }
}
