using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TaskAdmin.Domain;
using TaskAdmin.Domain.Entities;
using TaskAdmin.Infrastructure.Tenancy;

namespace TaskAdmin.Infrastructure;

public class TaskAdminDbContext : DbContext
{
    public TaskAdminDbContext(DbContextOptions<TaskAdminDbContext> options) : base(options) { }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<User> Users => Set<User>();
    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<OidcLoginAttempt> OidcLoginAttempts => Set<OidcLoginAttempt>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();

    public DbSet<ProjectTemplate> ProjectTemplates => Set<ProjectTemplate>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<Workflow> Workflows => Set<Workflow>();
    public DbSet<WorkflowStage> WorkflowStages => Set<WorkflowStage>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();
    public DbSet<StageResponsible> StageResponsibles => Set<StageResponsible>();
    public DbSet<CustomFieldDef> CustomFieldDefs => Set<CustomFieldDef>();

    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemStageAssignment> WorkItemStageAssignments => Set<WorkItemStageAssignment>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<WorkItemLabel> WorkItemLabels => Set<WorkItemLabel>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<WorkItemDependency> WorkItemDependencies => Set<WorkItemDependency>();
    public DbSet<WorkItemEvent> WorkItemEvents => Set<WorkItemEvent>();
    public DbSet<Blocker> Blockers => Set<Blocker>();
    public DbSet<RepoLink> RepoLinks => Set<RepoLink>();

    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<DeliverableVersion> DeliverableVersions => Set<DeliverableVersion>();
    public DbSet<ReviewRound> ReviewRounds => Set<ReviewRound>();

    public DbSet<CheckIn> CheckIns => Set<CheckIn>();
    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<PendingHandoff> PendingHandoffs => Set<PendingHandoff>();
    public DbSet<AgentAction> AgentActions => Set<AgentAction>();

    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<OrganizationSetting> OrganizationSettings => Set<OrganizationSetting>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // ── Organizaciones ───────────────────────────────────────────────────────
        b.Entity<Organization>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(60).IsRequired();
            e.Property(x => x.SuspendedReason).HasMaxLength(500);
            e.HasIndex(x => x.Slug).IsUnique();
        });

        // ── Usuarios y dispositivos ──────────────────────────────────────────────
        b.Entity<User>(e =>
        {
            // La dirección es única dentro de la organización, no en toda la instalación: dos
            // empresas distintas pueden tener a la misma persona, y son dos cuentas.
            e.HasIndex(x => new { x.OrganizationId, x.Email }).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.TimeZoneId).HasMaxLength(100);
        });

        b.Entity<DeviceRegistration>(e =>
        {
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Token).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.User).WithMany(u => u.Devices)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);

            // El scheduler pregunta "¿este usuario tiene algún dispositivo vivo?" en cada
            // disparo de check-in; sin este índice sería un scan por usuario.
            e.HasIndex(x => new { x.UserId, x.LastHeartbeatAt });
        });

        b.Entity<ExternalIdentity>(e =>
        {
            e.Property(x => x.Issuer).HasMaxLength(300).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            e.Property(x => x.Email).HasMaxLength(320);

            // La clave real del login externo, única dentro de la organización: adentro de una
            // empresa, la misma cuenta del proveedor no puede quedar vinculada a dos personas —si
            // eso pasara, cuál entra dependería del orden de las filas—. Entre organizaciones sí
            // se repite, porque una cuenta por organización significa que la misma dirección de
            // Google puede tener un usuario en cada una.
            e.HasIndex(x => new { x.OrganizationId, x.Issuer, x.Subject }).IsUnique();

            // Por acá entra el login: buscar la cuenta del proveedor sin saber todavía a qué
            // organización pertenece.
            e.HasIndex(x => new { x.Issuer, x.Subject });

            e.HasOne(x => x.User).WithMany(u => u.ExternalIdentities)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<OidcLoginAttempt>(e =>
        {
            e.Property(x => x.State).HasMaxLength(64).IsRequired();
            e.Property(x => x.CodeVerifier).HasMaxLength(128).IsRequired();
            e.Property(x => x.Nonce).HasMaxLength(64).IsRequired();
            e.Property(x => x.ReturnPath).HasMaxLength(300);
            e.Property(x => x.Ticket).HasMaxLength(64);

            e.HasIndex(x => x.State).IsUnique();
            e.HasIndex(x => x.Ticket).IsUnique().HasFilter("\"Ticket\" IS NOT NULL");

            // Por dónde entra el barrido que borra los intentos viejos.
            e.HasIndex(x => x.CreatedAt);

            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PendingRegistration>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.OrganizationName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Token).HasMaxLength(64).IsRequired();

            e.HasIndex(x => x.Token).IsUnique();

            // Por acá entra el reintento —«me registré de nuevo»— y el barrido de las vencidas.
            e.HasIndex(x => new { x.Email, x.CreatedAt });
        });

        // ── Plantillas ───────────────────────────────────────────────────────────
        b.Entity<ProjectTemplate>(e =>
        {
            e.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
            e.Property(x => x.Key).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();

            // Etapas y campos de la plantilla van como jsonb: son un documento que se clona al
            // crear el proyecto, nunca se consultan de a uno.
            e.OwnsMany(x => x.Stages, s => s.ToJson());
            e.OwnsMany(x => x.Fields, f => f.ToJson());
        });

        // ── Proyectos y workflow ─────────────────────────────────────────────────
        b.Entity<Project>(e =>
        {
            // Por organización: que una empresa ya tenga un proyecto «DEV» no puede impedirle a
            // otra llamar «DEV» al suyo.
            e.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
            e.Property(x => x.Key).HasMaxLength(10).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();

            // Único y filtrado: solo los proyectos con intake habilitado tienen token, y dos
            // proyectos no pueden compartirlo. La búsqueda pública entra por acá.
            e.HasIndex(x => x.IntakeToken).IsUnique().HasFilter("\"IntakeToken\" IS NOT NULL");
            e.Property(x => x.IntakeToken).HasMaxLength(64);

            e.HasOne(x => x.Template).WithMany()
                .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Workflow).WithMany()
                .HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ArchivedBy).WithMany()
                .HasForeignKey(x => x.ArchivedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ProjectMember>(e =>
        {
            e.HasKey(x => new { x.ProjectId, x.UserId });
            e.HasOne(x => x.Project).WithMany(p => p.Members)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PendingHandoff>(e =>
        {
            e.Property(x => x.ItemKey).HasMaxLength(40).IsRequired();
            e.Property(x => x.StageName).HasMaxLength(100).IsRequired();

            // El barrido busca lo no avisado y lo agrupa por persona: ese es el índice.
            e.HasIndex(x => new { x.NotifiedAt, x.UserId });

            // Si la persona se va, sus avisos pendientes se van con ella: no hay a quién avisarle.
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkflowStage>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasOne(x => x.Workflow).WithMany(w => w.Stages)
                .HasForeignKey(x => x.WorkflowId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.WorkflowId, x.Order });

            // SetNull y no Restrict: si se borra a la persona, la etapa deja de tener responsable
            // y el trabajo que caiga ahí queda sin asignar —visible— en vez de impedir el borrado
            // del usuario o, peor, seguir apuntando a alguien que ya no está.
            e.HasOne(x => x.DefaultAssignee).WithMany()
                .HasForeignKey(x => x.DefaultAssigneeId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<WorkflowTransition>(e =>
        {
            e.HasIndex(x => new { x.FromStageId, x.ToStageId }).IsUnique();

            // Dos FKs a la misma tabla: cascada en ambas daría múltiples caminos de borrado,
            // que Postgres acepta pero deja el grafo ambiguo. Se borran con el workflow.
            e.HasOne(x => x.FromStage).WithMany(s => s.AllowedTransitions)
                .HasForeignKey(x => x.FromStageId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ToStage).WithMany()
                .HasForeignKey(x => x.ToStageId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CustomFieldDef>(e =>
        {
            e.Property(x => x.Key).HasMaxLength(60).IsRequired();
            e.Property(x => x.Label).HasMaxLength(120).IsRequired();
            e.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
            e.HasOne(x => x.Project).WithMany(p => p.CustomFields)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Work items ───────────────────────────────────────────────────────────
        b.Entity<WorkItem>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Type).HasMaxLength(60);
            e.Property(x => x.Estimate).HasPrecision(10, 2);
            e.Property(x => x.SubmitterName).HasMaxLength(200);
            e.Property(x => x.SubmitterEmail).HasMaxLength(320);

            e.HasIndex(x => new { x.ProjectId, x.Number }).IsUnique();

            // La consulta más caliente del producto: "el trabajo de esta persona, sin cerrar".
            // El agente la hace en cada check-in.
            e.HasIndex(x => new { x.AssigneeId, x.ClosedAt });
            e.HasIndex(x => new { x.ProjectId, x.StageId, x.SortOrder });
            e.HasIndex(x => x.DueDate);

            // jsonb + GIN: campos personalizados arbitrarios por proyecto, filtrables sin
            // migraciones y sin escaneo secuencial.
            e.Property(x => x.CustomFields).HasColumnType("jsonb");
            e.HasIndex(x => x.CustomFields).HasMethod("gin");

            e.HasOne(x => x.Project).WithMany(p => p.Items)
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Stage).WithMany()
                .HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Assignee).WithMany()
                .HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Reporter).WithMany()
                .HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<Label>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => new { x.ProjectId, x.Name }).IsUnique();
            e.HasOne(x => x.Project).WithMany()
                .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkItemLabel>(e =>
        {
            e.HasKey(x => new { x.WorkItemId, x.LabelId });
            e.HasOne(x => x.WorkItem).WithMany(w => w.Labels)
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Label).WithMany()
                .HasForeignKey(x => x.LabelId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkItemComment>(e =>
        {
            e.HasOne(x => x.WorkItem).WithMany(w => w.Comments)
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Author).WithMany()
                .HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.WorkItemId, x.CreatedAt });
        });

        b.Entity<WorkItemDependency>(e =>
        {
            e.HasIndex(x => new { x.BlockedItemId, x.BlockingItemId }).IsUnique();
            e.HasOne(x => x.BlockedItem).WithMany()
                .HasForeignKey(x => x.BlockedItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.BlockingItem).WithMany()
                .HasForeignKey(x => x.BlockingItemId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<WorkItemEvent>(e =>
        {
            e.Property(x => x.Field).HasMaxLength(80).IsRequired();
            e.HasOne(x => x.WorkItem).WithMany(w => w.Events)
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CheckIn).WithMany()
                .HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.SetNull);

            e.HasIndex(x => new { x.WorkItemId, x.CreatedAt });
            // "Mostrame todo lo que tocó la IA" — la consulta de auditoría del manager.
            e.HasIndex(x => new { x.ActorType, x.CreatedAt });
        });

        b.Entity<Blocker>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne(x => x.WorkItem).WithMany(w => w.Blockers)
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.BlockedByUser).WithMany()
                .HasForeignKey(x => x.BlockedByUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.WorkItemId, x.ResolvedAt });
        });

        b.Entity<RepoLink>(e =>
        {
            e.Property(x => x.ExternalId).HasMaxLength(200).IsRequired();
            e.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            e.HasIndex(x => new { x.WorkItemId, x.Kind, x.ExternalId }).IsUnique();
            e.HasOne(x => x.WorkItem).WithMany(w => w.RepoLinks)
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Entregables y revisión ───────────────────────────────────────────────
        b.Entity<Deliverable>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.WorkItem).WithMany(w => w.Deliverables)
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DeliverableVersion>(e =>
        {
            e.HasIndex(x => new { x.DeliverableId, x.Version }).IsUnique();
            e.HasOne(x => x.Deliverable).WithMany(d => d.Versions)
                .HasForeignKey(x => x.DeliverableId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.UploadedBy).WithMany()
                .HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ReviewRound>(e =>
        {
            e.HasOne(x => x.WorkItem).WithMany()
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.DeliverableVersion).WithMany(v => v.ReviewRounds)
                .HasForeignKey(x => x.DeliverableVersionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Reviewer).WithMany()
                .HasForeignKey(x => x.ReviewerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.WorkItemId, x.OpenedAt });
        });

        // ── Check-ins y agente ───────────────────────────────────────────────────
        b.Entity<CheckIn>(e =>
        {
            e.Property(x => x.Transcript).HasColumnType("jsonb");

            // Un check-in por persona por día laboral: hace idempotente al scheduler.
            e.HasIndex(x => new { x.UserId, x.LocalDate }).IsUnique();

            // El barrido de la escalera solo mira filas con el próximo peldaño vencido.
            e.HasIndex(x => new { x.Status, x.NextEscalationAt });

            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<NotificationAttempt>(e =>
        {
            e.HasOne(x => x.CheckIn).WithMany(c => c.Attempts)
                .HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Notification).WithMany()
                .HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.CheckInId, x.Step });
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(60).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt });
        });

        b.Entity<AgentAction>(e =>
        {
            e.Property(x => x.ToolName).HasMaxLength(80).IsRequired();
            e.Property(x => x.Arguments).HasColumnType("jsonb");

            e.HasOne(x => x.CheckIn).WithMany(c => c.Actions)
                .HasForeignKey(x => x.CheckInId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.WorkItem).WithMany()
                .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.ApprovedBy).WithMany()
                .HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.SetNull);

            // La cola de aprobaciones del manager.
            e.HasIndex(x => new { x.Status, x.CreatedAt });
        });

        // ── Mensajes directos ────────────────────────────────────────────────────
        b.Entity<DirectMessage>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();

            e.HasOne(x => x.FromUser).WithMany()
                .HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.Cascade);

            // Restrict del lado del destinatario: con cascada en las dos puntas, Postgres queda
            // con múltiples caminos de borrado para la misma fila.
            e.HasOne(x => x.ToUser).WithMany()
                .HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.Restrict);

            // La consulta de la bandeja: «lo que no leí», y el hilo con una persona.
            e.HasIndex(x => new { x.ToUserId, x.ReadAt });
            e.HasIndex(x => new { x.ToUserId, x.FromUserId, x.CreatedAt });
        });

        // ── Configuración editable ───────────────────────────────────────────────
        b.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(120);
            e.Property(x => x.Value).HasMaxLength(4000).IsRequired();

            e.HasOne(x => x.UpdatedBy).WithMany()
                .HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<OrganizationSetting>(e =>
        {
            // La clave incluye la organización: la misma «AgentModel:Model» convive una vez por
            // empresa, y esa es toda la diferencia con la tabla de al lado.
            e.HasKey(x => new { x.OrganizationId, x.Key });
            e.Property(x => x.Key).HasMaxLength(120);
            e.Property(x => x.Value).HasMaxLength(4000).IsRequired();

            e.HasOne(x => x.UpdatedBy).WithMany()
                .HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
        });

        // ── Aislamiento entre organizaciones ─────────────────────────────────────
        // El filtro se aplica a todo lo que implemente IOrganizationScoped, en un bucle y no
        // entidad por entidad: agregar una tabla nueva y olvidarse de filtrarla sería una fuga
        // silenciosa entre empresas, y este bucle hace que olvidarse no sea posible.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            if (!typeof(IOrganizationScoped).IsAssignableFrom(entity.ClrType)) continue;
            if (entity.BaseType is not null) continue;

            b.Entity(entity.ClrType).HasQueryFilter(OrganizationFilter(entity.ClrType));

            // El filtro entra por esta columna en cada consulta de cada tabla; sin índice, cada
            // lectura arranca con un scan.
            b.Entity(entity.ClrType)
                .HasIndex(nameof(IOrganizationScoped.OrganizationId));

            b.Entity(entity.ClrType)
                .HasOne(typeof(Organization))
                .WithMany()
                .HasForeignKey(nameof(IOrganizationScoped.OrganizationId))
                // Restrict y no Cascade: borrar una organización por accidente no puede
                // llevarse el trabajo de una empresa entera en una sentencia. Para eso está
                // suspenderla, que es reversible.
                .OnDelete(DeleteBehavior.Restrict);
        }

        // Todos los enums como texto: si mañana se reordena un enum, los datos existentes
        // siguen significando lo mismo, y las filas se leen sin diccionario.
        foreach (var entity in b.Model.GetEntityTypes())
        {
            foreach (var prop in entity.GetProperties())
            {
                var clr = prop.ClrType;
                var underlying = Nullable.GetUnderlyingType(clr);
                if (!(underlying ?? clr).IsEnum) continue;

                var converterType = typeof(EnumToStringConverter<>).MakeGenericType(underlying ?? clr);
                prop.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                prop.SetMaxLength(40);
            }
        }
    }

    /// <summary>La organización activa, leída a través del contexto. Delega en el ambiente: la
    /// propiedad es de instancia solo para el filtro (ver <see cref="OrganizationFilter"/>), y su
    /// valor no depende de qué instancia sea.</summary>
    public Guid CurrentOrganizationId => OrganizationScope.CurrentId;

    public bool OrganizationFilterEnabled => OrganizationScope.FilterEnabled;

    /// <summary>`e => !contexto.OrganizationFilterEnabled || e.OrganizationId == contexto.CurrentOrganizationId`,
    /// armado a mano porque el tipo de la entidad solo se conoce en tiempo de ejecución.
    ///
    /// Las dos lecturas pasan sí o sí por una propiedad **de instancia** del contexto, y eso no es
    /// un detalle de estilo. Con accesos estáticos, EF considera que la expresión es constante:
    /// la evalúa una vez, la incrusta en el SQL y cachea esa consulta —lo que en la práctica
    /// significa que la segunda organización recibe el SQL de la primera y ve sus datos—. Es un
    /// fallo silencioso, y se comprobó que pasa. Con una propiedad del contexto, EF la extrae como
    /// parámetro de filtro y la vuelve a leer en cada ejecución.
    ///
    /// Que además deleguen en el ambiente (<see cref="OrganizationScope"/>, un AsyncLocal
    /// estático) resuelve la otra mitad del problema: el modelo se construye una sola vez y la
    /// expresión queda atada a la instancia que existía entonces, así que el valor no puede vivir
    /// en el estado de esa instancia.</summary>
    private LambdaExpression OrganizationFilter(Type clrType)
    {
        var entity = Expression.Parameter(clrType, "e");
        var context = Expression.Constant(this);

        var body = Expression.OrElse(
            Expression.Not(Expression.Property(context, nameof(OrganizationFilterEnabled))),
            Expression.Equal(
                Expression.Property(entity, nameof(IOrganizationScoped.OrganizationId)),
                Expression.Property(context, nameof(CurrentOrganizationId))));

        return Expression.Lambda(body, entity);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOrganization();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampOrganization();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Completa la organización de lo que se está creando y vigila que nadie la cambie.
    ///
    /// La alternativa —que cada servicio se acuerde de asignarla— funciona hasta el primer
    /// olvido, y ese olvido escribe una fila que no le pertenece a nadie: invisible para todas
    /// las organizaciones, imposible de encontrar salvo por la fuga que causa. Por eso acá se
    /// prefiere fallar la escritura antes que guardarla sin dueño.</summary>
    private void StampOrganization()
    {
        var current = OrganizationScope.Organization;

        foreach (var entry in ChangeTracker.Entries<IOrganizationScoped>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.OrganizationId == Guid.Empty)
                {
                    entry.Entity.OrganizationId = current ?? throw new InvalidOperationException(
                        $"Se intentó crear {entry.Entity.GetType().Name} sin organización. " +
                        "Dentro de un request la pone el middleware; en un proceso de fondo hay " +
                        "que asignar OrganizationId explícitamente o envolver el trabajo en " +
                        "OrganizationScope.Use(...).");
                }
                else if (current is not null && entry.Entity.OrganizationId != current)
                {
                    throw new InvalidOperationException(
                        $"Se intentó crear {entry.Entity.GetType().Name} en la organización " +
                        $"{entry.Entity.OrganizationId} desde el contexto de {current}.");
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                // Mover una fila de organización no es una operación del producto. Si alguna vez
                // hace falta (una fusión de empresas), será un procedimiento explícito y no el
                // efecto secundario de un mapeo mal escrito.
                var property = entry.Property(x => x.OrganizationId);
                if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                {
                    throw new InvalidOperationException(
                        $"Se intentó mover {entry.Entity.GetType().Name} de la organización " +
                        $"{property.OriginalValue} a {property.CurrentValue}.");
                }
            }
        }
    }
}
