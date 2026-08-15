using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformOperator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPlatform",
                table: "Organizations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPlatform",
                table: "Organizations");
        }
    }
}
