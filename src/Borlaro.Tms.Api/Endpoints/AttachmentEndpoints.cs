using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Borlaro.Tms.Api.Auth;
using Borlaro.Tms.Domain.Entities;
using Borlaro.Tms.Infrastructure;
using Borlaro.Tms.Infrastructure.Services;
using Borlaro.Tms.Infrastructure.Storage;

namespace Borlaro.Tms.Api.Endpoints;

/// <summary>Los archivos que acompañan al enunciado de una tarea: el brief, una captura, el PDF
/// con las especificaciones.
///
/// Están separados de los entregables a propósito, y la diferencia es de significado: **el adjunto
/// es lo que ENTRA a la tarea y el entregable lo que SALE**. Un brief no se versiona ni se aprueba;
/// meterlo en `Deliverable` haría que cada documento de referencia apareciera como algo pendiente
/// de revisar, y el ciclo de revisión —que es una de las funciones que el producto vende— dejaría
/// de significar lo que significa.</summary>
public static class AttachmentEndpoints
{
    public static IEndpointRouteBuilder MapAttachmentEndpoints(this IEndpointRouteBuilder app)
    {
        var items = app.MapGroup("/api/items/{itemId:guid}").WithTags("Adjuntos").RequireAuthorization();

        items.MapGet("/adjuntos", async (
            Guid itemId,
            ClaimsPrincipal principal,
            ProjectAccess access,
            BorlaroTmsDbContext db,
            CancellationToken ct) =>
        {
            if (!(await access.ForWorkItemAsync(principal.UserId(), itemId, ct)).CanView) return Results.NotFound();

            var rows = await db.WorkItemAttachments
                .AsNoTracking()
                .Where(a => a.WorkItemId == itemId)
                .OrderBy(a => a.CreatedAt)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    a.ContentType,
                    a.SizeBytes,
                    a.CreatedAt,
                    UploadedByName = a.UploadedBy != null ? a.UploadedBy.Name : null
                })
                .ToListAsync(ct);

            return Results.Ok(rows);
        })
        .WithName("ListAttachments");

        items.MapPost("/adjuntos", async (
            Guid itemId,
            HttpRequest request,
            ClaimsPrincipal principal,
            ProjectAccess access,
            BorlaroTmsDbContext db,
            IFileStore files,
            IOptionsMonitor<FileStoreOptions> storage,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForWorkItemAsync(me, itemId, ct);
            if (!permissions.CanView) return Results.NotFound();

            var flags = await db.WorkItems
                .Where(i => i.Id == itemId)
                .Select(i => new { i.AssigneeId, i.AssigneeCanEdit })
                .FirstOrDefaultAsync(ct);

            if (flags is null) return Results.NotFound();

            // Adjuntar es cambiar el enunciado, así que es el mismo permiso que editarlo y no uno
            // nuevo: quien no puede reescribir la tarea tampoco puede cambiar lo que la explica.
            if (!permissions.CanEditItem(flags.AssigneeId, me, flags.AssigneeCanEdit))
            {
                return Results.Problem("No podés adjuntar archivos a esta tarea.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            if (!request.HasFormContentType)
            {
                return Results.Problem("Se espera multipart/form-data.", statusCode: 400);
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");

            if (file is null || file.Length == 0) return Results.Problem("Falta el archivo.", statusCode: 400);

            // El mismo tope que los entregables, editable desde Configuración: un adjunto no
            // merece un límite propio y dos números para lo mismo se desalinean solos.
            var maxBytes = storage.CurrentValue.MaxBytes;

            if (file.Length > maxBytes)
            {
                return Results.Problem(
                    $"El archivo supera el máximo de {maxBytes / 1024 / 1024} MB configurado.",
                    statusCode: 400);
            }

            // El nombre que manda el cliente nunca toca el disco: el almacén guarda con un nombre
            // generado y este queda como metadato. Un «..\\..\\web.config» no escribe nada.
            await using var stream = file.OpenReadStream();
            var stored = await files.SaveAsync(stream, file.FileName, file.ContentType, ct);

            var attachment = new WorkItemAttachment
            {
                WorkItemId = itemId,
                Name = stored.OriginalName,
                FilePath = stored.Path,
                ContentType = stored.ContentType,
                SizeBytes = stored.SizeBytes,
                UploadedById = me
            };

            db.WorkItemAttachments.Add(attachment);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { attachment.Id, attachment.Name, attachment.SizeBytes });
        })
        .WithName("UploadAttachment")
        .DisableAntiforgery();

        items.MapGet("/adjuntos/{attachmentId:guid}", async (
            Guid itemId,
            Guid attachmentId,
            ClaimsPrincipal principal,
            ProjectAccess access,
            BorlaroTmsDbContext db,
            IFileStore files,
            CancellationToken ct) =>
        {
            if (!(await access.ForWorkItemAsync(principal.UserId(), itemId, ct)).CanView) return Results.NotFound();

            var attachment = await db.WorkItemAttachments
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.WorkItemId == itemId, ct);

            if (attachment is null) return Results.NotFound();

            var stream = await files.OpenAsync(attachment.FilePath, ct);

            // Con su nombre original: el almacén guarda con un nombre generado, y bajar
            // «a3f2c1…» no le dice nada a nadie.
            return Results.File(stream, attachment.ContentType ?? "application/octet-stream", attachment.Name);
        })
        .WithName("DownloadAttachment");

        items.MapDelete("/adjuntos/{attachmentId:guid}", async (
            Guid itemId,
            Guid attachmentId,
            ClaimsPrincipal principal,
            ProjectAccess access,
            BorlaroTmsDbContext db,
            IFileStore files,
            CancellationToken ct) =>
        {
            var me = principal.UserId();
            var permissions = await access.ForWorkItemAsync(me, itemId, ct);
            if (!permissions.CanView) return Results.NotFound();

            var flags = await db.WorkItems
                .Where(i => i.Id == itemId)
                .Select(i => new { i.AssigneeId, i.AssigneeCanEdit })
                .FirstOrDefaultAsync(ct);

            if (flags is null) return Results.NotFound();

            if (!permissions.CanEditItem(flags.AssigneeId, me, flags.AssigneeCanEdit))
            {
                return Results.Problem("No podés borrar adjuntos de esta tarea.",
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var attachment = await db.WorkItemAttachments
                .FirstOrDefaultAsync(a => a.Id == attachmentId && a.WorkItemId == itemId, ct);

            if (attachment is null) return Results.NotFound();

            db.WorkItemAttachments.Remove(attachment);
            await db.SaveChangesAsync(ct);

            // El archivo, después de que la fila se fue y sin tirar si falla. Al revés, un borrado
            // en base que fallara dejaría una fila apuntando a un archivo que ya no está; así, lo
            // peor que pasa es un archivo que ocupa disco y no lo referencia nadie.
            try
            {
                await files.DeleteAsync(attachment.FilePath, ct);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Mejor-esfuerzo a propósito: ver arriba.
            }

            return Results.NoContent();
        })
        .WithName("DeleteAttachment");

        return app;
    }
}
