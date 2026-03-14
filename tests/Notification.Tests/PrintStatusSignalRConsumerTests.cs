using FactoryERP.Abstractions.Realtime;
using FactoryERP.Contracts.Shipping;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Infrastructure.Consumers;
using NSubstitute;

namespace Notification.Tests;

/// <summary>
/// Unit tests for real-time print status SignalR consumers:
/// <see cref="ShipmentBatchPrintQueuedSignalRConsumer"/>,
/// <see cref="ShipmentItemPrintedSignalRConsumer"/>,
/// <see cref="ShipmentItemPrintFailedSignalRConsumer"/>.
/// </summary>
public sealed class PrintStatusSignalRConsumerTests
{
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();

    // ── ShipmentBatchPrintQueuedSignalRConsumer ───────────────────────────

    [Fact]
    public async Task BatchQueued_NotifiesWarehouseRole()
    {
        var consumer = new ShipmentBatchPrintQueuedSignalRConsumer(
            _dispatcher, NullLogger<ShipmentBatchPrintQueuedSignalRConsumer>.Instance);

        var msg = new ShipmentItemPrintQueuedEvent
        {
            BatchId           = Guid.NewGuid(),
            BatchNumber       = "SB-001",
            ApprovedItemCount = 5,
            ReviewedByUserId  = "user-123",
            RequestedBy       = "user-123",
            PoReference       = "PO-X",
        };

        var context = Substitute.For<ConsumeContext<ShipmentItemPrintQueuedEvent>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        // Assert: push to role:Warehouse
        await _dispatcher.Received(1).NotifyRoleAsync(
            "Warehouse",
            PrintStatusEventTypes.BatchQueued,
            Arg.Is<BatchQueuedPayload>(p =>
                p.BatchNumber == "SB-001" &&
                p.ApprovedItemCount == 5 &&
                p.ReviewedByUserId == "user-123"),
            Arg.Any<CancellationToken>());
    }

    // ── ShipmentItemPrintedSignalRConsumer ────────────────────────────────

    [Fact]
    public async Task ItemPrinted_NotifiesReviewerAndWarehouse()
    {
        var consumer = new ShipmentItemPrintedSignalRConsumer(
            _dispatcher, NullLogger<ShipmentItemPrintedSignalRConsumer>.Instance);

        var msg = new ShipmentItemPrintedEvent
        {
            BatchId          = Guid.NewGuid(),
            BatchNumber      = "SB-001",
            ItemId           = Guid.NewGuid(),
            LineNumber       = 1,
            PartNo           = "P-1",
            CustomerCode     = "C",
            PrinterId        = Guid.NewGuid(),
            PrinterName      = "Zebra-1",
            PrintedAtUtc     = DateTime.UtcNow,
            ReviewedByUserId = "reviewer-xyz",
        };

        var context = Substitute.For<ConsumeContext<ShipmentItemPrintedEvent>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        // 1. Notify user (reviewer)
        await _dispatcher.Received(1).NotifyUserAsync(
            "reviewer-xyz",
            PrintStatusEventTypes.ItemPrinted,
            Arg.Is<ItemPrintedPayload>(p => p.PartNo == "P-1"),
            Arg.Any<CancellationToken>());

        // 2. Notify role (Warehouse)
        await _dispatcher.Received(1).NotifyRoleAsync(
            "Warehouse",
            PrintStatusEventTypes.ItemPrinted,
            Arg.Is<ItemPrintedPayload>(p => p.PrinterName == "Zebra-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ItemPrinted_WithoutReviewer_SkipsUserNotification()
    {
        var consumer = new ShipmentItemPrintedSignalRConsumer(
            _dispatcher, NullLogger<ShipmentItemPrintedSignalRConsumer>.Instance);

        var msg = new ShipmentItemPrintedEvent
        {
            BatchId          = Guid.NewGuid(),
            BatchNumber      = "SB-001",
            ItemId           = Guid.NewGuid(),
            LineNumber       = 1,
            PartNo           = "P-1",
            CustomerCode     = "C",
            PrinterId        = Guid.NewGuid(),
            PrinterName      = "Zebra-1",
            PrintedAtUtc     = DateTime.UtcNow,
            ReviewedByUserId = "", // Empty
        };

        var context = Substitute.For<ConsumeContext<ShipmentItemPrintedEvent>>();
        context.Message.Returns(msg);

        await consumer.Consume(context);

        // Should NOT call NotifyUserAsync
        await _dispatcher.DidNotReceive().NotifyUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());

        // Should still notify role
        await _dispatcher.Received(1).NotifyRoleAsync(
            "Warehouse", Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    // ── ShipmentItemPrintFailedSignalRConsumer ────────────────────────────

    [Fact]
    public async Task ItemFailed_NotifiesReviewerAndWarehouse()
    {
        var consumer = new ShipmentItemPrintFailedSignalRConsumer(
            _dispatcher, NullLogger<ShipmentItemPrintFailedSignalRConsumer>.Instance);

        var msg = new ShipmentItemPrintFailedEvent
        {
            BatchId          = Guid.NewGuid(),
            BatchNumber      = "SB-001",
            ItemId           = Guid.NewGuid(),
            LineNumber       = 2,
            PartNo           = "P-2",
            CustomerCode     = "C",
            PrinterId        = Guid.NewGuid(),
            ErrorCode        = "PRINTER_OFFLINE",
            ErrorMessage     = "Connection timed out",
            ReviewedByUserId = "reviewer-abc",
        };

        var context = Substitute.For<ConsumeContext<ShipmentItemPrintFailedEvent>>();
        context.Message.Returns(msg);
        context.CancellationToken.Returns(CancellationToken.None);

        await consumer.Consume(context);

        // 1. Notify user
        await _dispatcher.Received(1).NotifyUserAsync(
            "reviewer-abc",
            PrintStatusEventTypes.ItemFailed,
            Arg.Is<ItemPrintFailedPayload>(p => p.ErrorCode == "PRINTER_OFFLINE"),
            Arg.Any<CancellationToken>());

        // 2. Notify role
        await _dispatcher.Received(1).NotifyRoleAsync(
            "Warehouse",
            PrintStatusEventTypes.ItemFailed,
            Arg.Is<ItemPrintFailedPayload>(p => p.ErrorMessage == "Connection timed out"),
            Arg.Any<CancellationToken>());
    }
}
