using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IntakeForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmitterEmail",
                table: "WorkItems",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmitterName",
                table: "WorkItems",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IntakeEnabled",
                table: "Projects",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IntakeInstructions",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntakeToken",
                table: "Projects",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_IntakeToken",
                table: "Projects",
                column: "IntakeToken",
                unique: true,
                filter: "\"IntakeToken\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Projects_IntakeToken",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SubmitterEmail",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "SubmitterName",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "IntakeEnabled",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IntakeInstructions",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IntakeToken",
                table: "Projects");
        }
    }
}
