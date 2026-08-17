using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Borlaro.Tms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El valor por defecto que genera EF es cadena vacía, que no es ningún ProjectRole:
            // las filas que ya existen quedarían ilegibles al mapear el enum. Se siembra Member.
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "ProjectMembers",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Member");

            // Los proyectos que ya existían quedarían sin líder, y entonces solo un admin podría
            // administrarlos. Se promueve a alguien con criterio: primero quien ya lidera equipos
            // en la instancia, y si no hay, el miembro más antiguo del proyecto.
            migrationBuilder.Sql("""
                WITH candidato AS (
                    SELECT DISTINCT ON (m."ProjectId") m."ProjectId", m."UserId"
                    FROM "ProjectMembers" m
                    JOIN "Users" u ON u."Id" = m."UserId"
                    WHERE m."ProjectId" NOT IN (
                        SELECT "ProjectId" FROM "ProjectMembers" WHERE "Role" = 'Lead'
                    )
                    ORDER BY m."ProjectId",
                             CASE u."Role" WHEN 'Admin' THEN 0 WHEN 'Manager' THEN 1 ELSE 2 END,
                             m."AddedAt"
                )
                UPDATE "ProjectMembers" m
                SET "Role" = 'Lead'
                FROM candidato c
                WHERE m."ProjectId" = c."ProjectId" AND m."UserId" = c."UserId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "ProjectMembers");
        }
    }
}
