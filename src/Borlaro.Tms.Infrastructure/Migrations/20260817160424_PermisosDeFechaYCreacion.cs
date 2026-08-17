using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <summary>Los permisos por persona de fijar fechas y de crear tareas.
    ///
    /// Los dos entran **encendidos**, y para las filas que ya existen también: son restricciones
    /// que alguien aplica a mano, no permisos que haya que conceder de a uno. Naciendo apagados le
    /// quitarían de golpe a todo el equipo dos cosas que hoy puede hacer, sin ninguna pantalla
    /// donde devolvérselas. Ya pasó una vez con `CanAssignTasks` —ver `PermisoAsignarPorDefecto`—
    /// y es el tipo de error que conviene cometer una sola vez.</summary>
    public partial class PermisosDeFechaYCreacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanCreateTasks",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "CanSetDueDate",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanCreateTasks",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "CanSetDueDate",
                table: "Users");
        }
    }
}
