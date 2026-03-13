using MediatR;

namespace Labeling.Application.Features.PrintJobs;

/// <summary>
/// Creates a new print job. Idempotent: if <see cref="IdempotencyKey"/> already exists,
/// returns the existing job ID without creating a duplicate.
/// </summary>
public record CreatePrintJobCommand(
    string IdempotencyKey,
    Guid PrinterId,
    string ZplContent,
    int Copies,
    string RequestedBy) : IRequest<CreatePrintJobResult>;
