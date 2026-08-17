using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Borlaro.Tms.Infrastructure;

/// <summary>Permite a `dotnet ef migrations add` construir el contexto sin levantar la API ni
/// tener Postgres corriendo. La cadena solo se usa para inferir el proveedor: EF no se conecta
/// al generar una migración.</summary>
public class BorlaroTmsDbContextFactory : IDesignTimeDbContextFactory<BorlaroTmsDbContext>
{
    public BorlaroTmsDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TASKADMIN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=taskadmin;Username=taskadmin;Password=taskadmin";

        var options = new DbContextOptionsBuilder<BorlaroTmsDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BorlaroTmsDbContext(options);
    }
}
