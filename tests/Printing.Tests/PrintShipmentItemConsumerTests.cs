using FactoryERP.Contracts.Shipping;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Printing.Application.Abstractions;
using Printing.Application.Models;
using Printing.Infrastructure.Consumers;
using Printing.Infrastructure.Strategies;
using Shipping.Application.Abstractions;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;

namespace Printing.Tests;

/// <summary>Unit tests for <see cref="PrintShipmentItemConsumer"/>.</summary>
public sealed class PrintShipmentItemConsumerTests
{
    private readonly IShipmentBatchRepository     _repository       = Substitute.For<IShipmentBatchRepository>();
    private readonly IQrPayloadBuilder            _qrBuilder        = Substitute.For<IQrPayloadBuilder>();
    private readonly ILabelTemplateResolver       _templateResolver = Substitute.For<ILabelTemplateResolver>();
    private readonly IPrinterProfileResolver      _printerResolver  = Substitute.For<IPrinterProfileResolver>();
    private readonly ILabelPrinterClient          _printerClient    = Substitute.For<ILabelPrinterClient>();

    private static readonly Guid PrinterId    = Guid.NewGuid();
    private static readonly Guid TemplateId   = Guid.NewGuid();

    // ── Helpers ───────────────────────────────────────────────────────────

    private PrintShipmentItemConsumer CreateConsumer()
    {
        var strategies = new List<ITemplatePrintStrategy>
            { new V1ShipmentLabelStrategy() };
        var selector = new TemplatePrintStrategySelector(
            strategies,
            NullLogger<TemplatePrintStrategySelector>.Instance);

        return new PrintShipmentItemConsumer(
            _repository, _qrBuilder, _templateResolver,
            _printerResolver, _printerClient, selector,
            NullLogger<PrintShipmentItemConsumer>.Instance);
    }

    private static ShipmentBatch CreatePrintRequestedBatch(int itemCount = 2)
    {
        var batch = ShipmentBatch.CreateDraft(
            "SB-20260314-001", "PO-TEST", "test.csv", "sha256", itemCount, "system");

        for (int i = 1; i <= itemCount; i++)
        {
            batch.AddItem(i, $"CUST-{i}", $"PART-{i}", $"Product {i}",
                $"Desc {i}", 10, "PO-TEST", $"PITEM-{i}", "2026-04-01", null, null,
                null, null);
        }

        batch.Submit();
        batch.BeginReview();
        batch.Approve(Guid.NewGuid());
        batch.PrepareForPrint(PrinterId, TemplateId);
        batch.MarkPrintRequested();
        batch.ClearDomainEvents();
        return batch;
    }

    private static PrintShipmentItemCommand MakeCommand(ShipmentBatch batch, Guid itemId) =>
        new()
        {
            IdempotencyKey  = $"{batch.Id}:{itemId}",
            BatchId         = batch.Id,
            ItemId          = itemId,
            BatchNumber     = batch.BatchNumber,
            LineNumber      = 1,
            CustomerCode    = "CUST-1",
            PartNo          = "PART-1",
            ProductName     = "Product 1",
            Description     = "Desc 1",
            Quantity        = 10,
            PoNumber        = "PO-TEST",
            PoItem          = "PITEM-1",
            DueDate         = "2026-04-01",
            LabelCopies     = 1,
            PrinterId       = PrinterId,
            LabelTemplateId = TemplateId,
            RequestedBy     = "system",
            CorrelationId   = batch.Id,
        };

    private static ConsumeContext<PrintShipmentItemCommand> CreateContext(PrintShipmentItemCommand cmd)
    {
        var ctx = Substitute.For<ConsumeContext<PrintShipmentItemCommand>>();
        ctx.Message.Returns(cmd);
        ctx.CancellationToken.Returns(CancellationToken.None);
        return ctx;
    }

    private static LabelTemplateSpec DefaultTemplate() => new()
    {
        Id            = TemplateId,
        TemplateKey   = "ShipmentQrLabel",
        Version       = "v1",
        ZplBody       = "^XA^FD{{ProductName}}^FS^FD{{QrPayload}}^FS^XZ",
        DesignDpi     = 300,
        LabelWidthMm  = 90,
        LabelHeightMm = 55,
    };

    private static PrinterProfile DefaultPrinter() => new()
    {
        Id            = PrinterId,
        Name          = "Zebra-01",
        Host          = "192.168.1.10",
        Port          = 9100,
        Protocol      = "Raw9100",
        Dpi           = 300,
        LabelWidthMm  = 90,
        LabelHeightMm = 55,
        IsEnabled     = true,
    };

    // ── Happy path ────────────────────────────────────────────────────────

    [Fact]
    public async Task Consume_HappyPath_ItemIsMarkedPrinted()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        var item  = batch.Items.First();
        var cmd   = MakeCommand(batch, item.Id);

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _qrBuilder.Build(Arg.Any<ShipmentItemLabelData>())
            .Returns(new QrPayloadData { Payload = "v1|QR", Version = "v1", PartNo = "PART-1" });
        _templateResolver.ResolveAsync(TemplateId, Arg.Any<CancellationToken>())
            .Returns(DefaultTemplate());
        _printerResolver.ResolveAsync(PrinterId, Arg.Any<CancellationToken>())
            .Returns(DefaultPrinter());
        _printerClient.PrintAsync(Arg.Any<PrintDocument>(), Arg.Any<PrinterProfile>(), Arg.Any<CancellationToken>())
            .Returns(PrintDispatchResult.Success());

        await CreateConsumer().Consume(CreateContext(cmd));

        item.IsPrinted.Should().BeTrue();
        item.PrintedAtUtc.Should().NotBeNull();
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_HappyPath_PrintDocumentUsesRenderedZpl()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        var item  = batch.Items.First();
        var cmd   = MakeCommand(batch, item.Id);

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _qrBuilder.Build(Arg.Any<ShipmentItemLabelData>())
            .Returns(new QrPayloadData { Payload = "v1|QR", Version = "v1", PartNo = "PART-1" });
        _templateResolver.ResolveAsync(TemplateId, Arg.Any<CancellationToken>())
            .Returns(DefaultTemplate());
        _printerResolver.ResolveAsync(PrinterId, Arg.Any<CancellationToken>())
            .Returns(DefaultPrinter());

        PrintDocument? capturedDoc = null;
        _printerClient.PrintAsync(
                Arg.Do<PrintDocument>(d => capturedDoc = d),
                Arg.Any<PrinterProfile>(),
                Arg.Any<CancellationToken>())
            .Returns(PrintDispatchResult.Success());

        await CreateConsumer().Consume(CreateContext(cmd));

        capturedDoc.Should().NotBeNull();
        capturedDoc!.ZplContent.Should().Contain("Product 1");
        capturedDoc.ZplContent.Should().Contain("v1|QR");
        capturedDoc.IdempotencyKey.Should().Be(cmd.IdempotencyKey);
    }

    // ── Idempotency ───────────────────────────────────────────────────────

    [Fact]
    public async Task Consume_ItemAlreadyPrinted_SkipsDispatch()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        var item  = batch.Items.First();
        item.MarkPrinted(); // pre-mark as printed

        var cmd = MakeCommand(batch, item.Id);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        await CreateConsumer().Consume(CreateContext(cmd));

        await _printerClient.DidNotReceive()
            .PrintAsync(Arg.Any<PrintDocument>(), Arg.Any<PrinterProfile>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Not-found guards ──────────────────────────────────────────────────

    [Fact]
    public async Task Consume_BatchNotFound_ReturnsWithoutThrow()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ShipmentBatch?)null);

        var cmd = new PrintShipmentItemCommand
        {
            IdempotencyKey  = "x:y",
            BatchId         = Guid.NewGuid(),
            ItemId          = Guid.NewGuid(),
            BatchNumber     = "SB-MISSING",
            LineNumber      = 1,
            CustomerCode    = "C",
            PartNo          = "P",
            ProductName     = "N",
            Description     = "D",
            Quantity        = 1,
            LabelCopies     = 1,
            PrinterId       = PrinterId,
            LabelTemplateId = TemplateId,
            RequestedBy     = "sys",
            CorrelationId   = Guid.NewGuid(),
        };

        var act = () => CreateConsumer().Consume(CreateContext(cmd));
        await act.Should().NotThrowAsync();
        await _printerClient.DidNotReceive()
            .PrintAsync(Arg.Any<PrintDocument>(), Arg.Any<PrinterProfile>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ItemNotInBatch_ReturnsWithoutThrow()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);

        var cmd = MakeCommand(batch, Guid.NewGuid()); // unknown item ID

        var act = () => CreateConsumer().Consume(CreateContext(cmd));
        await act.Should().NotThrowAsync();
    }

    // ── Permanent printer failure ─────────────────────────────────────────

    [Fact]
    public async Task Consume_PrinterDisabled_DoesNotMarkPrinted()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        var item  = batch.Items.First();
        var cmd   = MakeCommand(batch, item.Id);

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _qrBuilder.Build(Arg.Any<ShipmentItemLabelData>())
            .Returns(new QrPayloadData { Payload = "v1|QR", Version = "v1", PartNo = "P" });
        _templateResolver.ResolveAsync(TemplateId, Arg.Any<CancellationToken>())
            .Returns(DefaultTemplate());
        _printerResolver.ResolveAsync(PrinterId, Arg.Any<CancellationToken>())
            .Returns(DefaultPrinter() with { IsEnabled = false });
        _printerClient.PrintAsync(Arg.Any<PrintDocument>(), Arg.Any<PrinterProfile>(), Arg.Any<CancellationToken>())
            .Returns(PrintDispatchResult.Failure("PRINTER_DISABLED", "Printer is disabled."));

        await CreateConsumer().Consume(CreateContext(cmd));

        item.IsPrinted.Should().BeFalse();
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ── Transient failures propagate for retry ────────────────────────────

    [Fact]
    public async Task Consume_TemplateResolverThrows_PropagatesForRetry()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        var item  = batch.Items.First();
        var cmd   = MakeCommand(batch, item.Id);

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _qrBuilder.Build(Arg.Any<ShipmentItemLabelData>())
            .Returns(new QrPayloadData { Payload = "v1|Q", Version = "v1", PartNo = "P" });
        _templateResolver.ResolveAsync(TemplateId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Template not found."));

        var act = () => CreateConsumer().Consume(CreateContext(cmd));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Template not found*");
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_PrinterClientThrows_PropagatesForRetry()
    {
        var batch = CreatePrintRequestedBatch(itemCount: 1);
        var item  = batch.Items.First();
        var cmd   = MakeCommand(batch, item.Id);

        _repository.GetByIdAsync(batch.Id, Arg.Any<CancellationToken>()).Returns(batch);
        _qrBuilder.Build(Arg.Any<ShipmentItemLabelData>())
            .Returns(new QrPayloadData { Payload = "v1|Q", Version = "v1", PartNo = "P" });
        _templateResolver.ResolveAsync(TemplateId, Arg.Any<CancellationToken>())
            .Returns(DefaultTemplate());
        _printerResolver.ResolveAsync(PrinterId, Arg.Any<CancellationToken>())
            .Returns(DefaultPrinter());
        _printerClient.PrintAsync(Arg.Any<PrintDocument>(), Arg.Any<PrinterProfile>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new TimeoutException("Printer timed out."));

        var act = () => CreateConsumer().Consume(CreateContext(cmd));

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("*timed out*");
        item.IsPrinted.Should().BeFalse();
    }
}

