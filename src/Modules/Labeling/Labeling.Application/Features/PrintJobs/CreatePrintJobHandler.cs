using FactoryERP.Contracts.Labeling;
using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Application.Features.PrintJobs;

public sealed class CreatePrintJobHandler : IRequestHandler<CreatePrintJobCommand, CreatePrintJobResult>
{
    private readonly ILabelingDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public CreatePrintJobHandler(ILabelingDbContext dbContext, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<CreatePrintJobResult> Handle(CreatePrintJobCommand request, CancellationToken cancellationToken)
    {
        // ── Idempotency: return existing job if key already used ──────────
        var existing = await _dbContext.PrintJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.IdempotencyKey == request.IdempotencyKey, cancellationToken);

        if (existing is not null)
            return new CreatePrintJobResult(existing.Id, AlreadyExisted: true);

        // ── Validate printer exists and is enabled ────────────────────────
        var printer = await _dbContext.Printers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PrinterId, cancellationToken)
            ?? throw new InvalidOperationException($"Printer '{request.PrinterId}' not found in registry.");

        if (!printer.IsEnabled)
            throw new InvalidOperationException($"Printer '{printer.Name}' is disabled.");

        // ── Create aggregate ──────────────────────────────────────────────
        var correlationId = Guid.NewGuid();

        var printJob = PrintJob.Create(
            idempotencyKey: request.IdempotencyKey,
            printerId: request.PrinterId,
            zplPayload: request.ZplContent,
            copies: request.Copies,
            correlationId: correlationId,
            requestedBy: request.RequestedBy);

        _dbContext.PrintJobs.Add(printJob);

        // ── Publish via MassTransit EF Outbox (same SaveChanges tx) ──────
        await _publishEndpoint.Publish(new QrPrintRequestedIntegrationEvent
        {
            CorrelationId = correlationId,
            RequestedBy = request.RequestedBy,
            PrintJobId = printJob.Id,
            PrinterId = request.PrinterId
        }, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreatePrintJobResult(printJob.Id, AlreadyExisted: false);
    }
}
