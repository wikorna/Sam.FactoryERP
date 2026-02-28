using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Application.Features.ValidateEdiFile;
using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.Entities;
using EDI.Domain.ValueObjects;
using FactoryERP.Abstractions.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EDI.Tests;

public class ValidateEdiFileCommandHandlerTests
{
    private readonly Mock<IEdiFileJobRepository> _jobRepo;
    private readonly Mock<IEdiFileTypeConfigRepository> _configRepo;
    private readonly Mock<IStagingRepository> _staging;
    private readonly Mock<ICacheService> _cache;
    private readonly ValidateEdiFileCommandHandler _handler;

    public ValidateEdiFileCommandHandlerTests()
    {
        _jobRepo = new Mock<IEdiFileJobRepository>();
        _configRepo = new Mock<IEdiFileTypeConfigRepository>();
        _staging = new Mock<IStagingRepository>();
        _cache = new Mock<ICacheService>();
        var logger = new Mock<ILogger<ValidateEdiFileCommandHandler>>();

        _handler = new ValidateEdiFileCommandHandler(
            _jobRepo.Object, _configRepo.Object, _staging.Object, _cache.Object, logger.Object);
    }

    private static EdiFileJob CreateJob(string? fileTypeCode = "SAP_FORECAST")
    {
        var job = EdiFileJob.CreateReceived(
            Guid.NewGuid(), "TEST", "F_test.csv", "/tmp/test.csv",
            1024, "sha256", EdiFormat.Csv, EdiSchemaVersion.V1, fileTypeCode);
        job.MarkParsing();
        job.MarkParsed(2);
        return job;
    }

    private static EdiFileTypeConfig CreateForecastConfig()
    {
        var config = EdiFileTypeConfig.Create("SAP_FORECAST", "Forecast", "^F");
        config.AddColumn(EdiColumnDefinition.Create(0, "ForecastId", "String", isRequired: true, maxLength: 50));
        config.AddColumn(EdiColumnDefinition.Create(1, "ItemCode", "String", isRequired: true, maxLength: 50));
        config.AddColumn(EdiColumnDefinition.Create(2, "Quantity", "Decimal", isRequired: true));
        config.AddColumn(EdiColumnDefinition.Create(3, "DueDate", "Date", isRequired: true));
        return config;
    }

    [Fact]
    public async Task HandleAllRowsValidShouldReturnZeroInvalidRows()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateForecastConfig();

        _jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _configRepo.Setup(r => r.GetByCodeAsync("SAP_FORECAST", It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var validRow = new EdiStagingRow
        {
            Id = Guid.NewGuid(), JobId = job.Id, FileTypeCode = "SAP_FORECAST", RowIndex = 1,
            RawLine = "FC001,ITEM-A,100,2026-03-01",
            ParsedColumnsJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["ForecastId"] = "FC001", ["ItemCode"] = "ITEM-A",
                ["Quantity"] = "100", ["DueDate"] = "2026-03-01"
            }),
            IsValid = true, IsSelected = true
        };

        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 1, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow> { validRow });
        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 2, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow>());
        _staging.Setup(s => s.GetStagingRowCountAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new ValidateEdiFileCommand(job.Id), CancellationToken.None);

        // Assert
        result.ValidRows.Should().Be(1);
        result.InvalidRows.Should().Be(0);
    }

    [Fact]
    public async Task HandleRowWithMissingRequiredFieldShouldMarkInvalid()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateForecastConfig();

        _jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _configRepo.Setup(r => r.GetByCodeAsync("SAP_FORECAST", It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var invalidRow = new EdiStagingRow
        {
            Id = Guid.NewGuid(), JobId = job.Id, FileTypeCode = "SAP_FORECAST", RowIndex = 1,
            RawLine = ",ITEM-A,100,2026-03-01",
            ParsedColumnsJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["ForecastId"] = null, ["ItemCode"] = "ITEM-A",
                ["Quantity"] = "100", ["DueDate"] = "2026-03-01"
            }),
            IsValid = true, IsSelected = true
        };

        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 1, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow> { invalidRow });
        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 2, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow>());
        _staging.Setup(s => s.GetStagingRowCountAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new ValidateEdiFileCommand(job.Id), CancellationToken.None);

        // Assert
        result.InvalidRows.Should().Be(1);
        result.ValidRows.Should().Be(0);

        // Verify the row was updated with validation errors
        _staging.Verify(s => s.UpdateRowValidationAsync(
            It.Is<EdiStagingRow>(r => !r.IsValid && r.ValidationErrorsJson != null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleRowWithInvalidDecimalShouldMarkInvalid()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateForecastConfig();

        _jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _configRepo.Setup(r => r.GetByCodeAsync("SAP_FORECAST", It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var invalidRow = new EdiStagingRow
        {
            Id = Guid.NewGuid(), JobId = job.Id, FileTypeCode = "SAP_FORECAST", RowIndex = 1,
            RawLine = "FC001,ITEM-A,NOT_A_NUMBER,2026-03-01",
            ParsedColumnsJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["ForecastId"] = "FC001", ["ItemCode"] = "ITEM-A",
                ["Quantity"] = "NOT_A_NUMBER", ["DueDate"] = "2026-03-01"
            }),
            IsValid = true, IsSelected = true
        };

        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 1, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow> { invalidRow });
        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 2, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow>());
        _staging.Setup(s => s.GetStagingRowCountAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new ValidateEdiFileCommand(job.Id), CancellationToken.None);

        // Assert
        result.InvalidRows.Should().Be(1);
    }

    [Fact]
    public async Task HandleRowExceedingMaxLengthShouldMarkInvalid()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateForecastConfig();

        _jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _configRepo.Setup(r => r.GetByCodeAsync("SAP_FORECAST", It.IsAny<CancellationToken>())).ReturnsAsync(config);

        var longValue = new string('X', 60); // Exceeds maxLength of 50

        var invalidRow = new EdiStagingRow
        {
            Id = Guid.NewGuid(), JobId = job.Id, FileTypeCode = "SAP_FORECAST", RowIndex = 1,
            RawLine = $"{longValue},ITEM-A,100,2026-03-01",
            ParsedColumnsJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["ForecastId"] = longValue, ["ItemCode"] = "ITEM-A",
                ["Quantity"] = "100", ["DueDate"] = "2026-03-01"
            }),
            IsValid = true, IsSelected = true
        };

        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 1, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow> { invalidRow });
        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 2, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow>());
        _staging.Setup(s => s.GetStagingRowCountAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new ValidateEdiFileCommand(job.Id), CancellationToken.None);

        // Assert
        result.InvalidRows.Should().Be(1);
    }

    [Fact]
    public async Task HandleShouldTransitionJobToValidated()
    {
        // Arrange
        var job = CreateJob();
        var config = CreateForecastConfig();

        _jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);
        _configRepo.Setup(r => r.GetByCodeAsync("SAP_FORECAST", It.IsAny<CancellationToken>())).ReturnsAsync(config);
        _staging.Setup(s => s.GetStagingRowsAsync(job.Id, 1, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EdiStagingRow>());
        _staging.Setup(s => s.GetStagingRowCountAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _handler.Handle(new ValidateEdiFileCommand(job.Id), CancellationToken.None);

        // Assert — job should be saved twice: Validating and Validated
        _jobRepo.Verify(r => r.SaveAsync(It.IsAny<EdiFileJob>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleShouldThrowWhenJobHasNoFileTypeCode()
    {
        // Arrange
        var job = CreateJob(fileTypeCode: null);

        _jobRepo.Setup(r => r.GetAsync(job.Id, It.IsAny<CancellationToken>())).ReturnsAsync(job);

        // Act & Assert
        var act = () => _handler.Handle(new ValidateEdiFileCommand(job.Id), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no file type code*");
    }
}
