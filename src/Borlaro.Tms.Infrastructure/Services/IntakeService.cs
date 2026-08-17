using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Infrastructure.Services;

public record IntakeSubmission(
    string Title,
    string? Description,
    string? Type,
    string SubmitterName,
    string SubmitterEmail,
    JsonObject? CustomFields);

/// <summary>Formulario público: deja que un brief entre como work item sin que quien lo manda
/// tenga cuenta. Es la única superficie anónima con escritura del sistema, así que todo acá
/// está pensado para acotar el daño de un abuso.</summary>
public class IntakeService(BorlaroTmsDbContext db)
{
    public async Task<(string Token, Project Project)> EnableAsync(
        string projectKey,
        string? instructions,
        CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Key == projectKey.ToUpperInvariant(), ct)
            ?? throw new DomainException("El proyecto no existe.");

        // 32 bytes de aleatoriedad criptográfica: el token es la única barrera entre internet
        // y la cola de trabajo del equipo.
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        project.IntakeEnabled = true;
        project.IntakeToken = token;
        project.IntakeInstructions = instructions?.Trim();

        await db.SaveChangesAsync(ct);
        return (token, project);
    }

    public async Task DisableAsync(string projectKey, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Key == projectKey.ToUpperInvariant(), ct)
            ?? throw new DomainException("El proyecto no existe.");

        // Se borra el token, no solo se apaga el flag: si mañana se reactiva, los enlaces
        // viejos ya repartidos no vuelven a funcionar.
        project.IntakeEnabled = false;
        project.IntakeToken = null;

        await db.SaveChangesAsync(ct);
    }

    /// <summary>El formulario es anónimo: quien lo abre no tiene sesión, así que no hay
    /// organización en el contexto y la búsqueda tiene que ir sin filtro. El token es lo que
    /// hace de credencial —24 bytes aleatorios, único en toda la instalación— y de él sale a qué
    /// organización pertenece el resto de la operación.</summary>
    public async Task<Project?> FindByTokenAsync(string token, CancellationToken ct = default)
    {
        using var scope = OrganizationScope.UseSystem();

        return await db.Projects
            .AsNoTracking()
            .Include(p => p.CustomFields)
            .FirstOrDefaultAsync(p => p.IntakeToken == token && p.IntakeEnabled, ct);
    }

    public async Task<WorkItem> SubmitAsync(
        string token,
        IntakeSubmission submission,
        CancellationToken ct = default)
    {
        Project? found;
        using (OrganizationScope.UseSystem())
        {
            found = await db.Projects
                .Include(p => p.CustomFields)
                .Include(p => p.Workflow!).ThenInclude(w => w.Stages)
                .FirstOrDefaultAsync(p => p.IntakeToken == token && p.IntakeEnabled, ct);
        }

        var project = found ?? throw new DomainException("Este formulario no está disponible.");

        // Resuelto el proyecto, el resto corre dentro de su organización: es lo que estampa el
        // work item y su evento sin que este método tenga que asignarlos a mano.
        using var scope = OrganizationScope.Use(project.OrganizationId);

        if (string.IsNullOrWhiteSpace(submission.Title))
        {
            throw new DomainException("El título es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(submission.SubmitterName) ||
            string.IsNullOrWhiteSpace(submission.SubmitterEmail) ||
            !submission.SubmitterEmail.Contains('@'))
        {
            throw new DomainException("Hay que indicar nombre y un email válido de contacto.");
        }

        // Topes de longitud acá y no solo en la UI: el formulario es público y el cliente no
        // es de confianza.
        if (submission.Title.Length > 300 || (submission.Description?.Length ?? 0) > 5000)
        {
            throw new DomainException("El texto enviado es demasiado largo.");
        }

        var errors = CustomFieldValidator.Validate(submission.CustomFields, project.CustomFields.ToList());
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors.Select(e => e.Message)));
        }

        var type = submission.Type is not null && project.WorkItemTypes.Contains(submission.Type)
            ? submission.Type
            : project.WorkItemTypes.FirstOrDefault() ?? "Tarea";

        var firstStage = project.Workflow!.Stages.OrderBy(s => s.Order).First();

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var numbers = await db.Database
            .SqlQuery<int>($"""
                UPDATE "Projects"
                SET "NextItemNumber" = "NextItemNumber" + 1
                WHERE "Id" = {project.Id}
                RETURNING "NextItemNumber" - 1 AS "Value"
                """)
            .ToListAsync(ct);

        var item = new WorkItem
        {
            ProjectId = project.Id,
            Number = numbers.Single(),
            Title = submission.Title.Trim(),
            DescriptionMd = submission.Description?.Trim() ?? string.Empty,
            Type = type,
            Priority = WorkItemPriority.Normal,
            // Media, y es la única excepción a «el nivel lo elige quien crea la tarea»: acá quien
            // crea es alguien de afuera de la organización, y preguntarle a un cliente qué tan
            // difícil le resulta a este equipo su propio pedido no tiene sentido. Media es el punto
            // medio y no arrastra al agente hacia ninguno de los extremos.
            //
            // Que quede sin revisar no es un problema práctico: un pedido de intake entra sin
            // responsable, y el agente solo habla de trabajo asignado. Para cuando lo vea, alguien
            // lo tomó y pudo corregir el nivel.
            Difficulty = WorkItemDifficulty.Media,
            StageId = firstStage.Id,
            SubmitterName = submission.SubmitterName.Trim(),
            SubmitterEmail = submission.SubmitterEmail.Trim().ToLowerInvariant(),
            SortOrder = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            CustomFields = submission.CustomFields is null
                ? null
                : JsonDocument.Parse(submission.CustomFields.ToJsonString())
        };

        db.WorkItems.Add(item);
        db.WorkItemEvents.Add(new WorkItemEvent
        {
            WorkItemId = item.Id,
            // System y no User: no hay usuario detrás, y marcarlo así deja claro en el
            // historial que el item entró desde afuera.
            ActorType = ActorType.System,
            Field = "intake",
            NewValue = $"{item.SubmitterName} <{item.SubmitterEmail}>"
        });

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return item;
    }
}
