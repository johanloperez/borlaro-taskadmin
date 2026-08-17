using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MultiplesResponsables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AssignedBySystem",
                table: "WorkItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
                name: "CanAssignTasks",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

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
                        principalColumn: "Id");
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

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_PreviousAssigneeId",
                table: "WorkItems",
                column: "PreviousAssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_StageResponsibles_OrganizationId",
                table: "StageResponsibles",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_StageResponsibles_StageId",
                table: "StageResponsibles",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_StageResponsibles_UserId",
                table: "StageResponsibles",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStageAssignments_AssignedUserId",
                table: "WorkItemStageAssignments",
                column: "AssignedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStageAssignments_OrganizationId",
                table: "WorkItemStageAssignments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStageAssignments_StageId",
                table: "WorkItemStageAssignments",
                column: "StageId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStageAssignments_WorkItemId",
                table: "WorkItemStageAssignments",
                column: "WorkItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Users_PreviousAssigneeId",
                table: "WorkItems",
                column: "PreviousAssigneeId",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Users_PreviousAssigneeId",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "StageResponsibles");

            migrationBuilder.DropTable(
                name: "WorkItemStageAssignments");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_PreviousAssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssignedBySystem",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "PreviousAssigneeId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "PreviousStageId",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "CanAssignTasks",
                table: "Users");
        }
    }
}
