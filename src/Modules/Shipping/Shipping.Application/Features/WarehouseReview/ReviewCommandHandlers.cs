using FactoryERP.Abstractions.Messaging;
using FactoryERP.Contracts.Shipping;
using MediatR;
using Microsoft.Extensions.Logging;
using Shipping.Application.Abstractions;
using Shipping.Domain.Enums;

namespace Shipping.Application.Features.WarehouseReview;

/// <summary>Handles full batch approval.</summary>
public sealed class ApproveShipmentBatchCommandHandler(
    IShipmentBatchRepository repository,
    IEventBus eventBus,
    ILogger<ApproveShipmentBatchCommandHandler> logger)
    : IRequestHandler<ApproveShipmentBatchCommand, ReviewResultDto>
{
    /// <inheritdoc />
    public async Task<ReviewResultDto> Handle(
        ApproveShipmentBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await repository.GetByIdAsync(request.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Shipment batch '{request.BatchId}' not found.");

        // Auto-begin review if still in Submitted state.
        if (batch.Status == ShipmentBatchStatus.Submitted)
            batch.BeginReview();

        batch.Approve(request.ReviewerUserId, request.Comment);

        // Publish integration event — MassTransit Outbox ensures atomic commit with SaveChanges.
        await eventBus.PublishAsync(new ShipmentApprovedForPrintingEvent
        {
            CorrelationId = batch.Id,
            RequestedBy = request.ReviewerUserId.ToString(),
            BatchId = batch.Id,
            BatchNumber = batch.BatchNumber,
            ReviewDecision = batch.ReviewDecision.ToString(),
            TotalItemCount = batch.Items.Count,
            ApprovedItemCount = batch.Items.Count,
            ExcludedItemCount = 0,
            ReviewedByUserId = request.ReviewerUserId,
            ReviewedAtUtc = batch.ReviewedAtUtc!.Value,
            PoReference = batch.PoReference,
        }, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);

        LogApproved(logger, batch.BatchNumber, request.ReviewerUserId);

        return new ReviewResultDto(
            batch.Id,
            batch.BatchNumber,
            batch.Status.ToString(),
            batch.ReviewDecision.ToString(),
            request.ReviewerUserId,
            batch.ReviewedAtUtc!.Value);
    }

    private static void LogApproved(ILogger logger, string batchNumber, Guid reviewerUserId) => logger.LogInformation("Shipment batch approved: {BatchNumber} by {ReviewerUserId}", batchNumber, reviewerUserId);
}

/// <summary>Handles batch rejection.</summary>
public sealed class RejectShipmentBatchCommandHandler(
    IShipmentBatchRepository repository,
    ILogger<RejectShipmentBatchCommandHandler> logger)
    : IRequestHandler<RejectShipmentBatchCommand, ReviewResultDto>
{
    /// <inheritdoc />
    public async Task<ReviewResultDto> Handle(
        RejectShipmentBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await repository.GetByIdAsync(request.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Shipment batch '{request.BatchId}' not found.");

        if (batch.Status == ShipmentBatchStatus.Submitted)
            batch.BeginReview();

        batch.Reject(request.ReviewerUserId, request.Reason);

        // No integration event on rejection — no downstream work needed.
        await repository.SaveChangesAsync(cancellationToken);

        LogRejected(logger, batch.BatchNumber, request.ReviewerUserId, request.Reason);

        return new ReviewResultDto(
            batch.Id,
            batch.BatchNumber,
            batch.Status.ToString(),
            batch.ReviewDecision.ToString(),
            request.ReviewerUserId,
            batch.ReviewedAtUtc!.Value);
    }

    private static void LogRejected(ILogger logger, string batchNumber, Guid reviewerUserId, string reason) => logger.LogInformation("Shipment batch rejected: {BatchNumber} by {ReviewerUserId}, reason: {Reason}", batchNumber, reviewerUserId, reason);
}

/// <summary>Handles partial approval — per-item approve/exclude decisions.</summary>
public sealed class PartiallyApproveShipmentBatchCommandHandler(
    IShipmentBatchRepository repository,
    IEventBus eventBus,
    ILogger<PartiallyApproveShipmentBatchCommandHandler> logger)
    : IRequestHandler<PartiallyApproveShipmentBatchCommand, PartialApproveResultDto>
{
    /// <inheritdoc />
    public async Task<PartialApproveResultDto> Handle(
        PartiallyApproveShipmentBatchCommand request,
        CancellationToken cancellationToken)
    {
        var batch = await repository.GetByIdAsync(request.BatchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Shipment batch '{request.BatchId}' not found.");

        if (batch.Status == ShipmentBatchStatus.Submitted)
            batch.BeginReview();

        // Build approved ID set and exclusion reasons map.
        var approvedIds = request.ItemDecisions
            .Where(d => d.IsApproved)
            .Select(d => d.ItemId)
            .ToHashSet();

        var exclusionReasons = request.ItemDecisions
            .Where(d => !d.IsApproved && !string.IsNullOrWhiteSpace(d.ExclusionReason))
            .ToDictionary(d => d.ItemId, d => d.ExclusionReason!);

        batch.PartiallyApprove(
            request.ReviewerUserId,
            approvedIds,
            exclusionReasons,
            request.Comment);

        var approvedCount = batch.Items.Count(i => i.ReviewStatus == ItemReviewStatus.Approved);
        var excludedCount = batch.Items.Count(i => i.ReviewStatus == ItemReviewStatus.Excluded);

        // Publish integration event — MassTransit Outbox ensures atomic commit.
        await eventBus.PublishAsync(new ShipmentApprovedForPrintingEvent
        {
            CorrelationId = batch.Id,
            RequestedBy = request.ReviewerUserId.ToString(),
            BatchId = batch.Id,
            BatchNumber = batch.BatchNumber,
            ReviewDecision = batch.ReviewDecision.ToString(),
            TotalItemCount = batch.Items.Count,
            ApprovedItemCount = approvedCount,
            ExcludedItemCount = excludedCount,
            ReviewedByUserId = request.ReviewerUserId,
            ReviewedAtUtc = batch.ReviewedAtUtc!.Value,
            PoReference = batch.PoReference,
        }, cancellationToken);

        await repository.SaveChangesAsync(cancellationToken);


        LogPartiallyApproved(logger, batch.BatchNumber, request.ReviewerUserId, approvedCount, excludedCount);

        var itemResults = batch.Items
            .OrderBy(i => i.LineNumber)
            .Select(i => new ItemReviewResultDto(
                i.Id,
                i.LineNumber,
                i.PartNo,
                i.ReviewStatus.ToString(),
                i.ExclusionReason))
            .ToList();

        return new PartialApproveResultDto(
            batch.Id,
            batch.BatchNumber,
            batch.Status.ToString(),
            batch.ReviewDecision.ToString(),
            request.ReviewerUserId,
            batch.ReviewedAtUtc!.Value,
            approvedCount,
            excludedCount,
            itemResults);
    }

    private static void LogPartiallyApproved(ILogger logger, string batchNumber, Guid reviewerUserId, int approvedCount, int excludedCount) => logger.LogInformation("Shipment batch partially approved: {BatchNumber} by {ReviewerUserId}, approved={ApprovedCount}, excluded={ExcludedCount}", batchNumber, reviewerUserId, approvedCount, excludedCount);
}

