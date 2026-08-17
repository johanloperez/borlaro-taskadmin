using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TiempoDificultadYNovedades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AddedHours",
                table: "WorkItems",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "WorkItems",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkItemTimeExtensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Hours = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    CheckInId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemTimeExtensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemTimeExtensions_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemTimeExtensions_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTimeExtensions_OrganizationId",
                table: "WorkItemTimeExtensions",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTimeExtensions_WorkItemId_CreatedAt",
                table: "WorkItemTimeExtensions",
                columns: new[] { "WorkItemId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemTimeExtensions");

            migrationBuilder.DropColumn(
                name: "AddedHours",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "WorkItems");
        }
    }
}
