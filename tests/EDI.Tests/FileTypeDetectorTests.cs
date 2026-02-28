using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Domain.Entities;
using FactoryERP.Abstractions.Caching;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EDI.Tests;

public class FileTypeDetectorTests
{
    private readonly Mock<IEdiFileTypeConfigRepository> _configRepo;
    private readonly Mock<ICacheService> _cache;
    private readonly Infrastructure.FileTypeDetector _detector;

    public FileTypeDetectorTests()
    {
        _configRepo = new Mock<IEdiFileTypeConfigRepository>();
        _cache = new Mock<ICacheService>();
        var logger = new Mock<ILogger<Infrastructure.FileTypeDetector>>();

        // Setup cache to always call the factory (pass-through)
        _cache.Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, ValueTask<IReadOnlyList<EdiFileTypeConfig>>>>(),
                It.IsAny<CacheEntrySettings?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, ValueTask<IReadOnlyList<EdiFileTypeConfig>>>, CacheEntrySettings?, CancellationToken>(
                async (_, factory, _, ct) => await factory(ct));

        _detector = new Infrastructure.FileTypeDetector(_configRepo.Object, _cache.Object, logger.Object);
    }

    private static List<EdiFileTypeConfig> CreateDefaultConfigs()
    {
        return
        [
            EdiFileTypeConfig.Create("SAP_FORECAST", "SAP Forecast", "^F", detectionPriority: 10),
            EdiFileTypeConfig.Create("SAP_PO", "SAP Purchase Order", "^P", detectionPriority: 20)
        ];
    }

    [Theory]
    [InlineData("F20260301_forecast.csv", "SAP_FORECAST")]
    [InlineData("FORECAST_2026.csv", "SAP_FORECAST")]
    [InlineData("P20260301_po.csv", "SAP_PO")]
    [InlineData("PO_2026.csv", "SAP_PO")]
    public async Task DetectAsyncShouldMatchFilenamePrefixPattern(string fileName, string expectedTypeCode)
    {
        // Arrange
        _configRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDefaultConfigs());

        // Act
        var result = await _detector.DetectAsync(fileName, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.FileTypeCode.Should().Be(expectedTypeCode);
    }

    [Fact]
    public async Task DetectAsyncShouldReturnNullWhenNoPatternMatches()
    {
        // Arrange
        _configRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDefaultConfigs());

        // Act
        var result = await _detector.DetectAsync("X_unknown.csv", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task DetectAsyncShouldRespectDetectionPriority()
    {
        // Arrange — both patterns could match "FP_file.csv" if we had overlapping patterns
        // but priority ordering ensures the first match wins
        var configs = new List<EdiFileTypeConfig>
        {
            EdiFileTypeConfig.Create("TYPE_A", "Type A", "^F", detectionPriority: 10),
            EdiFileTypeConfig.Create("TYPE_B", "Type B", "^FP", detectionPriority: 20)
        };

        _configRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _detector.DetectAsync("FP_file.csv", CancellationToken.None);

        // Assert — TYPE_A has higher priority (lower number), so it matches first
        result.Should().NotBeNull();
        result!.FileTypeCode.Should().Be("TYPE_A");
    }

    [Fact]
    public async Task DetectAsyncShouldBeCaseInsensitive()
    {
        // Arrange
        _configRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDefaultConfigs());

        // Act
        var result = await _detector.DetectAsync("f_lowercase_forecast.csv", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.FileTypeCode.Should().Be("SAP_FORECAST");
    }

    [Fact]
    public async Task GetAllActiveConfigsAsyncShouldReturnCachedConfigs()
    {
        // Arrange
        var configs = CreateDefaultConfigs();
        _configRepo.Setup(r => r.GetAllActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(configs);

        // Act
        var result = await _detector.GetAllActiveConfigsAsync(CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        _cache.Verify(c => c.GetOrCreateAsync(
            It.IsAny<string>(),
            It.IsAny<Func<CancellationToken, ValueTask<IReadOnlyList<EdiFileTypeConfig>>>>(),
            It.IsAny<CacheEntrySettings?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
