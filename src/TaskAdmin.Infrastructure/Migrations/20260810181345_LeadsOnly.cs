using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskAdmin.Infrastructure.Migrations
{
    /// <summary>La tabla de miembros pasa a ser el registro de líderes y nada más.
    ///
    /// Participar en un proyecto ahora se deduce de tener trabajo asignado o revisiones a cargo,
    /// así que una fila «Member» no significaba nada: era una lista paralela que alguien tenía
    /// que mantener sincronizada con la realidad, y que se desincronizaba el primer día.</summary>
    public partial class LeadsOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Antes de borrarlas: si un proyecto quedaba sin ningún líder, se promueve a quien
            // más trabajo tiene asignado ahí. Sin líder, solo un admin puede asignar.
            migrationBuilder.Sql("""
                WITH candidato AS (
                    SELECT DISTINCT ON (m."ProjectId") m."ProjectId", m."UserId"
                    FROM "ProjectMembers" m
                    LEFT JOIN "WorkItems" i
                        ON i."ProjectId" = m."ProjectId" AND i."AssigneeId" = m."UserId"
                    WHERE m."ProjectId" NOT IN (
                        SELECT "ProjectId" FROM "ProjectMembers" WHERE "Role" = 'Lead'
                    )
                    GROUP BY m."ProjectId", m."UserId", m."AddedAt"
                    ORDER BY m."ProjectId", COUNT(i."Id") DESC, m."AddedAt"
                )
                UPDATE "ProjectMembers" m
                SET "Role" = 'Lead'
                FROM candidato c
                WHERE m."ProjectId" = c."ProjectId" AND m."UserId" = c."UserId";
                """);

            migrationBuilder.Sql("""DELETE FROM "ProjectMembers" WHERE "Role" <> 'Lead';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Las filas borradas no se pueden reconstruir: eran una lista manual sin más fuente
            // que ella misma. Bajar deja solo a los líderes, que es información real.
        }
    }
}
