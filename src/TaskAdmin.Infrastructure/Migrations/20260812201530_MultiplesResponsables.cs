using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiplesResponsables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Agregar columnas a Users
            migrationBuilder.AddColumn<bool>(
                name: "CanAssignTasks",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Agregar columnas a WorkItems
            migrationBuilder.AddColumn<Guid>(
                name: "PreviousAssigneeId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PreviousStageId",
                table: "WorkItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AssignedBySystem",
                table: "WorkItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Crear tabla StageResponsible
            migrationBuilder.CreateTable(
                name: "StageResponsibles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StageResponsibles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StageResponsibles_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StageResponsibles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StageResponsibles_WorkflowStages_StageId",
                        column: x => x.StageId,
                        principalTable: "WorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Crear tabla WorkItemStageAssignment
            migrationBuilder.CreateTable(
                name: "WorkItemStageAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemStageAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemStageAssignments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemStageAssignments_Users_AssignedUserId",
                        column: x => x.AssignedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkItemStageAssignments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemStageAssignments_WorkflowStages_StageId",
                        column: x => x.StageId,
                        principalTable: "WorkflowStages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Agregar FK para PreviousAssigneeId en WorkItems
            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Users_PreviousAssigneeId",
                table: "WorkItems",
                column: "PreviousAssigneeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_WorkflowStages_PreviousStageId",
                table: "WorkItems",
                column: "PreviousStageId",
                principalTable: "WorkflowStages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Crear índices para performance
            migrationBuilder.CreateIndex(
                name: "IX_StageResponsibles_StageId_UserId",
                table: "StageResponsibles",
                columns: new[] { "StageId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStageAssignments_WorkItemId_StageId",
                table: "WorkItemStageAssignments",
                columns: new[] { "WorkItemId", "StageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_PreviousAssigneeId",
                table: "WorkItems",
                column: "PreviousAssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_PreviousStageId",
                table: "WorkItems",
                column: "PreviousStageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StageResponsibles_Organizations_OrganizationId",
                table: "StageResponsibles");

            migrationBuilder.DropForeignKey(
                name: "FK_StageResponsibles_Users_UserId",
                table: "StageResponsibles");

            migrationBuilder.DropForeignKey(
                name: "FK_StageResponsibles_WorkflowStages_StageId",
                table: "StageResponsibles");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemStageAssignments_Organizations_OrganizationId",
                table: "WorkItemStageAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemStageAssignments_Users_AssignedUserId",
                table: "WorkItemStageAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemStageAssignments_WorkItems_WorkItemId",
                table: "WorkItemStageAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemStageAssignments_WorkflowStages_StageId",
                table: "WorkItemStageAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Users_PreviousAssigneeId",
                table: "WorkItems");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_WorkflowStages_PreviousStageId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "StageResponsibles");

            migrationBuilder.DropTable(
                name: "WorkItemStageAssignments");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_PreviousAssigneeId",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_PreviousStageId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "PreviousAssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "PreviousStageId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssignedBySystem",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "CanAssignTasks",
                table: "Users");
        }
    }
}
