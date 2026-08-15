using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaskAdmin.Infrastructure;

/// <summary>Permite a `dotnet ef migrations add` construir el contexto sin levantar la API ni
/// tener Postgres corriendo. La cadena solo se usa para inferir el proveedor: EF no se conecta
/// al generar una migración.</summary>
public class TaskAdminDbContextFactory : IDesignTimeDbContextFactory<TaskAdminDbContext>
{
    public TaskAdminDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("TASKADMIN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=taskadmin;Username=taskadmin;Password=taskadmin";

        var options = new DbContextOptionsBuilder<TaskAdminDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new TaskAdminDbContext(options);
    }
}
