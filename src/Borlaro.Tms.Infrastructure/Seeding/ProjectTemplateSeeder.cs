using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure.Tenancy;

namespace Borlaro.Tms.Infrastructure.Seeding;

/// <summary>Las cinco plantillas de disciplina que vienen de fábrica. Son datos, no código:
/// una vez sembradas se editan desde la UI y se pueden crear nuevas sin tocar el binario.
/// El seed es idempotente — se identifica por Key y no pisa lo que el usuario haya cambiado.</summary>
public static class ProjectTemplateSeeder
{
    /// <summary>Dónde se anotan las de fábrica que el admin borró. Es una clave interna: no está en
    /// el catálogo de ajustes, así que no aparece en la pantalla de Configuración ni se puede pisar
    /// desde la API.
    ///
    /// Lleva la organización adentro de la clave porque la tabla de ajustes todavía es de la
    /// instalación entera: sin eso, que una empresa borre la plantilla de video le impediría a
    /// otra recibirla al crearse.</summary>
    public const string DeletedKeysSetting = "Internal:DeletedTemplateKeys";

    public static string DeletedKeysSettingFor(Guid organizationId) =>
        $"{DeletedKeysSetting}:{organizationId}";

    /// <summary>Siembra las plantillas de fábrica de una organización. Se llama al crear una
    /// organización y en cada arranque: una versión nueva del producto puede traer una plantilla
    /// más, y todas las empresas la reciben sin intervención.</summary>
    public static async Task SeedAsync(
        BorlaroTmsDbContext db,
        Guid organizationId,
        CancellationToken ct = default)
    {
        using var scope = OrganizationScope.Use(organizationId);

        var existing = await db.ProjectTemplates
            .Select(t => t.Key)
            .ToListAsync(ct);

        // Sembrar por Key es lo que hace que una versión nueva pueda agregar una plantilla de
        // fábrica sin pisar las editadas. El precio es que también repone las borradas, y entonces
        // el borrado duraría hasta el próximo reinicio: de ahí la lista de lápidas.
        var deleted = await DeletedKeysAsync(db, organizationId, ct);

        var missing = All()
            .Where(t => !existing.Contains(t.Key) && !deleted.Contains(t.Key))
            .ToList();
        if (missing.Count == 0) return;

        db.ProjectTemplates.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Las claves de fábrica que ya no se reponen porque alguien las borró a propósito.</summary>
    public static async Task<HashSet<string>> DeletedKeysAsync(
        BorlaroTmsDbContext db,
        Guid organizationId,
        CancellationToken ct = default)
    {
        var row = await db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == DeletedKeysSettingFor(organizationId), ct);

        return Split(row?.Value);
    }

    /// <summary>Anota una clave para que el arranque no la reponga. No guarda: lo hace quien
    /// borra, en la misma transacción que la baja de la plantilla — si se guardara acá, un fallo
    /// posterior dejaría la lápida sin muerto y la plantilla desaparecida para siempre.</summary>
    public static async Task RememberDeletedAsync(
        BorlaroTmsDbContext db,
        Guid organizationId,
        string key,
        CancellationToken ct = default)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == DeletedKeysSettingFor(organizationId), ct);

        var keys = Split(row?.Value);
        if (!keys.Add(key)) return;

        var value = string.Join(',', keys.OrderBy(k => k, StringComparer.Ordinal));

        if (row is null)
        {
            db.AppSettings.Add(new AppSetting { Key = DeletedKeysSettingFor(organizationId), Value = value });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Quita una clave de la lista: es lo que permite recuperar una plantilla de fábrica
    /// borrada sin entrar a la base a mano.</summary>
    public static async Task ForgetDeletedAsync(
        BorlaroTmsDbContext db,
        Guid organizationId,
        string key,
        CancellationToken ct = default)
    {
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == DeletedKeysSettingFor(organizationId), ct);
        if (row is null) return;

        var keys = Split(row.Value);
        if (!keys.Remove(key)) return;

        if (keys.Count == 0)
        {
            db.AppSettings.Remove(row);
            return;
        }

        row.Value = string.Join(',', keys.OrderBy(k => k, StringComparer.Ordinal));
        row.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static HashSet<string> Split(string? value) =>
        (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ProjectTemplate> All() =>
    [
        Development(),
        Design(),
        VideoEditing(),
        Content(),
        Generic()
    ];

    private static ProjectTemplate Development() => new()
    {
        Key = "development",
        Name = "Desarrollo de software",
        Description = "Equipos de producto y ingeniería. Ciclo con revisión de código y QA.",
        IsBuiltIn = true,
        ItemNounSingular = "tarea",
        ItemNounPlural = "tareas",
        WorkItemTypes = ["Bug", "Feature", "Tarea", "Spike", "Deuda técnica"],
        AgentContext =
            "Este es un equipo de desarrollo de software. Hablá de ramas, pull requests, entornos y " +
            "despliegues. Cuando algo lleva días En curso, preguntá si hay un PR abierto o si falta " +
            "resolver algo puntual. Una tarea En revisión sin movimiento suele significar que nadie la " +
            "está revisando: preguntá si conviene pedir revisor.",
        Stages =
        [
            new() { Name = "Backlog", Order = 0, Category = StageCategory.Backlog, AllowedNext = ["Por hacer"] },
            new() { Name = "Por hacer", Order = 1, Category = StageCategory.Todo, AllowedNext = ["En curso", "Backlog"] },
            new() { Name = "En curso", Order = 2, Category = StageCategory.InProgress, AllowedNext = ["Bloqueado", "En revisión", "Por hacer"] },
            new() { Name = "Bloqueado", Order = 3, Category = StageCategory.Blocked, RequiresBlockerReason = true, AllowedNext = ["En curso"] },
            new() { Name = "En revisión", Order = 4, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["QA", "En curso"] },
            new() { Name = "QA", Order = 5, Category = StageCategory.InReview, AllowedNext = ["Hecho", "En curso"] },
            new() { Name = "Hecho", Order = 6, Category = StageCategory.Done, AllowedNext = [] }
        ],
        Fields =
        [
            new() { Key = "environment", Label = "Entorno", Type = CustomFieldType.Select, Order = 0,
                    Options = ["Desarrollo", "Staging", "Producción"],
                    AgentHint = "Actualizalo si la persona menciona en qué entorno está probando o desplegando." },
            new() { Key = "release", Label = "Versión de release", Type = CustomFieldType.Text, Order = 1 },
            new() { Key = "branch", Label = "Rama", Type = CustomFieldType.Text, Order = 2,
                    AgentHint = "Se completa solo desde el conector Git cuando aparece una rama que menciona el ID." }
        ]
    };

    private static ProjectTemplate Design() => new()
    {
        Key = "design",
        Name = "Diseño",
        Description = "Equipos de diseño gráfico, producto o marca. Ciclo con revisión interna y de cliente.",
        IsBuiltIn = true,
        ItemNounSingular = "pieza",
        ItemNounPlural = "piezas",
        WorkItemTypes = ["Pieza", "Sistema", "Revisión", "Exploración"],
        AgentContext =
            "Este es un equipo de diseño. Hablá de piezas, entregables, versiones y rondas de revisión. " +
            "Una pieza parada en Revisión cliente varios días casi nunca es culpa del diseñador: " +
            "preguntá si el cliente respondió y ofrecé escalarlo. Si la persona menciona que subió una " +
            "versión nueva, confirmá si va a revisión.",
        Stages =
        [
            new() { Name = "Brief", Order = 0, Category = StageCategory.Backlog, AllowedNext = ["En cola"] },
            new() { Name = "En cola", Order = 1, Category = StageCategory.Todo, AllowedNext = ["Diseñando", "Brief"] },
            new() { Name = "Diseñando", Order = 2, Category = StageCategory.InProgress, AllowedNext = ["Revisión interna", "Bloqueado"] },
            new() { Name = "Bloqueado", Order = 3, Category = StageCategory.Blocked, RequiresBlockerReason = true, AllowedNext = ["Diseñando"] },
            new() { Name = "Revisión interna", Order = 4, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["Revisión cliente", "Ajustes"] },
            new() { Name = "Revisión cliente", Order = 5, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["Ajustes", "Aprobado"] },
            new() { Name = "Ajustes", Order = 6, Category = StageCategory.InProgress, AllowedNext = ["Revisión interna", "Revisión cliente"] },
            new() { Name = "Aprobado", Order = 7, Category = StageCategory.Done, AllowedNext = [] }
        ],
        Fields =
        [
            new() { Key = "format", Label = "Formato", Type = CustomFieldType.Select, Order = 0,
                    Options = ["Digital", "Impreso", "Social", "Web", "Packaging"] },
            new() { Key = "dimensions", Label = "Medidas", Type = CustomFieldType.Text, Order = 1 },
            new() { Key = "brand", Label = "Marca", Type = CustomFieldType.Text, Order = 2 },
            new() { Key = "source_url", Label = "Archivo fuente", Type = CustomFieldType.Url, Order = 3,
                    AgentHint = "Enlace a Figma o al archivo editable. Preguntá por él si la pieza está por entrar a revisión y falta." }
        ]
    };

    private static ProjectTemplate VideoEditing() => new()
    {
        Key = "video",
        Name = "Edición de video",
        Description = "Producción audiovisual: del guion al publicado, con corrección de color y revisión.",
        IsBuiltIn = true,
        ItemNounSingular = "video",
        ItemNounPlural = "videos",
        WorkItemTypes = ["Video", "Corto", "Reel", "Cobertura"],
        AgentContext =
            "Este es un equipo de edición audiovisual. Hablá de cortes, versiones, color, audio y masters. " +
            "Si la persona dice que un corte está listo, preguntá qué versión y si pasa a color o a revisión. " +
            "Los deadlines de publicación suelen ser duros: si el trabajo no va a llegar, es mejor saberlo " +
            "hoy que el día de publicar.",
        Stages =
        [
            new() { Name = "Brief", Order = 0, Category = StageCategory.Backlog, AllowedNext = ["Guion"] },
            new() { Name = "Guion", Order = 1, Category = StageCategory.Todo, AllowedNext = ["Rodaje", "Brief"] },
            new() { Name = "Rodaje", Order = 2, Category = StageCategory.InProgress, AllowedNext = ["Montaje", "Bloqueado"] },
            new() { Name = "Montaje", Order = 3, Category = StageCategory.InProgress, AllowedNext = ["Corrección de color", "Bloqueado"] },
            new() { Name = "Bloqueado", Order = 4, Category = StageCategory.Blocked, RequiresBlockerReason = true, AllowedNext = ["Montaje", "Rodaje"] },
            new() { Name = "Corrección de color", Order = 5, Category = StageCategory.InProgress, AllowedNext = ["Revisión"] },
            new() { Name = "Revisión", Order = 6, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["Ajustes", "Publicado"] },
            new() { Name = "Ajustes", Order = 7, Category = StageCategory.InProgress, AllowedNext = ["Revisión"] },
            new() { Name = "Publicado", Order = 8, Category = StageCategory.Done, AllowedNext = [] }
        ],
        Fields =
        [
            new() { Key = "duration_sec", Label = "Duración (seg)", Type = CustomFieldType.Number, Order = 0,
                    AgentHint = "Duración final del corte. Actualizalo si la persona menciona cuánto dura." },
            new() { Key = "platform", Label = "Plataforma", Type = CustomFieldType.MultiSelect, Order = 1,
                    Options = ["YouTube", "Instagram", "TikTok", "LinkedIn", "Web", "TV"] },
            new() { Key = "cut_version", Label = "Versión de corte", Type = CustomFieldType.Text, Order = 2,
                    AgentHint = "Actualizalo cuando la persona mencione una versión nueva del corte (v1, v2, corte final…)." },
            new() { Key = "music", Label = "Música / licencia", Type = CustomFieldType.Text, Order = 3 }
        ]
    };

    private static ProjectTemplate Content() => new()
    {
        Key = "content",
        Name = "Contenido",
        Description = "Redacción y publicación: blog, newsletter y redes, con aprobación previa.",
        IsBuiltIn = true,
        ItemNounSingular = "contenido",
        ItemNounPlural = "contenidos",
        WorkItemTypes = ["Artículo", "Newsletter", "Post", "Guion"],
        AgentContext =
            "Este es un equipo de contenido. Hablá de borradores, edición, aprobación y fecha de publicación. " +
            "Un contenido en Aprobación varios días suele estar esperando a alguien de fuera del equipo: " +
            "preguntá a quién y ofrecé escalarlo. La fecha de publicación manda sobre el resto.",
        Stages =
        [
            new() { Name = "Idea", Order = 0, Category = StageCategory.Backlog, AllowedNext = ["Investigación"] },
            new() { Name = "Investigación", Order = 1, Category = StageCategory.Todo, AllowedNext = ["Redacción", "Idea"] },
            new() { Name = "Redacción", Order = 2, Category = StageCategory.InProgress, AllowedNext = ["Edición", "Bloqueado"] },
            new() { Name = "Bloqueado", Order = 3, Category = StageCategory.Blocked, RequiresBlockerReason = true, AllowedNext = ["Redacción"] },
            new() { Name = "Edición", Order = 4, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["Aprobación", "Redacción"] },
            new() { Name = "Aprobación", Order = 5, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["Programado", "Edición"] },
            new() { Name = "Programado", Order = 6, Category = StageCategory.Todo, AllowedNext = ["Publicado"] },
            new() { Name = "Publicado", Order = 7, Category = StageCategory.Done, AllowedNext = [] }
        ],
        Fields =
        [
            new() { Key = "keyword", Label = "Palabra clave", Type = CustomFieldType.Text, Order = 0 },
            new() { Key = "channel", Label = "Canal", Type = CustomFieldType.Select, Order = 1,
                    Options = ["Blog", "Newsletter", "LinkedIn", "Instagram", "X"] },
            new() { Key = "publish_date", Label = "Fecha de publicación", Type = CustomFieldType.Date, Order = 2,
                    AgentHint = "Fecha comprometida de publicación. Si no va a llegar, proponé moverla en vez de dejarla vencer." }
        ]
    };

    private static ProjectTemplate Generic() => new()
    {
        Key = "generic",
        Name = "Genérico",
        Description = "Punto de partida mínimo para cualquier equipo. Se personaliza desde la UI.",
        IsBuiltIn = true,
        ItemNounSingular = "tarea",
        ItemNounPlural = "tareas",
        WorkItemTypes = ["Tarea"],
        AgentContext =
            "Equipo de disciplina no especificada. Usá lenguaje neutro: tarea, avance, bloqueo, fecha de " +
            "entrega. No asumas herramientas ni procesos que no aparezcan en la conversación.",
        Stages =
        [
            new() { Name = "Por hacer", Order = 0, Category = StageCategory.Todo, AllowedNext = ["En curso"] },
            new() { Name = "En curso", Order = 1, Category = StageCategory.InProgress, AllowedNext = ["Bloqueado", "Revisión"] },
            new() { Name = "Bloqueado", Order = 2, Category = StageCategory.Blocked, RequiresBlockerReason = true, AllowedNext = ["En curso"] },
            new() { Name = "Revisión", Order = 3, Category = StageCategory.InReview, OpensReviewRound = true, AllowedNext = ["Hecho", "En curso"] },
            new() { Name = "Hecho", Order = 4, Category = StageCategory.Done, AllowedNext = [] }
        ],
        Fields = []
    };
}
