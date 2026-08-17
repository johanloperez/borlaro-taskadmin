using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Borlaro.Tms.Api.Endpoints;

/// <summary>ABM de plantillas de disciplina. Es lo que permite que el sistema sirva a un oficio
/// que no previmos —un estudio de arquitectura, un equipo legal, una productora— sin tocar el
/// binario ni la base a mano.
///
/// Leer las plantillas lo puede hacer cualquiera con sesión, porque el alta de proyectos las
/// necesita. Editarlas es de admin: una plantilla mal armada afecta a todos los proyectos que se
/// creen después.</summary>
public static class TemplateEndpoints
{
    public static IEndpointRouteBuilder MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var templates = app.MapGroup("/api/templates").WithTags("Plantillas")
            .RequireAuthorization(Policies.IsAdmin);

        templates.MapGet("/{id:guid}", async (
            Guid id,
            TemplateService service,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var template = await service.ByIdAsync(id, ct);
            if (template is null) return Results.NotFound();

            return Results.Ok(await ToDetailAsync(template, db, ct));
        })
        .WithName("GetTemplate");

        /// El detalle de todas, para el listado del editor. El resumen liviano sigue en
        /// /api/templates, que es el que consume el alta de proyectos.
        templates.MapGet("/full", async (
            TemplateService service,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            var all = await service.AllAsync(ct);
            var details = new List<TemplateDetail>();
            foreach (var template in all) details.Add(await ToDetailAsync(template, db, ct));

            return Results.Ok(details);
        })
        .WithName("ListTemplatesFull");

        templates.MapPost("/", async (
            TemplateInput body,
            TemplateService service,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
            await GuardAsync(async () =>
            {
                var created = await service.CreateAsync(body, ct);
                return Results.Created($"/api/templates/{created.Id}", await ToDetailAsync(created, db, ct));
            }))
        .WithName("CreateTemplate");

        templates.MapPut("/{id:guid}", async (
            Guid id,
            TemplateInput body,
            TemplateService service,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(await ToDetailAsync(await service.UpdateAsync(id, body, ct), db, ct))))
        .WithName("UpdateTemplate");

        templates.MapPost("/{id:guid}/duplicate", async (
            Guid id,
            TemplateService service,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
            await GuardAsync(async () =>
                Results.Ok(await ToDetailAsync(await service.DuplicateAsync(id, ct), db, ct))))
        .WithName("DuplicateTemplate");

        templates.MapDelete("/{id:guid}", async (
            Guid id,
            TemplateService service,
            CancellationToken ct) =>
            await GuardAsync(async () =>
            {
                await service.DeleteAsync(id, ct);
                return Results.NoContent();
            }))
        .WithName("DeleteTemplate");

        return app;
    }

    private static async Task<TemplateDetail> ToDetailAsync(
        ProjectTemplate template,
        BorlaroTmsDbContext db,
        CancellationToken ct) =>
        new(template.Id,
            template.Key,
            template.Name,
            template.Description,
            template.IsBuiltIn,
            template.ItemNounSingular,
            template.ItemNounPlural,
            template.WorkItemTypes,
            template.AgentContext,
            await db.Projects.CountAsync(p => p.TemplateId == template.Id, ct),
            template.Stages.OrderBy(s => s.Order).Select(s => new TemplateStageDetail(
                s.Name, s.Order, s.Category, s.RequiresBlockerReason, s.OpensReviewRound, s.AllowedNext)).ToList(),
            template.Fields.OrderBy(f => f.Order).Select(f => new TemplateFieldDetail(
                f.Key, f.Label, f.Type, f.Options, f.Required, f.AgentHint)).ToList());

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainException ex)
        {
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
