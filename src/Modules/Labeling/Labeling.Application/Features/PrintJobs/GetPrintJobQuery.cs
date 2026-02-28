using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Application.Features.PrintJobs;

// ── Query ─────────────────────────────────────────────────────────────────
public record GetPrintJobQuery(Guid PrintJobId) : IRequest<PrintJobDto?>;

// ── DTO ───────────────────────────────────────────────────────────────────
public record PrintJobDto(
    Guid Id,
    string IdempotencyKey,
    Guid PrinterId,
    int Copies,
    PrintJobStatus Status,
    int FailCount,
    string? LastErrorCode,
    string? LastErrorMessage,
    Guid CorrelationId,
    string RequestedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? PrintedAtUtc);

// ── Handler ───────────────────────────────────────────────────────────────
public sealed class GetPrintJobHandler : IRequestHandler<GetPrintJobQuery, PrintJobDto?>
{
    private readonly ILabelingDbContext _dbContext;

    public GetPrintJobHandler(ILabelingDbContext dbContext) => _dbContext = dbContext;

    public async Task<PrintJobDto?> Handle(GetPrintJobQuery request, CancellationToken cancellationToken)
    {
        var job = await _dbContext.PrintJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == request.PrintJobId, cancellationToken);

        if (job is null) return null;

        return new PrintJobDto(
            job.Id,
            job.IdempotencyKey,
            job.PrinterId,
            job.Copies,
            job.Status,
            job.FailCount,
            job.LastErrorCode,
            job.LastErrorMessage,
            job.CorrelationId,
            job.RequestedBy,
            job.CreatedAtUtc,
            job.UpdatedAtUtc,
            job.PrintedAtUtc);
    }
}

