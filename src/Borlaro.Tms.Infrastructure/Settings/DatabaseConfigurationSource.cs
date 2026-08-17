using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Borlaro.Tms.Infrastructure.Settings;

/// <summary>Hace que la tabla de ajustes sea una fuente de configuración más, y la última: lo
/// que el admin guarda desde la interfaz pisa a `appsettings.json` y a las variables de entorno.
///
/// El orden es deliberado. Si el entorno ganara, el admin podría guardar un ajuste, ver el
/// mensaje de éxito y que no pasara nada — el peor resultado posible para una pantalla de
/// configuración.</summary>
public class DatabaseConfigurationSource(string connectionString, Func<SecretProtector> protector)
    : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new DatabaseConfigurationProvider(connectionString, protector);
}

public class DatabaseConfigurationProvider(string connectionString, Func<SecretProtector> protector)
    : ConfigurationProvider
{
    public override void Load()
    {
        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var options = new DbContextOptionsBuilder<BorlaroTmsDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            using var db = new BorlaroTmsDbContext(options);

            var secrets = protector();

            foreach (var row in db.AppSettings.AsNoTracking().ToList())
            {
                var value = row.IsSecret ? secrets.Unprotect(row.Value) : row.Value;
                if (value is not null) data[row.Key] = value;
            }
        }
        catch (Exception ex) when (ex is PostgresException or NpgsqlException or InvalidOperationException)
        {
            // El primer arranque lee esta fuente antes de que exista la tabla: la migración se
            // aplica después. Que falte no puede impedir que la app levante — se recarga apenas
            // el admin guarde algo.
        }

        Data = data;
    }
}
