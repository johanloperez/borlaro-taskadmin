using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <summary>La escalera deja de estar atada al escritorio y pasa a usar el canal de cada
    /// persona; se agrega `Users.PreferredChannel` para poder fijarlo.
    ///
    /// Los dos primeros peldaños se llamaban `FirstDesktopToast` y `SecondDesktopToast`. Ese
    /// nombre era el problema: nombraba un canal donde tenía que nombrar un rol. Ahora son
    /// `FirstDirectPing` y `SecondDirectPing`.
    ///
    /// **Los enums se guardan como texto**, así que renombrarlos en el código deja las filas
    /// viejas con un valor que el enum ya no sabe leer, y eso revienta al cargar el historial de
    /// entregas. Por eso las dos sentencias de abajo: no son cosmética, son lo que hace que la
    /// migración no rompa lo que ya está escrito.</summary>
    public partial class EscaleraPorCanalDeCadaPersona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredChannel",
                table: "Users",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""NotificationAttempts""
                SET ""Step"" = CASE ""Step""
                    WHEN 'FirstDesktopToast'  THEN 'FirstDirectPing'
                    WHEN 'SecondDesktopToast' THEN 'SecondDirectPing'
                    ELSE ""Step"" END
                WHERE ""Step"" IN ('FirstDesktopToast', 'SecondDesktopToast');");

            migrationBuilder.Sql(@"
                UPDATE ""CheckIns""
                SET ""EscalationStep"" = CASE ""EscalationStep""
                    WHEN 'FirstDesktopToast'  THEN 'FirstDirectPing'
                    WHEN 'SecondDesktopToast' THEN 'SecondDirectPing'
                    ELSE ""EscalationStep"" END
                WHERE ""EscalationStep"" IN ('FirstDesktopToast', 'SecondDesktopToast');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""NotificationAttempts""
                SET ""Step"" = CASE ""Step""
                    WHEN 'FirstDirectPing'  THEN 'FirstDesktopToast'
                    WHEN 'SecondDirectPing' THEN 'SecondDesktopToast'
                    ELSE ""Step"" END
                WHERE ""Step"" IN ('FirstDirectPing', 'SecondDirectPing');");

            migrationBuilder.Sql(@"
                UPDATE ""CheckIns""
                SET ""EscalationStep"" = CASE ""EscalationStep""
                    WHEN 'FirstDirectPing'  THEN 'FirstDesktopToast'
                    WHEN 'SecondDirectPing' THEN 'SecondDesktopToast'
                    ELSE ""EscalationStep"" END
                WHERE ""EscalationStep"" IN ('FirstDirectPing', 'SecondDirectPing');");

            migrationBuilder.DropColumn(
                name: "PreferredChannel",
                table: "Users");
        }
    }
}
