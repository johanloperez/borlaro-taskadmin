using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IdentidadDeSlack : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SlackUserId",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlackUserId",
                table: "Users");
        }
    }
}
