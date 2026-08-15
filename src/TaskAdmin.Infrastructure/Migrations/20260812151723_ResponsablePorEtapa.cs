using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResponsablePorEtapa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DefaultAssigneeId",
                table: "WorkflowStages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowStages_DefaultAssigneeId",
                table: "WorkflowStages",
                column: "DefaultAssigneeId");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkflowStages_Users_DefaultAssigneeId",
                table: "WorkflowStages",
                column: "DefaultAssigneeId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkflowStages_Users_DefaultAssigneeId",
                table: "WorkflowStages");

            migrationBuilder.DropIndex(
                name: "IX_WorkflowStages_DefaultAssigneeId",
                table: "WorkflowStages");

            migrationBuilder.DropColumn(
                name: "DefaultAssigneeId",
                table: "WorkflowStages");
        }
    }
}
