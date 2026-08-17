using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DificultadObligatoriaYEtiquetas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Primero los datos, después la restricción. El scaffolding proponía rellenar con
            // cadena vacía, que no es un nivel válido: dejaría filas que no son ni Baja ni Media
            // ni Alta y que el enum no sabe leer.
            //
            // Media y no Baja para las tareas que ya existían. Son anteriores a que el campo
            // existiera, así que nadie las juzgó; ponerlas en Baja afirmaría que alguien las
            // consideró fáciles, y el agente trataría un dato inventado como si fuera una opinión.
            // Media es el punto medio y no lo arrastra hacia ninguno de los dos extremos.
            migrationBuilder.Sql(
                @"UPDATE ""WorkItems"" SET ""Difficulty"" = 'Media' WHERE ""Difficulty"" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Difficulty",
                table: "WorkItems",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLabelHigh",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLabelLow",
                table: "Projects",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DifficultyLabelMedium",
                table: "Projects",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DifficultyLabelHigh",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DifficultyLabelLow",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "DifficultyLabelMedium",
                table: "Projects");

            migrationBuilder.AlterColumn<string>(
                name: "Difficulty",
                table: "WorkItems",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40);
        }
    }
}
