using FactoryERP.Contracts.Messaging;
using FactoryERP.Contracts.Shipping;
using FluentAssertions;

namespace Shipping.Tests;

/// <summary>
/// Contract tests for integration events — verify serialization shape,
/// default values, and idempotency metadata.
/// </summary>
public sealed class IntegrationEventContractTests
{
    // ── IntegrationEvent base ─────────────────────────────────────────────

    [Fact]
    public void IntegrationEvent_HasMessageId_ByDefault()
    {
        var evt = new ShipmentApprovedForPrintingEvent
        {
            BatchId = Guid.NewGuid(),
            BatchNumber = "SB-001",
            ReviewDecision = "Approved",
            TotalItemCount = 5,
            ApprovedItemCount = 5,
            ExcludedItemCount = 0,
            ReviewedByUserId = Guid.NewGuid(),
            ReviewedAtUtc = DateTime.UtcNow,
        };

        evt.MessageId.Should().NotBeEmpty("MessageId is the idempotency key");
        evt.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        evt.SchemaVersion.Should().Be(1, "default schema version is 1");
    }

    [Fact]
    public void IntegrationEvent_CausationId_IsNullByDefault()
    {
        var evt = new ShipmentApprovedForPrintingEvent
        {
            BatchId = Guid.NewGuid(),
            BatchNumber = "SB-001",
            ReviewDecision = "Approved",
            TotalItemCount = 1,
            ApprovedItemCount = 1,
            ExcludedItemCount = 0,
            ReviewedByUserId = Guid.NewGuid(),
            ReviewedAtUtc = DateTime.UtcNow,
        };

        evt.CausationId.Should().BeNull("CausationId is optional and null when this is the root event");
    }

    // ── ShipmentApprovedForPrintingEvent ──────────────────────────────────

    [Fact]
    public void ShipmentApprovedForPrintingEvent_CorrelationId_SetToBatchId()
    {
        var batchId = Guid.NewGuid();

        var evt = new ShipmentApprovedForPrintingEvent
        {
            CorrelationId = batchId,
            BatchId = batchId,
            BatchNumber = "SB-20260314-001",
            ReviewDecision = "Approved",
            TotalItemCount = 10,
            ApprovedItemCount = 10,
            ExcludedItemCount = 0,
            ReviewedByUserId = Guid.NewGuid(),
            ReviewedAtUtc = DateTime.UtcNow,
            RequestedBy = "reviewer@example.com",
        };

        evt.CorrelationId.Should().Be(batchId, "CorrelationId must equal BatchId for shipment lifecycle tracing");
    }

    [Fact]
    public void ShipmentApprovedForPrintingEvent_PartialApproval_HasExcludedCount()
    {
        var evt = new ShipmentApprovedForPrintingEvent
        {
            BatchId = Guid.NewGuid(),
            BatchNumber = "SB-002",
            ReviewDecision = "PartiallyApproved",
            TotalItemCount = 5,
            ApprovedItemCount = 3,
            ExcludedItemCount = 2,
            ReviewedByUserId = Guid.NewGuid(),
            ReviewedAtUtc = DateTime.UtcNow,
        };

        evt.ApprovedItemCount.Should().BeLessThan(evt.TotalItemCount);
        evt.ExcludedItemCount.Should().Be(2);
        (evt.ApprovedItemCount + evt.ExcludedItemCount).Should().Be(evt.TotalItemCount);
    }

    // ── PrintShipmentItemCommand ──────────────────────────────────────────

    [Fact]
    public void PrintShipmentItemCommand_IdempotencyKey_Format()
    {
        var batchId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        var cmd = new PrintShipmentItemCommand
        {
            IdempotencyKey = $"{batchId}:{itemId}",
            BatchId = batchId,
            ItemId = itemId,
            BatchNumber = "SB-001",
            LineNumber = 1,
            CustomerCode = "CUST-001",
            PartNo = "PART-001",
            ProductName = "Widget",
            Description = "A widget",
            Quantity = 10,
            LabelCopies = 2,
            PrinterId = Guid.NewGuid(),
            LabelTemplateId = Guid.NewGuid(),
            RequestedBy = "user@example.com",
        };

        cmd.IdempotencyKey.Should().Contain(batchId.ToString());
        cmd.IdempotencyKey.Should().Contain(itemId.ToString());
        cmd.CommandId.Should().NotBeEmpty();
        cmd.SchemaVersion.Should().Be(1);
    }

    [Fact]
    public void PrintShipmentItemCommand_CausationId_LinksToApprovalEvent()
    {
        var approvalMessageId = Guid.NewGuid();

        var cmd = new PrintShipmentItemCommand
        {
            CausationId = approvalMessageId,
            CorrelationId = Guid.NewGuid(),
            IdempotencyKey = $"{Guid.NewGuid()}:{Guid.NewGuid()}",
            BatchId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            BatchNumber = "SB-001",
            LineNumber = 1,
            CustomerCode = "C",
            PartNo = "P",
            ProductName = "W",
            Description = "D",
            Quantity = 1,
            LabelCopies = 1,
            PrinterId = Guid.NewGuid(),
            LabelTemplateId = Guid.NewGuid(),
            RequestedBy = "user",
        };

        cmd.CausationId.Should().Be(approvalMessageId,
            "CausationId traces back to the ShipmentApprovedForPrintingEvent that spawned this command");
    }
}

