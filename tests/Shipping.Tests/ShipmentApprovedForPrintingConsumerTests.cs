using FactoryERP.Contracts.Shipping;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shipping.Application.Abstractions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Shipping.Domain.Enums;
using Shipping.Infrastructure.Consumers;

namespace Shipping.Tests;

/// <summary>
/// Unit tests for <see cref="ShipmentApprovedForPrintingConsumer"/>.
/// </summary>
public sealed class ShipmentApprovedForPrintingConsumerTests
{
    private readonly IShipmentBatchRepository _repository = Substitute.For<IShipmentBatchRepository>();
    private readonly IShipmentPrinterResolver _printerResolver = Substitute.For<IShipmentPrinterResolver>();
    private readonly IPublishEndpoint _publishEndpoint = Substitute.For<IPublishEndpoint>();

    private static readonly Guid DefaultPrinterId = Guid.NewGuid();
    private static readonly Guid DefaultTemplateId = Guid.NewGuid();

    private ShipmentApprovedForPrintingConsumer CreateConsumer() =>
        new(_repository, _printerResolver, _publishEndpoint,
            NullLogger<ShipmentApprovedForPrintingConsumer>.Instance);

    // ── Test data helpers ──────────────────────────────────────────────────

    /// <summary>Creates an approved batch with the given number of items.</summary>
    private static ShipmentBatch CreateApprovedBatch(int itemCount = 3)
    {
        var batch = ShipmentBatch.CreateDraft(
            "SB-20260314-001", "PO-TEST", "test.csv", "sha256", itemCount, "user");

        for (int i = 1; i <= itemCount; i++)
        {
            batch.AddItem(i, $"CUST-{i}", $"PART-{i}", $"Product {i}",
                $"Desc {i}", i * 10, "PO-TEST", $"PITEM-{i}", null, null, null, null, null);
        }

        batch.Submit();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid(), "OK");
        batch.ClearDomainEvents();
        return batch;
    }

    /// <summary>Creates a partially-approved batch: first <paramref name="approvedCount"/> items approved, rest excluded.</summary>
    private static ShipmentBatch CreatePartiallyApprovedBatch(int totalItems = 4, int approvedCount = 2)
    {
        var batch = ShipmentBatch.CreateDraft(
            "SB-20260314-002", "PO-TEST", "test.csv", "sha256", totalItems, "user");

        for (int i = 1; i <= totalItems; i++)
        {
            batch.AddItem(i, $"CUST-{i}", $"PART-{i}", $"Product {i}",
                $"Desc {i}", i * 10, "PO-TEST", $"PITEM-{i}", null, null, null, null, null);
        }

        batch.Submit();
        batch.BeginReview();

        var approvedIds = batch.Items.Take(approvedCount).Select(i => i.Id).ToHashSet();
        batch.PartiallyApprove(Guid.NewGuid(), approvedIds);
        batch.ClearDomainEvents();
        return batch;
    }

    private static ConsumeContext<ShipmentApprovedForPrintingEvent> CreateContext(
        ShipmentApprovedForPrintingEvent message)
    {
        var ctx = Substitute.For<ConsumeContext<ShipmentApprovedForPrintingEvent>>();
        ctx.Message.Returns(message);
        ctx.CancellationToken.Returns(CancellationToken.None);
        ctx.MessageId.Returns(Guid.NewGuid());
        return ctx;
    }

    private static ShipmentApprovedForPrintingEvent BuildEvent(
        ShipmentBatch batch,
        string reviewDecision = "Approved") =>
        new()
        {
            BatchId           = batch.Id,
            BatchNumber       = batch.BatchNumber,
            ReviewDecision    = reviewDecision,
            TotalItemCount    = batch.Items.Count,
            ApprovedItemCount = batch.Items.Count,
            ExcludedItemCount = 0,
            ReviewedByUserId  = Guid.NewGuid(),
            ReviewedAtUtc     = DateTime.UtcNow,
            RequestedBy       = "reviewer-user",
            PoReference       = "PO-TEST",
        };

    // ══════════════════════════════════════════════════════════════════════
    // Happy path — full approval
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Consume_FullApproval_PublishesOneCommandPerItem()
    {
        // Arrange
        var batch = CreateApprovedBatch(itemCount: 3);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));

        var consumer = CreateConsumer();
        var ctx = CreateContext(BuildEvent(batch, "Approved"));

        // Act
        await consumer.Consume(ctx);

        // Assert — one command per item
        await _publishEndpoint.Received(3)
            .Publish(Arg.Any<PrintShipmentItemCommand>(), Arg.Any<CancellationToken>());

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_FullApproval_CommandsHaveCorrectIdempotencyKeys()
    {
        // Arrange
        var batch = CreateApprovedBatch(itemCount: 2);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));

        var publishedCommands = new List<PrintShipmentItemCommand>();
        await _publishEndpoint
            .Publish(
                Arg.Do<PrintShipmentItemCommand>(cmd => publishedCommands.Add(cmd)),
                Arg.Any<CancellationToken>());

        var consumer = CreateConsumer();
        await consumer.Consume(CreateContext(BuildEvent(batch, "Approved")));

        // Assert — each key is "{BatchId}:{ItemId}"
        var expectedKeys = batch.Items
            .Select(i => $"{batch.Id}:{i.Id}")
            .ToHashSet();

        publishedCommands.Select(c => c.IdempotencyKey).Should().BeEquivalentTo(expectedKeys);
    }

    [Fact]
    public async Task Consume_FullApproval_CommandsCarryPrinterAndTemplate()
    {
        // Arrange
        var batch = CreateApprovedBatch(itemCount: 1);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));

        PrintShipmentItemCommand? captured = null;
        await _publishEndpoint
            .Publish(
                Arg.Do<PrintShipmentItemCommand>(cmd => captured = cmd),
                Arg.Any<CancellationToken>());

        var consumer = CreateConsumer();
        await consumer.Consume(CreateContext(BuildEvent(batch, "Approved")));

        // Assert
        captured.Should().NotBeNull();
        captured!.PrinterId.Should().Be(DefaultPrinterId);
        captured.LabelTemplateId.Should().Be(DefaultTemplateId);
        captured.BatchId.Should().Be(batch.Id);
        captured.BatchNumber.Should().Be(batch.BatchNumber);
    }

    [Fact]
    public async Task Consume_FullApproval_BatchStatusAdvancesToPrintRequested()
    {
        // Arrange
        var batch = CreateApprovedBatch(itemCount: 2);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));

        var consumer = CreateConsumer();
        await consumer.Consume(CreateContext(BuildEvent(batch, "Approved")));

        // Assert
        batch.Status.Should().Be(ShipmentBatchStatus.PrintRequested);
        batch.PrinterId.Should().Be(DefaultPrinterId);
        batch.LabelTemplateId.Should().Be(DefaultTemplateId);
        batch.PrintRequestedAtUtc.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════
    // Partial approval
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Consume_PartialApproval_PublishesCommandsOnlyForApprovedItems()
    {
        // Arrange — 4 items, 2 approved, 2 excluded
        var batch = CreatePartiallyApprovedBatch(totalItems: 4, approvedCount: 2);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));

        var consumer = CreateConsumer();
        var evt = BuildEvent(batch, "PartiallyApproved") with
        {
            ApprovedItemCount = 2,
            ExcludedItemCount = 2,
        };

        await consumer.Consume(CreateContext(evt));

        // Assert — only the 2 approved items get commands
        await _publishEndpoint.Received(2)
            .Publish(Arg.Any<PrintShipmentItemCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PartialApproval_ExcludedItemsReceiveNoCommand()
    {
        // Arrange
        var batch = CreatePartiallyApprovedBatch(totalItems: 4, approvedCount: 2);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));

        var publishedItemIds = new List<Guid>();
        await _publishEndpoint
            .Publish(
                Arg.Do<PrintShipmentItemCommand>(cmd => publishedItemIds.Add(cmd.ItemId)),
                Arg.Any<CancellationToken>());

        var consumer = CreateConsumer();
        var evt = BuildEvent(batch, "PartiallyApproved") with
        {
            ApprovedItemCount = 2,
            ExcludedItemCount = 2,
        };
        await consumer.Consume(CreateContext(evt));

        var excludedIds = batch.Items
            .Where(i => i.ReviewStatus == ItemReviewStatus.Excluded)
            .Select(i => i.Id);

        publishedItemIds.Should().NotIntersectWith(excludedIds);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Idempotency
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Consume_BatchAlreadyPrintRequested_SkipsWithoutPublishing()
    {
        // Arrange — simulate batch already advanced beyond Approved
        var batch = CreateApprovedBatch();
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));
        batch.PrepareForPrint(DefaultPrinterId, DefaultTemplateId);
        batch.MarkPrintRequested();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var consumer = CreateConsumer();
        await consumer.Consume(CreateContext(BuildEvent(batch, "Approved")));

        // Assert — no commands published, no SaveChanges
        await _publishEndpoint.DidNotReceive()
            .Publish(Arg.Any<PrintShipmentItemCommand>(), Arg.Any<CancellationToken>());

        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_BatchCompleted_SkipsWithoutPublishing()
    {
        // Arrange — completed batch
        var batch = CreateApprovedBatch();
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .Returns((DefaultPrinterId, DefaultTemplateId));
        batch.PrepareForPrint(DefaultPrinterId, DefaultTemplateId);
        batch.MarkPrintRequested();
        batch.MarkCompleted();

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var consumer = CreateConsumer();
        await consumer.Consume(CreateContext(BuildEvent(batch, "Approved")));

        await _publishEndpoint.DidNotReceive()
            .Publish(Arg.Any<PrintShipmentItemCommand>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // Batch not found
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Consume_BatchNotFound_LogsWarningAndReturnsGracefully()
    {
        // Arrange
        var missingId = Guid.NewGuid();
        _repository.GetByIdAsync(missingId, Arg.Any<CancellationToken>()).Returns((ShipmentBatch?)null);

        var consumer = CreateConsumer();
        var evt = new ShipmentApprovedForPrintingEvent
        {
            BatchId           = missingId,
            BatchNumber       = "SB-MISSING",
            ReviewDecision    = "Approved",
            TotalItemCount    = 1,
            ApprovedItemCount = 1,
            ExcludedItemCount = 0,
            ReviewedByUserId  = Guid.NewGuid(),
            ReviewedAtUtc     = DateTime.UtcNow,
            RequestedBy       = "reviewer",
        };

        // Act + Assert — must not throw
        var act = () => consumer.Consume(CreateContext(evt));
        await act.Should().NotThrowAsync();

        await _publishEndpoint.DidNotReceive()
            .Publish(Arg.Any<PrintShipmentItemCommand>(), Arg.Any<CancellationToken>());
    }

    // ══════════════════════════════════════════════════════════════════════
    // Resolver failure
    // ══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Consume_ResolverThrows_PropagatesForRetry()
    {
        // Arrange
        var batch = CreateApprovedBatch();
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _printerResolver.ResolveAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("ShippingPrint:PrinterId is not configured."));

        var consumer = CreateConsumer();

        // Act + Assert — must rethrow so MassTransit retries
        var act = () => consumer.Consume(CreateContext(BuildEvent(batch)));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*PrinterId is not configured*");

        // No state saved on failure
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

