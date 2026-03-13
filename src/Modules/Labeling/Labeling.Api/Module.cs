using Labeling.Application;
using Labeling.Application.Features.PrintJobs;
using Labeling.Application.Interfaces;
using Labeling.Infrastructure;
using Labeling.Infrastructure.Services;
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

        services.AddHttpContextAccessor(); // Ensure accessor is available if needed, though usually Host does this.

        return services;
    }

    public static IEndpointRouteBuilder MapLabelingEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/labeling");
        g.MapGet("/ping", () => Results.Ok("labeling-ok"));

        // other endpoints are mapped via Controllers

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
