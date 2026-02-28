using FactoryERP.Contracts.Labeling;
using FluentAssertions;
using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using Labeling.Domain.Exceptions;
using Labeling.Infrastructure.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Labeling.Tests;

public sealed class QrPrintRequestedConsumerTests
{
    private readonly Mock<IZplPrinterClient> _printerClientMock;
    private readonly Mock<ILabelingDbContext> _dbContextMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly QrPrintRequestedConsumer _consumer;

    private readonly Printer _testPrinter;

    public QrPrintRequestedConsumerTests()
    {
        _printerClientMock = new Mock<IZplPrinterClient>();
        _dbContextMock = new Mock<ILabelingDbContext>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<QrPrintRequestedConsumer>>();

        _testPrinter = Printer.Create("printer-dept-a", PrinterProtocol.Raw9100, "192.168.1.101", 9100);

        _consumer = new QrPrintRequestedConsumer(
            _printerClientMock.Object,
            _dbContextMock.Object,
            _publishEndpointMock.Object,
            loggerMock.Object);
    }

    [Fact]
    public async Task Consume_HappyPath_ShouldPrintAndMarkPrinted()
    {
        // Arrange
        var printJob = PrintJob.Create("idem-001", _testPrinter.Id, "^XA^XZ", 1, Guid.NewGuid(), "test-user");

        SetupPrintJobLookup(printJob);
        SetupPrinterLookup(_testPrinter);

        var message = CreateMessage(printJob);
        var consumeContext = CreateConsumeContext(message);

        // Act
        await _consumer.Consume(consumeContext);

        // Assert
        printJob.Status.Should().Be(PrintJobStatus.Printed);
        printJob.PrintedAtUtc.Should().NotBeNull();

        _printerClientMock.Verify(
            x => x.SendZplAsync(_testPrinter, "^XA^XZ", It.IsAny<CancellationToken>()),
            Times.Once);

        _publishEndpointMock.Verify(
            x => x.Publish(
                It.IsAny<QrPrintCompletedIntegrationEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_TransientFailure_ShouldMarkFailedRetryingAndThrow()
    {
        // Arrange
        var printJob = PrintJob.Create("idem-002", _testPrinter.Id, "^XA^XZ", 1, Guid.NewGuid(), "test-user");

        SetupPrintJobLookup(printJob);
        SetupPrinterLookup(_testPrinter);

        _printerClientMock
            .Setup(x => x.SendZplAsync(It.IsAny<Printer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TransientPrinterException(_testPrinter.Id.ToString(), "Printer offline"));

        var message = CreateMessage(printJob);
        var consumeContext = CreateConsumeContext(message);

        // Act
        var act = () => _consumer.Consume(consumeContext);

        // Assert — should rethrow for MassTransit retry
        await act.Should().ThrowAsync<TransientPrinterException>()
            .WithMessage("Printer offline");

        printJob.Status.Should().Be(PrintJobStatus.FailedRetrying);
        printJob.FailCount.Should().Be(1);
        printJob.LastErrorCode.Should().Be("TRANSIENT");
    }

    [Fact]
    public async Task Consume_PermanentFailure_ShouldDeadLetterAndNotThrow()
    {
        // Arrange
        var printJob = PrintJob.Create("idem-003", _testPrinter.Id, "^XA^XZ", 1, Guid.NewGuid(), "test-user");

        SetupPrintJobLookup(printJob);
        SetupPrinterLookup(_testPrinter);

        _printerClientMock
            .Setup(x => x.SendZplAsync(It.IsAny<Printer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PermanentPrinterException(_testPrinter.Id.ToString(), "Printer disabled"));

        var message = CreateMessage(printJob);
        var consumeContext = CreateConsumeContext(message);

        // Act — should NOT throw (permanent = dead-letter)
        await _consumer.Consume(consumeContext);

        // Assert
        printJob.Status.Should().Be(PrintJobStatus.DeadLettered);
        printJob.LastErrorCode.Should().Be("PERMANENT");

        _publishEndpointMock.Verify(
            x => x.Publish(
                It.IsAny<QrPrintFailedIntegrationEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_AlreadyPrinted_ShouldSkipDuplicate()
    {
        // Arrange — create a job that is already printed
        var printJob = PrintJob.Create("idem-004", _testPrinter.Id, "^XA^XZ", 1, Guid.NewGuid(), "test-user");
        printJob.MarkDispatching();
        printJob.MarkPrinted();

        SetupPrintJobLookup(printJob);

        var message = CreateMessage(printJob);
        var consumeContext = CreateConsumeContext(message);

        // Act
        await _consumer.Consume(consumeContext);

        // Assert — should NOT print again
        _printerClientMock.Verify(
            x => x.SendZplAsync(It.IsAny<Printer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _publishEndpointMock.Verify(
            x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PrintJobNotFound_ShouldSkipGracefully()
    {
        // Arrange — empty DB
        SetupEmptyPrintJobs();

        var message = new QrPrintRequestedIntegrationEvent
        {
            CorrelationId = Guid.NewGuid(),
            RequestedBy = "test",
            PrintJobId = Guid.NewGuid(),
            PrinterId = _testPrinter.Id
        };

        var consumeContext = CreateConsumeContext(message);

        // Act — should not throw
        await _consumer.Consume(consumeContext);

        // Assert — no print attempt
        _printerClientMock.Verify(
            x => x.SendZplAsync(It.IsAny<Printer>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PrinterNotFound_ShouldDeadLetter()
    {
        // Arrange
        var printJob = PrintJob.Create("idem-005", _testPrinter.Id, "^XA^XZ", 1, Guid.NewGuid(), "test-user");

        SetupPrintJobLookup(printJob);
        SetupEmptyPrinters();

        var message = CreateMessage(printJob);
        var consumeContext = CreateConsumeContext(message);

        // Act
        await _consumer.Consume(consumeContext);

        // Assert
        printJob.Status.Should().Be(PrintJobStatus.DeadLettered);
        printJob.LastErrorCode.Should().Be("PRINTER_NOT_FOUND");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void SetupEmptyPrintJobs()
    {
        var empty = new List<PrintJob>().AsQueryable();
        _dbContextMock.Setup(x => x.PrintJobs).Returns(MockDbSetFactory.CreateMockDbSet(empty).Object);
    }

    private void SetupPrintJobLookup(PrintJob printJob)
    {
        var data = new List<PrintJob> { printJob }.AsQueryable();
        _dbContextMock.Setup(x => x.PrintJobs).Returns(MockDbSetFactory.CreateMockDbSet(data).Object);
    }

    private void SetupPrinterLookup(Printer printer)
    {
        var data = new List<Printer> { printer }.AsQueryable();
        _dbContextMock.Setup(x => x.Printers).Returns(MockDbSetFactory.CreateMockDbSet(data).Object);
    }

    private void SetupEmptyPrinters()
    {
        var empty = new List<Printer>().AsQueryable();
        _dbContextMock.Setup(x => x.Printers).Returns(MockDbSetFactory.CreateMockDbSet(empty).Object);
    }

    private static QrPrintRequestedIntegrationEvent CreateMessage(PrintJob printJob)
        => new()
        {
            CorrelationId = printJob.CorrelationId,
            RequestedBy = printJob.RequestedBy,
            PrintJobId = printJob.Id,
            PrinterId = printJob.PrinterId
        };

    private static ConsumeContext<QrPrintRequestedIntegrationEvent> CreateConsumeContext(
        QrPrintRequestedIntegrationEvent message)
    {
        var mock = new Mock<ConsumeContext<QrPrintRequestedIntegrationEvent>>();
        mock.Setup(x => x.Message).Returns(message);
        mock.Setup(x => x.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }
}
