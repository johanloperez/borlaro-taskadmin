using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;
using Borlaro.Tms.Infrastructure.Storage;

namespace Borlaro.Tms.Api.Endpoints;

public record DeliverableVersionDto(
    Guid Id,
    int Version,
    string? Url,
    string? FileName,
    long? SizeBytes,
    string? Notes,
    string UploadedByName,
    DateTimeOffset CreatedAt,
    bool IsApproved,
    IReadOnlyList<ReviewRoundDto> Reviews);

public record ReviewRoundDto(
    Guid Id,
    Guid DeliverableVersionId,
    int VersionNumber,
    Guid ReviewerId,
    string ReviewerName,
    ReviewStatus Status,
    string? CommentsMd,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt);

public record DeliverableDto(
    Guid Id,
    string Name,
    DeliverableKind Kind,
    int CurrentVersion,
    Guid? ApprovedVersionId,
    int ReviewRoundCount,
    IReadOnlyList<DeliverableVersionDto> Versions);

public record NewLinkBody(string Name, Guid? DeliverableId, string Url, string? Notes);

public record OpenReviewBody(Guid ReviewerId);

public record SubmitReviewBody(ReviewStatus Decision, string? Comments);

public static class DeliverableEndpoints
{
    public static IEndpointRouteBuilder MapDeliverableEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/api/items/{itemId:guid}").WithTags("Entregables").RequireAuthorization();

        items.MapGet("/deliverables", async (Guid itemId, BorlaroTmsDbContext db, CancellationToken ct) =>
        {
            var deliverables = await db.Deliverables
                .AsNoTracking()
                .Where(d => d.WorkItemId == itemId)
                .Include(d => d.Versions)
                .OrderBy(d => d.CreatedAt)
                .ToListAsync(ct);

            var versionIds = deliverables.SelectMany(d => d.Versions).Select(v => v.Id).ToList();

            var rounds = await db.ReviewRounds
                .AsNoTracking()
                .Where(r => versionIds.Contains(r.DeliverableVersionId))
                .Select(r => new
                {
                    r.Id, r.DeliverableVersionId, r.ReviewerId,
                    ReviewerName = r.Reviewer!.Name,
                    r.Status, r.CommentsMd, r.OpenedAt, r.ClosedAt
                })
                .ToListAsync(ct);

            var uploaderIds = deliverables.SelectMany(d => d.Versions).Select(v => v.UploadedById).Distinct().ToList();
            var uploaders = await db.Users.AsNoTracking()
                .Where(u => uploaderIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

            var roundsByVersion = rounds.ToLookup(r => r.DeliverableVersionId);

            return Results.Ok(deliverables.Select(d => new DeliverableDto(
                d.Id, d.Name, d.Kind, d.CurrentVersion, d.ApprovedVersionId,
                d.Versions.Sum(v => roundsByVersion[v.Id].Count()),
                d.Versions.OrderByDescending(v => v.Version).Select(v => new DeliverableVersionDto(
                    v.Id, v.Version, v.Url, v.FileName, v.SizeBytes, v.Notes,
                    uploaders.TryGetValue(v.UploadedById, out var name) ? name : "—",
                    v.CreatedAt,
                    d.ApprovedVersionId == v.Id,
                    roundsByVersion[v.Id]
                        .OrderBy(r => r.OpenedAt)
                        .Select(r => new ReviewRoundDto(
                            r.Id, r.DeliverableVersionId, v.Version, r.ReviewerId, r.ReviewerName,
                            r.Status, r.CommentsMd, r.OpenedAt, r.ClosedAt))
                        .ToList()))
                    .ToList())));
        })
        .WithName("ListDeliverables");

        items.MapPost("/deliverables/file", async (
            Guid itemId,
            HttpRequest request,
            DeliverableService service,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.Problem("Se espera multipart/form-data.", statusCode: 400);
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.Problem("Falta el archivo.", statusCode: 400);
            }

            try
            {
                await using var stream = file.OpenReadStream();
                var result = await service.AddFileVersionAsync(
                    itemId,
                    form["name"].FirstOrDefault(),
                    Guid.TryParse(form["deliverableId"].FirstOrDefault(), out var did) ? did : null,
                    stream,
                    file.FileName,
                    file.ContentType,
                    principal.UserId()!.Value,
                    form["notes"].FirstOrDefault(),
                    ct);

                return Results.Ok(new { result.Deliverable.Id, result.Version.Version });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("UploadDeliverableFile")
        .DisableAntiforgery();

        items.MapPost("/deliverables/link", async (
            Guid itemId,
            NewLinkBody body,
            DeliverableService service,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            try
            {
                var result = await service.AddLinkVersionAsync(
                    itemId, body.Name, body.DeliverableId, body.Url,
                    principal.UserId()!.Value, body.Notes, ct);

                return Results.Ok(new { result.Deliverable.Id, result.Version.Version });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .WithName("AddDeliverableLink");

        var versions = app.MapGroup("/api/deliverable-versions").WithTags("Entregables").RequireAuthorization();

        versions.MapGet("/{versionId:guid}/content", async (
            Guid versionId,
            BorlaroTmsDbContext db,
            IFileStore files,
            CancellationToken ct) =>
        {
            var version = await db.DeliverableVersions.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == versionId, ct);

            if (version?.FilePath is null) return Results.NotFound();

            var stream = await files.OpenAsync(version.FilePath, ct);
            return Results.File(stream, version.ContentType ?? "application/octet-stream", version.FileName);
        })
        .WithName("DownloadDeliverableVersion");

        versions.MapPost("/{versionId:guid}/reviews", async (
            Guid versionId,
            OpenReviewBody body,
            DeliverableService service,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            try
            {
                var round = await service.OpenReviewAsync(
                    versionId, body.ReviewerId, principal.UserId()!.Value, ct);
                return Results.Ok(new { round.Id, round.Status });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 409);
            }
        })
        .WithName("OpenReviewRound");

        app.MapPost("/api/reviews/{roundId:guid}", async (
            Guid roundId,
            SubmitReviewBody body,
            DeliverableService service,
            ClaimsPrincipal principal,
            CancellationToken ct) =>
        {
            try
            {
                var round = await service.SubmitReviewAsync(
                    roundId, body.Decision, body.Comments, principal.UserId()!.Value, ct);
                return Results.Ok(new { round.Id, round.Status, round.ClosedAt });
            }
            catch (DomainException ex)
            {
                return Results.Problem(ex.Message, statusCode: 409);
            }
        })
        .WithTags("Entregables")
        .RequireAuthorization()
        .WithName("SubmitReview");

        return app;
    }
}
