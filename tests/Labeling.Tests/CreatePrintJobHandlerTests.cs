using FluentAssertions;
using Labeling.Application.Features.PrintJobs;
using Labeling.Application.Interfaces;
using Labeling.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Labeling.Tests;

public sealed class CreatePrintJobHandlerTests
{
    private readonly Mock<ILabelingDbContext> _dbContextMock;
    private readonly Mock<IPublishEndpoint> _publishEndpointMock;
    private readonly CreatePrintJobHandler _handler;

    private readonly Printer _testPrinter;

    public CreatePrintJobHandlerTests()
    {
        _dbContextMock = new Mock<ILabelingDbContext>();
        _publishEndpointMock = new Mock<IPublishEndpoint>();

        _testPrinter = Printer.Create("printer-dept-a", PrinterProtocol.Raw9100, "192.168.1.101", 9100);

        _handler = new CreatePrintJobHandler(_dbContextMock.Object, _publishEndpointMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldCreatePrintJob_WithQueuedStatus()
    {
        // Arrange
        SetupEmptyPrintJobs();
        SetupPrinterLookup(_testPrinter);

        var command = new CreatePrintJobCommand(
            IdempotencyKey: "idem-001",
            PrinterId: _testPrinter.Id,
            ZplContent: "^XA^FO50,50^ADN,36,20^FDHello^FS^XZ",
            Copies: 1,
            RequestedBy: "test-user");


        // We need to capture the Add call on the actual DbSet returned
        var printJobsDbSet = MockDbSetFactory.CreateMockDbSet(new List<PrintJob>().AsQueryable());
        _dbContextMock.Setup(x => x.PrintJobs).Returns(printJobsDbSet.Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PrintJobId.Should().NotBeEmpty();
        result.AlreadyExisted.Should().BeFalse();

        printJobsDbSet.Verify(
            x => x.Add(It.Is<PrintJob>(j =>
                j.Status == PrintJobStatus.Queued &&
                j.IdempotencyKey == "idem-001" &&
                j.PrinterId == _testPrinter.Id &&
                j.RequestedBy == "test-user" &&
                j.CorrelationId != Guid.Empty)),
            Times.Once);

        _dbContextMock.Verify(
            x => x.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishQrPrintRequestedEvent()
    {
        // Arrange
        SetupEmptyPrintJobs();
        SetupPrinterLookup(_testPrinter);

        var command = new CreatePrintJobCommand(
            IdempotencyKey: "idem-002",
            PrinterId: _testPrinter.Id,
            ZplContent: "^XA^FO50,50^ADN,36,20^FDTest^FS^XZ",
            Copies: 2,
            RequestedBy: "integration-test");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _publishEndpointMock.Verify(
            x => x.Publish(
                It.IsAny<FactoryERP.Contracts.Labeling.QrPrintRequestedIntegrationEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateIdempotencyKey_ShouldReturnExistingJob()
    {
        // Arrange
        var existing = PrintJob.Create("idem-dup", _testPrinter.Id, "^XA^XZ", 1, Guid.NewGuid(), "user");
        SetupPrintJobLookup(existing);

        var command = new CreatePrintJobCommand(
            IdempotencyKey: "idem-dup",
            PrinterId: _testPrinter.Id,
            ZplContent: "^XA^XZ",
            Copies: 1,
            RequestedBy: "user");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.PrintJobId.Should().Be(existing.Id);
        result.AlreadyExisted.Should().BeTrue();

        _publishEndpointMock.Verify(
            x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
}
