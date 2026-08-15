using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskAdmin.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AssigneePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AssigneeCanDelete",
                table: "WorkItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AssigneeCanEdit",
                table: "WorkItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AssigneeCanMove",
                table: "WorkItems",
                type: "boolean",
                nullable: false,
                // Verdadero: mover la tarea propia es lo que ya podian hacer todas las tareas
                // que existian, y una migracion no puede quitar permisos en silencio.
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssigneeCanDelete",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeCanEdit",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "AssigneeCanMove",
                table: "WorkItems");
        }
    }
}
