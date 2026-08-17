namespace Borlaro.Tms.Domain.Entities;

/// <summary>Entregable de un work item: archivo subido o enlace externo (Figma, Drive,
/// Frame.io). Es el equivalente del pull request para equipos creativos, y el punto donde
/// los trackers de ingeniería les fallan.</summary>
public class Deliverable : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public string Name { get; set; } = string.Empty;
    public DeliverableKind Kind { get; set; }

    /// <summary>Número de la versión vigente. Las versiones anteriores se conservan.</summary>
    public int CurrentVersion { get; set; }

    /// <summary>Versión aprobada, si hubo alguna. Permite responder "¿cuál es la buena?"
    /// sin recorrer las rondas.</summary>
    public Guid? ApprovedVersionId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<DeliverableVersion> Versions { get; set; } = new List<DeliverableVersion>();
}

public class DeliverableVersion : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliverableId { get; set; }
    public Deliverable? Deliverable { get; set; }

    public int Version { get; set; }

    /// <summary>Ruta relativa en el IFileStore cuando Kind = File.</summary>
    public string? FilePath { get; set; }

    /// <summary>URL externa cuando Kind = Link.</summary>
    public string? Url { get; set; }

    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long? SizeBytes { get; set; }
    public string? Notes { get; set; }

    public Guid UploadedById { get; set; }
    public User? UploadedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ReviewRound> ReviewRounds { get; set; } = new List<ReviewRound>();
}

/// <summary>Una vuelta de revisión sobre una versión concreta. Contar filas por work item da
/// "cuántas rondas llevó", que es la métrica que le importa a un equipo de diseño o edición.</summary>
public class ReviewRound : IOrganizationScoped
{
    public Guid OrganizationId { get; set; }

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkItemId { get; set; }
    public WorkItem? WorkItem { get; set; }

    public Guid DeliverableVersionId { get; set; }
    public DeliverableVersion? DeliverableVersion { get; set; }

    public Guid ReviewerId { get; set; }
    public User? Reviewer { get; set; }

    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;
    public string? CommentsMd { get; set; }

    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAt { get; set; }

    public bool IsOpen => ClosedAt is null;
}
