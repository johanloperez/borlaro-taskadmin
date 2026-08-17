using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AnulacionDeAmpliacionYAvisoDeVencimiento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "WorkItemTimeExtensions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "VoidedAt",
                table: "WorkItemTimeExtensions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedById",
                table: "WorkItemTimeExtensions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OverdueNotifiedAt",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemTimeExtensions_VoidedById",
                table: "WorkItemTimeExtensions",
                column: "VoidedById");

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItemTimeExtensions_Users_VoidedById",
                table: "WorkItemTimeExtensions",
                column: "VoidedById",
                principalTable: "Users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_WorkItemTimeExtensions_Users_VoidedById",
                table: "WorkItemTimeExtensions");

            migrationBuilder.DropIndex(
                name: "IX_WorkItemTimeExtensions_VoidedById",
                table: "WorkItemTimeExtensions");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "WorkItemTimeExtensions");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "WorkItemTimeExtensions");

            migrationBuilder.DropColumn(
                name: "VoidedById",
                table: "WorkItemTimeExtensions");

            migrationBuilder.DropColumn(
                name: "OverdueNotifiedAt",
                table: "WorkItems");
        }
    }
}
