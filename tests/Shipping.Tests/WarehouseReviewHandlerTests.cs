using FactoryERP.Abstractions.Messaging;
using FactoryERP.Contracts.Shipping;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shipping.Application.Abstractions;
using Shipping.Application.Features.WarehouseReview;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Shipping.Domain.Enums;

namespace Shipping.Tests;

/// <summary>
/// Unit tests for warehouse review command handlers:
/// <see cref="ApproveShipmentBatchCommandHandler"/>,
/// <see cref="RejectShipmentBatchCommandHandler"/>,
/// <see cref="PartiallyApproveShipmentBatchCommandHandler"/>.
/// </summary>
public sealed class WarehouseReviewHandlerTests
{
    private readonly IShipmentBatchRepository _repository = Substitute.For<IShipmentBatchRepository>();
    private readonly IEventBus _eventBus = Substitute.For<IEventBus>();

    // ── Helpers ────────────────────────────────────────────────────────────

    private static ShipmentBatch CreateSubmittedBatch(int itemCount = 3)
    {
        var batch = ShipmentBatch.CreateDraft(
            "SB-20260313-001", "PO-TEST", "test.csv", "abc", itemCount, "user");

        for (int i = 1; i <= itemCount; i++)
        {
            batch.AddItem(i, $"CUST-{i}", $"PART-{i}", $"Product {i}",
                $"Desc {i}", i * 10, "PO-TEST", null, null, null, null, null, null);
        }

        batch.Submit();
        batch.ClearDomainEvents();
        return batch;
    }

    // ══════════════════════════════════════════════════════════════════════
    // ApproveShipmentBatchCommandHandler
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Approve_SubmittedBatch_AutoBeginsReviewAndApproves()
    {
        // Arrange
        var batch = CreateSubmittedBatch();
        var reviewerId = Guid.NewGuid();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var handler = new ApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<ApproveShipmentBatchCommandHandler>.Instance);

        var command = new ApproveShipmentBatchCommand(batch.Id, reviewerId, "LGTM");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.BatchId.Should().Be(batch.Id);
        result.Status.Should().Be(ShipmentBatchStatus.Approved.ToString());
        result.ReviewDecision.Should().Be(WarehouseReviewDecision.Approved.ToString());
        result.ReviewedByUserId.Should().Be(reviewerId);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_PublishesShipmentApprovedForPrintingEvent()
    {
        var batch = CreateSubmittedBatch();
        var reviewerId = Guid.NewGuid();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var handler = new ApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<ApproveShipmentBatchCommandHandler>.Instance);

        await handler.Handle(new ApproveShipmentBatchCommand(batch.Id, reviewerId, null), CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ShipmentApprovedForPrintingEvent>(e =>
                e.BatchId == batch.Id &&
                e.ReviewDecision == "Approved" &&
                e.ApprovedItemCount == 3 &&
                e.ExcludedItemCount == 0 &&
                e.CorrelationId == batch.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_NotFound_ThrowsKeyNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ShipmentBatch?)null);

        var handler = new ApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<ApproveShipmentBatchCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ApproveShipmentBatchCommand(Guid.NewGuid(), Guid.NewGuid(), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Approve_AlreadyApproved_ThrowsInvalidOperation()
    {
        var batch = CreateSubmittedBatch();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid());

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var handler = new ApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<ApproveShipmentBatchCommandHandler>.Instance);

        var act = () => handler.Handle(
            new ApproveShipmentBatchCommand(batch.Id, Guid.NewGuid(), null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ══════════════════════════════════════════════════════════════════════
    // RejectShipmentBatchCommandHandler
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Reject_SubmittedBatch_AutoBeginsReviewAndRejects()
    {
        var batch = CreateSubmittedBatch();
        var reviewerId = Guid.NewGuid();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var handler = new RejectShipmentBatchCommandHandler(
            _repository, NullLogger<RejectShipmentBatchCommandHandler>.Instance);

        var command = new RejectShipmentBatchCommand(batch.Id, reviewerId, "Wrong parts");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ShipmentBatchStatus.Rejected.ToString());
        result.ReviewDecision.Should().Be(WarehouseReviewDecision.Rejected.ToString());
        result.ReviewedByUserId.Should().Be(reviewerId);

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_DoesNotPublishIntegrationEvent()
    {
        var batch = CreateSubmittedBatch();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var handler = new RejectShipmentBatchCommandHandler(
            _repository, NullLogger<RejectShipmentBatchCommandHandler>.Instance);

        await handler.Handle(
            new RejectShipmentBatchCommand(batch.Id, Guid.NewGuid(), "reason"),
            CancellationToken.None);

        // Reject handler has no eventBus dependency — verify no integration events published.
        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<ShipmentApprovedForPrintingEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Reject_NotFound_ThrowsKeyNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ShipmentBatch?)null);

        var handler = new RejectShipmentBatchCommandHandler(
            _repository, NullLogger<RejectShipmentBatchCommandHandler>.Instance);

        var act = () => handler.Handle(
            new RejectShipmentBatchCommand(Guid.NewGuid(), Guid.NewGuid(), "reason"),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ══════════════════════════════════════════════════════════════════════
    // PartiallyApproveShipmentBatchCommandHandler
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PartialApprove_SubmittedBatch_SetsPartiallyApproved()
    {
        var batch = CreateSubmittedBatch(3);
        var items = batch.Items.ToList();
        var reviewerId = Guid.NewGuid();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var handler = new PartiallyApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<PartiallyApproveShipmentBatchCommandHandler>.Instance);

        var decisions = new List<ItemReviewDecisionDto>
        {
            new(items[0].Id, true, null),
            new(items[1].Id, true, null),
            new(items[2].Id, false, "Damaged goods"),
        };

        var command = new PartiallyApproveShipmentBatchCommand(
            batch.Id, reviewerId, decisions, "2 of 3 approved");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ShipmentBatchStatus.Approved.ToString());
        result.ReviewDecision.Should().Be(WarehouseReviewDecision.PartiallyApproved.ToString());
        result.ApprovedItemCount.Should().Be(2);
        result.ExcludedItemCount.Should().Be(1);
        result.Items.Should().HaveCount(3);

        var excluded = result.Items.Single(i => i.ReviewStatus == ItemReviewStatus.Excluded.ToString());
        excluded.ExclusionReason.Should().Be("Damaged goods");

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PartialApprove_PublishesShipmentApprovedForPrintingEvent()
    {
        var batch = CreateSubmittedBatch(3);
        var items = batch.Items.ToList();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var handler = new PartiallyApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<PartiallyApproveShipmentBatchCommandHandler>.Instance);

        var decisions = new List<ItemReviewDecisionDto>
        {
            new(items[0].Id, true, null),
            new(items[1].Id, false, null),
            new(items[2].Id, false, null),
        };

        await handler.Handle(
            new PartiallyApproveShipmentBatchCommand(batch.Id, Guid.NewGuid(), decisions, null),
            CancellationToken.None);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<ShipmentApprovedForPrintingEvent>(e =>
                e.BatchId == batch.Id &&
                e.ReviewDecision == "PartiallyApproved" &&
                e.ApprovedItemCount == 1 &&
                e.ExcludedItemCount == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PartialApprove_NotFound_ThrowsKeyNotFound()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ShipmentBatch?)null);

        var handler = new PartiallyApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<PartiallyApproveShipmentBatchCommandHandler>.Instance);

        var act = () => handler.Handle(
            new PartiallyApproveShipmentBatchCommand(
                Guid.NewGuid(), Guid.NewGuid(),
                [new ItemReviewDecisionDto(Guid.NewGuid(), true, null)], null),
            CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task PartialApprove_AllApproved_ThrowsInvalidOperation()
    {
        var batch = CreateSubmittedBatch(2);
        var items = batch.Items.ToList();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>())
            .Returns(batch);

        var handler = new PartiallyApproveShipmentBatchCommandHandler(
            _repository, _eventBus, NullLogger<PartiallyApproveShipmentBatchCommandHandler>.Instance);

        var decisions = items
            .Select(i => new ItemReviewDecisionDto(i.Id, true, null))
            .ToList();

        var command = new PartiallyApproveShipmentBatchCommand(
            batch.Id, Guid.NewGuid(), decisions, null);

        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*All items are approved*");
    }
}

