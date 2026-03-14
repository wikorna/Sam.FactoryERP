using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shipping.Application.Abstractions;
using Shipping.Application.Features.UploadBatch;
using Shipping.Domain.Aggregates.ShipmentBatchAggregate;
using Shipping.Domain.Enums;
using System.Text;

namespace Shipping.Tests;

/// <summary>
/// Unit tests for <see cref="UploadShipmentBatchCommandHandler"/>.
/// </summary>
public sealed class UploadShipmentBatchCommandHandlerTests
{
    private readonly IShipmentCsvParser _csvParser = Substitute.For<IShipmentCsvParser>();
    private readonly IBatchNumberGenerator _batchNumberGen = Substitute.For<IBatchNumberGenerator>();
    private readonly IShipmentBatchRepository _repository = Substitute.For<IShipmentBatchRepository>();
    private readonly UploadShipmentBatchCommandHandler _handler;

    public UploadShipmentBatchCommandHandlerTests()
    {
        _handler = new UploadShipmentBatchCommandHandler(
            _csvParser,
            _batchNumberGen,
            _repository,
            NullLogger<UploadShipmentBatchCommandHandler>.Instance);
    }

    private static MemoryStream ToStream(string content)
        => new(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Handle_ValidCsv_CreatesDraftBatchAndPersists()
    {
        // Arrange
        var stream = ToStream("dummy csv content");
        var command = new UploadShipmentBatchCommand(stream, "test.csv", 100, "PO-OVERRIDE");

        _batchNumberGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("SB-20260313-001");

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ShipmentCsvParseResult
            {
                TotalRows = 2,
                ValidRows =
                [
                    new ShipmentCsvRow
                    {
                        RowNumber = 1, CustomerCode = "C1", PartNo = "P1",
                        ProductName = "Widget", Description = "Desc", Quantity = 10,
                        PoNumber = "PO-001", LabelCopies = 1,
                    },
                    new ShipmentCsvRow
                    {
                        RowNumber = 2, CustomerCode = "C2", PartNo = "P2",
                        ProductName = "Gadget", Description = "Desc2", Quantity = 20,
                        PoNumber = "PO-001", LabelCopies = 3,
                    },
                ],
                Errors = [],
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.BatchNumber.Should().Be("SB-20260313-001");
        result.Status.Should().Be(ShipmentBatchStatus.Draft.ToString());
        result.TotalRows.Should().Be(2);
        result.ValidItemCount.Should().Be(2);
        result.ErrorCount.Should().Be(0);
        result.Errors.Should().BeEmpty();

        _repository.Received(1).Add(Arg.Is<ShipmentBatch>(b =>
            b.BatchNumber == "SB-20260313-001" &&
            b.PoReference == "PO-OVERRIDE" &&
            b.Status == ShipmentBatchStatus.Draft &&
            b.Items.Count == 2));

        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CsvWithErrors_CreatesBatchWithRowErrors()
    {
        // Arrange
        var stream = ToStream("dummy");
        var command = new UploadShipmentBatchCommand(stream, "errors.csv", 50, null);

        _batchNumberGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("SB-20260313-002");

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ShipmentCsvParseResult
            {
                TotalRows = 3,
                ValidRows =
                [
                    new ShipmentCsvRow
                    {
                        RowNumber = 1, CustomerCode = "C1", PartNo = "P1",
                        ProductName = "Widget", Description = "Desc", Quantity = 10,
                        PoNumber = "PO-X",
                    },
                ],
                Errors =
                [
                    new ShipmentCsvRowError
                    {
                        RowNumber = 2, ErrorCode = "MISSING_PART_NO",
                        ErrorMessage = "PartNo is required.",
                    },
                    new ShipmentCsvRowError
                    {
                        RowNumber = 3, ErrorCode = "INVALID_QUANTITY",
                        ErrorMessage = "Quantity must be a positive integer.",
                    },
                ],
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.TotalRows.Should().Be(3);
        result.ValidItemCount.Should().Be(1);
        result.ErrorCount.Should().Be(2);
        result.Errors.Should().HaveCount(2);
        result.Errors[0].ErrorCode.Should().Be("MISSING_PART_NO");
        result.Errors[1].ErrorCode.Should().Be("INVALID_QUANTITY");
    }

    [Fact]
    public async Task Handle_NullPoReference_DerivesFromCsvRows()
    {
        // Arrange
        var stream = ToStream("dummy");
        var command = new UploadShipmentBatchCommand(stream, "derive.csv", 50, null);

        _batchNumberGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("SB-20260313-003");

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ShipmentCsvParseResult
            {
                TotalRows = 2,
                ValidRows =
                [
                    new ShipmentCsvRow
                    {
                        RowNumber = 1, CustomerCode = "C1", PartNo = "P1",
                        ProductName = "W1", Description = "D", Quantity = 5,
                        PoNumber = "PO-100",
                    },
                    new ShipmentCsvRow
                    {
                        RowNumber = 2, CustomerCode = "C2", PartNo = "P2",
                        ProductName = "W2", Description = "D", Quantity = 10,
                        PoNumber = "PO-200",
                    },
                ],
                Errors = [],
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — PO reference derived from unique PO numbers
        _repository.Received(1).Add(Arg.Is<ShipmentBatch>(b =>
            b.PoReference == "PO-100, PO-200"));
    }

    [Fact]
    public async Task Handle_NoPoInRows_DerivesFallbackNA()
    {
        // Arrange
        var stream = ToStream("dummy");
        var command = new UploadShipmentBatchCommand(stream, "no-po.csv", 50, null);

        _batchNumberGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("SB-20260313-004");

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ShipmentCsvParseResult
            {
                TotalRows = 1,
                ValidRows =
                [
                    new ShipmentCsvRow
                    {
                        RowNumber = 1, CustomerCode = "C1", PartNo = "P1",
                        ProductName = "W1", Description = "D", Quantity = 1,
                    },
                ],
                Errors = [],
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repository.Received(1).Add(Arg.Is<ShipmentBatch>(b =>
            b.PoReference == "N/A"));
    }

    [Fact]
    public async Task Handle_LabelCopiesGreaterThanOne_SetsOnItem()
    {
        // Arrange
        var stream = ToStream("dummy");
        var command = new UploadShipmentBatchCommand(stream, "copies.csv", 50, "PO-X");

        _batchNumberGen.GenerateAsync(Arg.Any<CancellationToken>())
            .Returns("SB-20260313-005");

        _csvParser.ParseAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new ShipmentCsvParseResult
            {
                TotalRows = 1,
                ValidRows =
                [
                    new ShipmentCsvRow
                    {
                        RowNumber = 1, CustomerCode = "C1", PartNo = "P1",
                        ProductName = "W1", Description = "D", Quantity = 5,
                        LabelCopies = 5,
                    },
                ],
                Errors = [],
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.ValidItemCount.Should().Be(1);
        _repository.Received(1).Add(Arg.Is<ShipmentBatch>(b =>
            b.Items.First().LabelCopies == 5));
    }
}

