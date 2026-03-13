namespace Labeling.Application.Features.PrintJobs;

public record CreatePrintJobResult(
    Guid PrintJobId,
    string Status,
    string IdempotencyKey,
    DateTime CreatedAtUtc,
    string? Message = null
);

