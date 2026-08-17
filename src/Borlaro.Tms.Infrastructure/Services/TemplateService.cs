using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;

namespace Borlaro.Tms.Infrastructure.Services;

public record TemplateStageInput(
    string Name,
    StageCategory Category,
    bool RequiresBlockerReason,
    bool OpensReviewRound,
    IReadOnlyList<string> AllowedNext);

public record TemplateFieldInput(
    string Key,
    string Label,
    CustomFieldType Type,
    IReadOnlyList<string> Options,
    bool Required,
    string? AgentHint);

public record TemplateInput(
    string Key,
    string Name,
    string? Description,
    string ItemNounSingular,
    string ItemNounPlural,
    IReadOnlyList<string> WorkItemTypes,
    string? AgentContext,
    IReadOnlyList<TemplateStageInput> Stages,
    IReadOnlyList<TemplateFieldInput> Fields);

/// <summary>Alta y edición de plantillas de disciplina. Es lo que permite que el sistema sirva a
/// un oficio que no previmos —un estudio de arquitectura, un equipo legal— sin tocar el binario.
///
/// La validación es estricta a propósito y corre al guardar, no al crear el proyecto: una
/// plantilla con una transición hacia una etapa que no existe produce un tablero con tareas
/// atrapadas, y descubrirlo tres semanas después, con trabajo real adentro, es mucho peor que un
/// mensaje de error ahora.</summary>
public class TemplateService(BorlaroTmsDbContext db)
{
    public Task<List<ProjectTemplate>> AllAsync(CancellationToken ct = default) =>
        db.ProjectTemplates.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public Task<ProjectTemplate?> ByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ProjectTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<ProjectTemplate> CreateAsync(TemplateInput input, CancellationToken ct = default)
    {
        var key = NormalizeKey(input.Key);

        if (await db.ProjectTemplates.AnyAsync(t => t.Key == key, ct))
        {
            throw new DomainException($"Ya existe una plantilla con la clave «{key}».");
        }

        var template = new ProjectTemplate { Key = key, IsBuiltIn = false };
        Apply(template, input);

        db.ProjectTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return template;
    }

    public async Task<ProjectTemplate> UpdateAsync(Guid id, TemplateInput input, CancellationToken ct = default)
    {
        var template = await db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainException("La plantilla no existe.");

        // La clave de las plantillas de fábrica es lo que hace idempotente al seed: si se
        // renombra, el próximo arranque vuelve a sembrar la original y quedan dos.
        var key = NormalizeKey(input.Key);
        if (template.IsBuiltIn && key != template.Key)
        {
            throw new DomainException(
                "La clave de una plantilla de fábrica no se puede cambiar. Duplicala y editá la copia.");
        }

        if (key != template.Key && await db.ProjectTemplates.AnyAsync(t => t.Key == key && t.Id != id, ct))
        {
            throw new DomainException($"Ya existe una plantilla con la clave «{key}».");
        }

        template.Key = key;
        Apply(template, input);

        await db.SaveChangesAsync(ct);
        return template;
    }

    /// <summary>Duplicar es el camino recomendado para partir de una plantilla de fábrica:
    /// editar la original funciona, pero perder la de referencia no tiene vuelta atrás.</summary>
    public async Task<ProjectTemplate> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var source = await db.ProjectTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainException("La plantilla no existe.");

        var key = source.Key;
        var suffix = 2;
        while (await db.ProjectTemplates.AnyAsync(t => t.Key == $"{key}-{suffix}", ct)) suffix++;

        var copy = new ProjectTemplate
        {
            Key = $"{key}-{suffix}",
            Name = $"{source.Name} (copia)",
            Description = source.Description,
            IsBuiltIn = false,
            ItemNounSingular = source.ItemNounSingular,
            ItemNounPlural = source.ItemNounPlural,
            WorkItemTypes = [.. source.WorkItemTypes],
            AgentContext = source.AgentContext,
            Stages = source.Stages.Select(s => new TemplateStage
            {
                Name = s.Name,
                Order = s.Order,
                Category = s.Category,
                RequiresBlockerReason = s.RequiresBlockerReason,
                OpensReviewRound = s.OpensReviewRound,
                AllowedNext = [.. s.AllowedNext]
            }).ToList(),
            Fields = source.Fields.Select(f => new TemplateField
            {
                Key = f.Key,
                Label = f.Label,
                Type = f.Type,
                Options = [.. f.Options],
                Required = f.Required,
                Order = f.Order,
                AgentHint = f.AgentHint
            }).ToList()
        };

        db.ProjectTemplates.Add(copy);
        await db.SaveChangesAsync(ct);

        return copy;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await db.ProjectTemplates.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new DomainException("La plantilla no existe.");

        if (template.IsBuiltIn)
        {
            throw new DomainException(
                "Las plantillas de fábrica no se borran: el arranque las vuelve a sembrar. " +
                "Si no las querés a la vista, renombralas.");
        }

        // Los proyectos clonan la plantilla al crearse —workflow, etapas y campos son suyos—,
        // así que borrarla no toca ningún tablero en marcha. La FK queda en null y listo.
        db.ProjectTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
    }

    public Task<int> ProjectsUsingAsync(Guid id, CancellationToken ct = default) =>
        db.Projects.CountAsync(p => p.TemplateId == id, ct);

    // ── Validación y armado ──────────────────────────────────────────────────

    private static void Apply(ProjectTemplate template, TemplateInput input)
    {
        Validate(input);

        template.Name = input.Name.Trim();
        template.Description = input.Description?.Trim() ?? string.Empty;
        template.ItemNounSingular = Blank(input.ItemNounSingular, "tarea");
        template.ItemNounPlural = Blank(input.ItemNounPlural, "tareas");
        template.AgentContext = input.AgentContext?.Trim() ?? string.Empty;

        template.WorkItemTypes = input.WorkItemTypes
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // El orden lo da la posición en la lista, no un número que el cliente pueda mandar
        // desordenado: así el tablero sale en el orden en que se ve el editor.
        template.Stages = input.Stages.Select((s, index) => new TemplateStage
        {
            Name = s.Name.Trim(),
            Order = index,
            Category = s.Category,
            RequiresBlockerReason = s.RequiresBlockerReason,
            OpensReviewRound = s.OpensReviewRound,
            AllowedNext = s.AllowedNext.Select(n => n.Trim()).Where(n => n.Length > 0).Distinct().ToList()
        }).ToList();

        template.Fields = input.Fields.Select((f, index) => new TemplateField
        {
            Key = f.Key.Trim(),
            Label = f.Label.Trim(),
            Type = f.Type,
            Options = f.Options.Select(o => o.Trim()).Where(o => o.Length > 0).ToList(),
            Required = f.Required,
            Order = index,
            AgentHint = string.IsNullOrWhiteSpace(f.AgentHint) ? null : f.AgentHint.Trim()
        }).ToList();
    }

    private static void Validate(TemplateInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new DomainException("La plantilla necesita un nombre.");

        if (input.Stages.Count < 2)
        {
            throw new DomainException("Una plantilla necesita al menos dos etapas: de dónde sale el trabajo y dónde termina.");
        }

        var names = input.Stages.Select(s => s.Name.Trim()).ToList();

        if (names.Any(string.IsNullOrWhiteSpace))
        {
            throw new DomainException("Hay una etapa sin nombre.");
        }

        var duplicated = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicated is not null)
        {
            throw new DomainException($"La etapa «{duplicated.Key}» está repetida. Los nombres identifican las transiciones.");
        }

        if (!input.Stages.Any(s => s.Category == StageCategory.Done))
        {
            throw new DomainException(
                "Falta una etapa de cierre (categoría «Hecho»). Sin ella el trabajo nunca se da por terminado " +
                "y el tablero no puede calcular nada.");
        }

        // Una transición hacia una etapa inexistente deja tareas atrapadas sin salida. Se
        // rechaza acá y no al crear el proyecto, que es tres semanas más tarde y con trabajo real.
        var known = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stage in input.Stages)
        {
            foreach (var next in stage.AllowedNext.Select(n => n.Trim()).Where(n => n.Length > 0))
            {
                if (!known.Contains(next))
                {
                    throw new DomainException(
                        $"La etapa «{stage.Name}» permite pasar a «{next}», que no es una etapa de esta plantilla.");
                }
            }
        }

        foreach (var stage in input.Stages.Where(s => s.Category != StageCategory.Done))
        {
            if (stage.AllowedNext.Count == 0)
            {
                throw new DomainException(
                    $"La etapa «{stage.Name}» no tiene ninguna salida. Una tarea que llegue ahí queda trabada.");
            }
        }

        foreach (var field in input.Fields)
        {
            if (string.IsNullOrWhiteSpace(field.Key)) throw new DomainException("Hay un campo sin clave.");
            if (string.IsNullOrWhiteSpace(field.Label)) throw new DomainException($"El campo «{field.Key}» no tiene etiqueta.");

            if (field.Type is CustomFieldType.Select or CustomFieldType.MultiSelect &&
                field.Options.Count(o => !string.IsNullOrWhiteSpace(o)) == 0)
            {
                throw new DomainException($"El campo «{field.Label}» es una lista y no tiene opciones.");
            }
        }

        var repeatedField = input.Fields
            .GroupBy(f => f.Key.Trim(), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (repeatedField is not null)
        {
            throw new DomainException($"La clave de campo «{repeatedField.Key}» está repetida.");
        }
    }

    /// <summary>Minúsculas y guiones: la clave viaja en el seed y en referencias de código, así
    /// que no puede depender de mayúsculas ni de espacios.</summary>
    private static string NormalizeKey(string key)
    {
        var normalized = new string(key.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray()).Trim('-');

        while (normalized.Contains("--")) normalized = normalized.Replace("--", "-");

        if (normalized.Length is 0 or > 50)
        {
            throw new DomainException("La clave de la plantilla tiene que tener entre 1 y 50 caracteres alfanuméricos.");
        }

        return normalized;
    }

    private static string Blank(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
