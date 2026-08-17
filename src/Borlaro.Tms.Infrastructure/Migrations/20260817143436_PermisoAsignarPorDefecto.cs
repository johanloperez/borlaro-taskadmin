using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <summary>Enciende `CanAssignTasks` para quien ya existía.
    ///
    /// La migración anterior agregó el campo apagado, y eso le quitó de golpe a todo el mundo
    /// —también a los administradores— la capacidad de repartir trabajo que tenían desde siempre,
    /// sin ninguna pantalla donde devolvérsela. El permiso es una restricción que se aplica a mano,
    /// no algo que haya que conceder de a uno; las cuentas nuevas nacen con él encendido desde la
    /// entidad `User`.
    ///
    /// Encenderlo para todas las filas no le da a nadie nada nuevo: este campo se comprueba
    /// *además* del permiso sobre el proyecto, así que quien no lidera sigue sin poder asignar.</summary>
    public partial class PermisoAsignarPorDefecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Users"" SET ""CanAssignTasks"" = TRUE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: no queda registro de quién lo tenía apagado antes, y apagárselo a
            // todos sería peor que dejarlo como está.
        }
    }
}
