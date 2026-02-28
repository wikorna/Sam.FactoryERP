using Labeling.Application;
using Labeling.Application.Features.PrintJobs;
using Labeling.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Labeling.Api;

public static class LabelingModule
{
    public static IServiceCollection AddLabelingModule(this IServiceCollection services, IConfiguration config)
    {
        services.AddLabelingInfrastructure(config);
        services.AddLabelingApplication();
        return services;
    }

    public static IEndpointRouteBuilder MapLabelingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/labeling");
        g.MapGet("/ping", () => Results.Ok("labeling-ok"));

        // ── Print Jobs CQRS ──────────────────────────────────────────────
        g.MapPost("/print-jobs", async (CreatePrintJobRequest body, IMediator mediator, CancellationToken ct) =>
        {
            var command = new CreatePrintJobCommand(
                body.IdempotencyKey,
                body.PrinterId,
                body.ZplContent,
                body.Copies,
                body.RequestedBy);

            var result = await mediator.Send(command, ct);

            return Results.Accepted(
                $"/api/v1/labeling/print-jobs/{result.PrintJobId}",
                result);
        });

        g.MapGet("/print-jobs/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetPrintJobQuery(id), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        return app;
    }

    // ── API request DTOs ──────────────────────────────────────────────────
        private sealed record  CreatePrintJobRequest(
        string IdempotencyKey,
        Guid PrinterId,
        string ZplContent,
        int Copies,
        string RequestedBy);
}
