using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiOrganization : Migration
    {
        /// <summary>La organización a la que se traspasa todo lo que ya existía. Es un literal
        /// fijo y no un GUID nuevo en cada ejecución: la migración tiene que dar el mismo
        /// resultado en la máquina de desarrollo y en el servidor.
        ///
        /// No es el GUID vacío a propósito: para el filtro de organización, vacío significa
        /// «ninguna», y una organización real con ese Id sería visible desde cualquier contexto
        /// sin sesión.</summary>
        private const string PrimeraOrganizacion = "9f6e0b4c-1c3a-4b7e-9d21-000000000001";

        private static readonly string[] TablasConOrganizacion =
        [
            "AgentActions", "Blockers", "CheckIns", "CustomFieldDefs", "DeliverableVersions",
            "Deliverables", "DeviceRegistrations", "DirectMessages", "ExternalIdentities",
            "Labels", "NotificationAttempts", "Notifications", "ProjectMembers",
            "ProjectTemplates", "Projects", "RepoLinks", "ReviewRounds", "Users",
            "WorkItemComments", "WorkItemDependencies", "WorkItemEvents", "WorkItemLabels",
            "WorkItems", "WorkflowStages", "WorkflowTransitions", "Workflows"
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTemplates_Key",
                table: "ProjectTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Projects_Key",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ExternalIdentities_Issuer_Subject",
                table: "ExternalIdentities");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkItemLabels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkItemEvents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkItemDependencies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkItemComments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkflowTransitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "WorkflowStages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Workflows",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ReviewRounds",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "RepoLinks",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ProjectTemplates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ProjectMembers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "NotificationAttempts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Labels",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "ExternalIdentities",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DirectMessages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DeviceRegistrations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "DeliverableVersions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Deliverables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CustomFieldDefs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "CheckIns",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "Blockers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganizationId",
                table: "AgentActions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    IsSuspended = table.Column<bool>(type: "boolean", nullable: false),
                    SuspendedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SuspendedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            // ── Traspaso de lo que ya existía ────────────────────────────────────
            // Las columnas nuevas nacen en el GUID vacío, que para el filtro significa «ninguna
            // organización»: sin este paso, una instalación en uso quedaría con todos sus datos
            // invisibles. Va acá, entre crear la tabla y crear las claves foráneas, que es la
            // única ventana donde las filas ya tienen dónde apuntar y todavía nadie lo exige.
            //
            // Solo corre si había usuarios. En una base vacía no hay nada que traspasar, y la
            // organización la crea el arranque con el nombre configurado en Bootstrap.
            migrationBuilder.Sql($"""
                INSERT INTO "Organizations" ("Id", "Name", "Slug", "IsSuspended", "CreatedAt")
                SELECT '{PrimeraOrganizacion}', 'Organización principal', 'principal', false, now()
                WHERE EXISTS (SELECT 1 FROM "Users");
                """);

            foreach (var tabla in TablasConOrganizacion)
            {
                migrationBuilder.Sql($"""
                    UPDATE "{tabla}"
                    SET "OrganizationId" = '{PrimeraOrganizacion}'
                    WHERE "OrganizationId" = '00000000-0000-0000-0000-000000000000';
                    """);
            }

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_OrganizationId",
                table: "WorkItems",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLabels_OrganizationId",
                table: "WorkItemLabels",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemEvents_OrganizationId",
                table: "WorkItemEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemDependencies_OrganizationId",
                table: "WorkItemDependencies",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemComments_OrganizationId",
                table: "WorkItemComments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowTransitions_OrganizationId",
                table: "WorkflowTransitions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStages_OrganizationId",
                table: "WorkflowStages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Workflows_OrganizationId",
                table: "Workflows",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId",
                table: "Users",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_OrganizationId_Email",
                table: "Users",
                columns: new[] { "OrganizationId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewRounds_OrganizationId",
                table: "ReviewRounds",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_RepoLinks_OrganizationId",
                table: "RepoLinks",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_OrganizationId",
                table: "ProjectTemplates",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_OrganizationId_Key",
                table: "ProjectTemplates",
                columns: new[] { "OrganizationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_Key",
                table: "Projects",
                columns: new[] { "OrganizationId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMembers_OrganizationId",
                table: "ProjectMembers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OrganizationId",
                table: "Notifications",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationAttempts_OrganizationId",
                table: "NotificationAttempts",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Labels_OrganizationId",
                table: "Labels",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_Issuer_Subject",
                table: "ExternalIdentities",
                columns: new[] { "Issuer", "Subject" });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_OrganizationId",
                table: "ExternalIdentities",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_OrganizationId_Issuer_Subject",
                table: "ExternalIdentities",
                columns: new[] { "OrganizationId", "Issuer", "Subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DirectMessages_OrganizationId",
                table: "DirectMessages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceRegistrations_OrganizationId",
                table: "DeviceRegistrations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_DeliverableVersions_OrganizationId",
                table: "DeliverableVersions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliverables_OrganizationId",
                table: "Deliverables",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefs_OrganizationId",
                table: "CustomFieldDefs",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_CheckIns_OrganizationId",
                table: "CheckIns",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Blockers_OrganizationId",
                table: "Blockers",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentActions_OrganizationId",
                table: "AgentActions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Slug",
                table: "Organizations",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AgentActions_Organizations_OrganizationId",
                table: "AgentActions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Blockers_Organizations_OrganizationId",
                table: "Blockers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CheckIns_Organizations_OrganizationId",
                table: "CheckIns",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CustomFieldDefs_Organizations_OrganizationId",
                table: "CustomFieldDefs",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliverables_Organizations_OrganizationId",
                table: "Deliverables",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliverableVersions_Organizations_OrganizationId",
                table: "DeliverableVersions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeviceRegistrations_Organizations_OrganizationId",
                table: "DeviceRegistrations",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DirectMessages_Organizations_OrganizationId",
                table: "DirectMessages",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExternalIdentities_Organizations_OrganizationId",
                table: "ExternalIdentities",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Labels_Organizations_OrganizationId",
                table: "Labels",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationAttempts_Organizations_OrganizationId",
                table: "NotificationAttempts",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Organizations_OrganizationId",
                table: "Notifications",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectMembers_Organizations_OrganizationId",
                table: "ProjectMembers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectTemplates_Organizations_OrganizationId",
                table: "ProjectTemplates",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RepoLinks_Organizations_OrganizationId",
                table: "RepoLinks",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReviewRounds_Organizations_OrganizationId",
                table: "ReviewRounds",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Workflows_Organizations_OrganizationId",
                table: "Workflows",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStages_Organizations_OrganizationId",
                table: "WorkflowStages",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowTransitions_Organizations_OrganizationId",
                table: "WorkflowTransitions",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemComments_Organizations_OrganizationId",
                table: "WorkItemComments",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemDependencies_Organizations_OrganizationId",
                table: "WorkItemDependencies",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemEvents_Organizations_OrganizationId",
                table: "WorkItemEvents",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemLabels_Organizations_OrganizationId",
                table: "WorkItemLabels",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Organizations_OrganizationId",
                table: "WorkItems",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgentActions_Organizations_OrganizationId",
                table: "AgentActions");

            migrationBuilder.DropForeignKey(
                name: "FK_Blockers_Organizations_OrganizationId",
                table: "Blockers");

            migrationBuilder.DropForeignKey(
                name: "FK_CheckIns_Organizations_OrganizationId",
                table: "CheckIns");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomFieldDefs_Organizations_OrganizationId",
                table: "CustomFieldDefs");

            migrationBuilder.DropForeignKey(
                name: "FK_Deliverables_Organizations_OrganizationId",
                table: "Deliverables");

            migrationBuilder.DropForeignKey(
                name: "FK_DeliverableVersions_Organizations_OrganizationId",
                table: "DeliverableVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_DeviceRegistrations_Organizations_OrganizationId",
                table: "DeviceRegistrations");

            migrationBuilder.DropForeignKey(
                name: "FK_DirectMessages_Organizations_OrganizationId",
                table: "DirectMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ExternalIdentities_Organizations_OrganizationId",
                table: "ExternalIdentities");

            migrationBuilder.DropForeignKey(
                name: "FK_Labels_Organizations_OrganizationId",
                table: "Labels");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationAttempts_Organizations_OrganizationId",
                table: "NotificationAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Organizations_OrganizationId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectMembers_Organizations_OrganizationId",
                table: "ProjectMembers");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectTemplates_Organizations_OrganizationId",
                table: "ProjectTemplates");

            migrationBuilder.DropForeignKey(
                name: "FK_RepoLinks_Organizations_OrganizationId",
                table: "RepoLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_ReviewRounds_Organizations_OrganizationId",
                table: "ReviewRounds");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Organizations_OrganizationId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Workflows_Organizations_OrganizationId",
                table: "Workflows");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStages_Organizations_OrganizationId",
                table: "WorkflowStages");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowTransitions_Organizations_OrganizationId",
                table: "WorkflowTransitions");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemComments_Organizations_OrganizationId",
                table: "WorkItemComments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemDependencies_Organizations_OrganizationId",
                table: "WorkItemDependencies");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemEvents_Organizations_OrganizationId",
                table: "WorkItemEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemLabels_Organizations_OrganizationId",
                table: "WorkItemLabels");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Organizations_OrganizationId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_OrganizationId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemLabels_OrganizationId",
                table: "WorkItemLabels");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemEvents_OrganizationId",
                table: "WorkItemEvents");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemDependencies_OrganizationId",
                table: "WorkItemDependencies");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemComments_OrganizationId",
                table: "WorkItemComments");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowTransitions_OrganizationId",
                table: "WorkflowTransitions");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStages_OrganizationId",
                table: "WorkflowStages");

            migrationBuilder.DropIndex(
                name: "IX_Workflows_OrganizationId",
                table: "Workflows");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_OrganizationId_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_ReviewRounds_OrganizationId",
                table: "ReviewRounds");

            migrationBuilder.DropIndex(
                name: "IX_RepoLinks_OrganizationId",
                table: "RepoLinks");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTemplates_OrganizationId",
                table: "ProjectTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ProjectTemplates_OrganizationId_Key",
                table: "ProjectTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId_Key",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectMembers_OrganizationId",
                table: "ProjectMembers");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OrganizationId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_NotificationAttempts_OrganizationId",
                table: "NotificationAttempts");

            migrationBuilder.DropIndex(
                name: "IX_Labels_OrganizationId",
                table: "Labels");

            migrationBuilder.DropIndex(
                name: "IX_ExternalIdentities_Issuer_Subject",
                table: "ExternalIdentities");

            migrationBuilder.DropIndex(
                name: "IX_ExternalIdentities_OrganizationId",
                table: "ExternalIdentities");

            migrationBuilder.DropIndex(
                name: "IX_ExternalIdentities_OrganizationId_Issuer_Subject",
                table: "ExternalIdentities");

            migrationBuilder.DropIndex(
                name: "IX_DirectMessages_OrganizationId",
                table: "DirectMessages");

            migrationBuilder.DropIndex(
                name: "IX_DeviceRegistrations_OrganizationId",
                table: "DeviceRegistrations");

            migrationBuilder.DropIndex(
                name: "IX_DeliverableVersions_OrganizationId",
                table: "DeliverableVersions");

            migrationBuilder.DropIndex(
                name: "IX_Deliverables_OrganizationId",
                table: "Deliverables");

            migrationBuilder.DropIndex(
                name: "IX_CustomFieldDefs_OrganizationId",
                table: "CustomFieldDefs");

            migrationBuilder.DropIndex(
                name: "IX_CheckIns_OrganizationId",
                table: "CheckIns");

            migrationBuilder.DropIndex(
                name: "IX_Blockers_OrganizationId",
                table: "Blockers");

            migrationBuilder.DropIndex(
                name: "IX_AgentActions_OrganizationId",
                table: "AgentActions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkItemLabels");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkItemEvents");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkItemDependencies");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkItemComments");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkflowTransitions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Workflows");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ReviewRounds");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "RepoLinks");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ProjectTemplates");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ProjectMembers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "NotificationAttempts");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Labels");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "ExternalIdentities");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DirectMessages");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DeviceRegistrations");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "DeliverableVersions");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Deliverables");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CustomFieldDefs");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "CheckIns");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "Blockers");

            migrationBuilder.DropColumn(
                name: "OrganizationId",
                table: "AgentActions");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTemplates_Key",
                table: "ProjectTemplates",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Key",
                table: "Projects",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIdentities_Issuer_Subject",
                table: "ExternalIdentities",
                columns: new[] { "Issuer", "Subject" },
                unique: true);
        }
    }
}
