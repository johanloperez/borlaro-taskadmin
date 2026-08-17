using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdjuntosDeTarea : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkItemAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_Users_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "Users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAttachments_OrganizationId",
                table: "WorkItemAttachments",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAttachments_UploadedById",
                table: "WorkItemAttachments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAttachments_WorkItemId",
                table: "WorkItemAttachments",
                column: "WorkItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkItemAttachments");
        }
    }
}
