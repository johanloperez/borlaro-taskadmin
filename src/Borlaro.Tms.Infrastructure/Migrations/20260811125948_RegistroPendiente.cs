using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistroPendiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PendingEmail",
                table: "OidcLoginAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingIssuer",
                table: "OidcLoginAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingName",
                table: "OidcLoginAttempts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingSubject",
                table: "OidcLoginAttempts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingEmail",
                table: "OidcLoginAttempts");

            migrationBuilder.DropColumn(
                name: "PendingIssuer",
                table: "OidcLoginAttempts");

            migrationBuilder.DropColumn(
                name: "PendingName",
                table: "OidcLoginAttempts");

            migrationBuilder.DropColumn(
                name: "PendingSubject",
                table: "OidcLoginAttempts");
        }
    }
}
