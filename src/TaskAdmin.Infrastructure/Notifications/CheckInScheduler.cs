using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Infrastructure.Notifications;

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
                var db = scope.ServiceProvider.GetRequiredService<TaskAdminDbContext>();
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
        TaskAdminDbContext db,
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
                         && db.WorkItems.Any(i => i.AssigneeId == u.Id && i.ClosedAt == null))
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
    /// Las tres señales son las mismas que hacen que un tablero mienta: algo que venció y sigue
    /// abierto, algo trabado sin resolver, y algo que nadie tocó en días. Ninguna de las tres se
    /// arregla sola, y todas se descubren tarde si nadie pregunta.</summary>
    private static async Task<bool> SomethingNeedsAskingAsync(
        TaskAdminDbContext db,
        Guid userId,
        int staleDays,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var stale = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, staleDays));

        return await db.WorkItems
            .AsNoTracking()
            .AnyAsync(i => i.AssigneeId == userId
                        && i.ClosedAt == null
                        && ((i.DueDate != null && i.DueDate < today)
                            || i.UpdatedAt < stale
                            || i.Blockers.Any(b => b.ResolvedAt == null)), ct);
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
