using EDI.Application;
using EDI.Application.Features.GetFileTypeConfigs;
using EDI.Application.Features.ImportSelectedRows;
using EDI.Application.Features.PreviewEdiFile;
using EDI.Application.Features.SelectRows;
using EDI.Application.Features.UploadEdiBatch;
using EDI.Application.Features.ValidateEdiFile;
using EDI.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace EDI.Api;

public static class EdiModule
{
    public static IServiceCollection AddEdiModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddEdiInfrastructure(config);
        services.AddEdiApplication();
        return services;
    }

    public static IEndpointRouteBuilder MapEdiEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/edi");

        g.MapGet("/ping", () => Results.Ok("edi-ok"));


        // ── Legacy endpoints ──

        g.MapPost("/receive", async (ReceiveEdiFileRequest req, IMediator mediator) =>
        {
            var command = new EDI.Application.Features.ReceiveEdiFile.ReceiveEdiFileCommand(
                req.PartnerCode,
                req.FullPath,
                req.FileName);

            var jobId = await mediator.Send(command);
            return Results.Ok(new { JobId = jobId });
        });

        g.MapPost("/upload", async (HttpRequest request, IMediator mediator) =>
        {
            if (!request.HasFormContentType)
                return Results.BadRequest("Invalid content type. Expected multipart/form-data.");

            var form = await request.ReadFormAsync();
            var file = form.Files.GetFile("file");
            var partnerCode = form["PartnerCode"].ToString();

            if (file is null || file.Length == 0)
                return Results.BadRequest("No file uploaded.");

            if (string.IsNullOrWhiteSpace(partnerCode))
                return Results.BadRequest("PartnerCode is required.");

            var tempPath = Path.GetTempFileName();
            await using (var stream = new FileStream(tempPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var command = new EDI.Application.Features.ReceiveEdiFile.ReceiveEdiFileCommand(
                partnerCode,
                tempPath,
                file.FileName);

            try
            {
                var jobId = await mediator.Send(command);
                return Results.Ok(new { JobId = jobId });
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
                return Results.Problem(ex.Message);
            }
        }).DisableAntiforgery();

        // ── Config-driven endpoints ──

        // GET /api/v1/edi/file-type-configs
        g.MapGet("/file-type-configs", async (IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetFileTypeConfigsQuery(), ct);
            return Results.Ok(result);
        });

        // POST /api/v1/edi/upload-batch — multi-file upload with auto-detection
        g.MapPost("/upload-batch", async (HttpRequest request, IMediator mediator, CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.Problem(
                    detail: "Expected multipart/form-data.",
                    statusCode: 400,
                    title: "Invalid Content Type");
            }

            var form = await request.ReadFormAsync(ct);
            var partnerCode = form["PartnerCode"].ToString();

            if (string.IsNullOrWhiteSpace(partnerCode))
            {
                return Results.Problem(
                    detail: "PartnerCode is required.",
                    statusCode: 400,
                    title: "Validation Error");
            }

            if (form.Files.Count == 0)
            {
                return Results.Problem(
                    detail: "At least one file is required.",
                    statusCode: 400,
                    title: "Validation Error");
            }

            var files = new List<UploadFileItem>(form.Files.Count);
            foreach (var f in form.Files)
            {
                files.Add(new UploadFileItem(f.OpenReadStream(), f.FileName, f.Length));
            }

            try
            {
                var command = new UploadEdiBatchCommand(partnerCode, files);
                var result = await mediator.Send(command, ct);
                return Results.Accepted(value: result);
            }
            finally
            {
                foreach (var f in files)
                {
                    await f.Content.DisposeAsync();
                }
            }
        }).DisableAntiforgery();

        // GET /api/v1/edi/jobs/{jobId}/preview
        g.MapGet("/jobs/{jobId:guid}/preview", async (
            Guid jobId,
            int? lines,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new PreviewEdiFileQuery(jobId, lines ?? 20);
            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        });

        // POST /api/v1/edi/jobs/{jobId}/parse
        g.MapPost("/jobs/{jobId:guid}/parse", async (
            Guid jobId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new EDI.Application.Features.ParseEdiFile.ParseEdiFileCommand(jobId);
            var count = await mediator.Send(command, ct);
            return Results.Ok(new { JobId = jobId, ParsedRecords = count });
        });

        // POST /api/v1/edi/jobs/{jobId}/validate
        g.MapPost("/jobs/{jobId:guid}/validate", async (
            Guid jobId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new ValidateEdiFileCommand(jobId);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        // PUT /api/v1/edi/jobs/{jobId}/select-rows
        g.MapPut("/jobs/{jobId:guid}/select-rows", async (
            Guid jobId,
            SelectRowsRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new SelectRowsCommand(jobId, body.RowIndexes, body.IsSelected);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        // PUT /api/v1/edi/jobs/{jobId}/select-all
        g.MapPut("/jobs/{jobId:guid}/select-all", async (
            Guid jobId,
            SelectAllRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new SelectAllRowsCommand(jobId, body.IsSelected);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        // POST /api/v1/edi/jobs/{jobId}/import
        g.MapPost("/jobs/{jobId:guid}/import", async (
            Guid jobId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new ImportSelectedRowsCommand(jobId);
            var result = await mediator.Send(command, ct);
            return Results.Ok(result);
        });

        return app;
    }

    // ── Request DTOs ──
    public record ReceiveEdiFileRequest(string PartnerCode, string FullPath, string FileName);
    public record SelectRowsRequest(IReadOnlyList<int> RowIndexes, bool IsSelected = true);
    public record SelectAllRequest(bool IsSelected = true);
}
