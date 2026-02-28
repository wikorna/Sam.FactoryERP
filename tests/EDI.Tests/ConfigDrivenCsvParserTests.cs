using System.Text;
using System.Text.Json;
using EDI.Domain.Entities;
using EDI.Infrastructure.Parsers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EDI.Tests;

public class ConfigDrivenCsvParserTests
{
    private readonly ConfigDrivenCsvParser _parser;

    public ConfigDrivenCsvParserTests()
    {
        var logger = new Mock<ILogger<ConfigDrivenCsvParser>>();
        _parser = new ConfigDrivenCsvParser(logger.Object);
    }

    private static EdiFileTypeConfig CreateForecastConfig()
    {
        var config = EdiFileTypeConfig.Create(
            "SAP_FORECAST", "SAP MCP Forecast", "^F",
            delimiter: ",", hasHeaderRow: true, headerLineCount: 1, skipLines: 0);

        config.AddColumn(EdiColumnDefinition.Create(0, "ForecastId", "String", isRequired: true, maxLength: 50));
        config.AddColumn(EdiColumnDefinition.Create(1, "ItemCode", "String", isRequired: true, maxLength: 50));
        config.AddColumn(EdiColumnDefinition.Create(2, "Description", "String", isRequired: false, maxLength: 255));
        config.AddColumn(EdiColumnDefinition.Create(3, "Quantity", "Decimal", isRequired: true));
        config.AddColumn(EdiColumnDefinition.Create(4, "UoM", "String", isRequired: false, maxLength: 20));
        config.AddColumn(EdiColumnDefinition.Create(5, "DueDate", "Date", isRequired: true));

        return config;
    }

    [Fact]
    public async Task ParseAsyncShouldSkipHeaderRowAndParseDataRows()
    {
        // Arrange
        var csv = "ForecastId,ItemCode,Description,Quantity,UoM,DueDate\n" +
                  "FC001,ITEM-A,Widget A,100.5,EA,2026-03-01\n" +
                  "FC002,ITEM-B,Widget B,200,KG,2026-04-15\n";

        var config = CreateForecastConfig();
        var jobId = Guid.NewGuid();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, jobId, config, CancellationToken.None))
            rows.Add(row);

        // Assert
        rows.Should().HaveCount(2);
        rows[0].RowIndex.Should().Be(1);
        rows[0].JobId.Should().Be(jobId);
        rows[0].FileTypeCode.Should().Be("SAP_FORECAST");
        rows[0].IsSelected.Should().BeTrue();
        rows[0].IsValid.Should().BeTrue();

        var parsed0 = JsonSerializer.Deserialize<Dictionary<string, string?>>(rows[0].ParsedColumnsJson)!;
        parsed0["ForecastId"].Should().Be("FC001");
        parsed0["ItemCode"].Should().Be("ITEM-A");
        parsed0["Quantity"].Should().Be("100.5");
        parsed0["DueDate"].Should().Be("2026-03-01");

        var parsed1 = JsonSerializer.Deserialize<Dictionary<string, string?>>(rows[1].ParsedColumnsJson)!;
        parsed1["ForecastId"].Should().Be("FC002");
        parsed1["ItemCode"].Should().Be("ITEM-B");
    }

    [Fact]
    public async Task ParseAsyncShouldHandleEmptyLines()
    {
        // Arrange
        var csv = "ForecastId,ItemCode,Description,Quantity,UoM,DueDate\n" +
                  "FC001,ITEM-A,Widget A,100,EA,2026-03-01\n" +
                  "\n" +
                  "FC002,ITEM-B,Widget B,200,KG,2026-04-15\n";

        var config = CreateForecastConfig();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, Guid.NewGuid(), config, CancellationToken.None))
            rows.Add(row);

        // Assert — empty line should be skipped
        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseAsyncShouldHandleMissingColumns()
    {
        // Arrange — row has fewer columns than config
        var csv = "ForecastId,ItemCode,Description,Quantity,UoM,DueDate\n" +
                  "FC001,ITEM-A\n";

        var config = CreateForecastConfig();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, Guid.NewGuid(), config, CancellationToken.None))
            rows.Add(row);

        // Assert
        rows.Should().HaveCount(1);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(rows[0].ParsedColumnsJson)!;
        parsed["ForecastId"].Should().Be("FC001");
        parsed["ItemCode"].Should().Be("ITEM-A");
        parsed["Description"].Should().BeNull();
        parsed["Quantity"].Should().BeNull();
    }

    [Fact]
    public async Task ParseAsyncShouldHandleQuotedFields()
    {
        // Arrange
        var csv = "ForecastId,ItemCode,Description,Quantity,UoM,DueDate\n" +
                  "FC001,ITEM-A,\"Widget, Deluxe\",100,EA,2026-03-01\n";

        var config = CreateForecastConfig();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, Guid.NewGuid(), config, CancellationToken.None))
            rows.Add(row);

        // Assert
        rows.Should().HaveCount(1);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(rows[0].ParsedColumnsJson)!;
        parsed["Description"].Should().Be("Widget, Deluxe");
    }

    [Fact]
    public async Task ParseAsyncShouldSkipMetadataLinesWhenSkipLinesSet()
    {
        // Arrange — 2 skip lines + 1 header
        var config = EdiFileTypeConfig.Create(
            "SAP_CUSTOM", "Custom", "^X",
            delimiter: ",", hasHeaderRow: true, headerLineCount: 1, skipLines: 2);

        config.AddColumn(EdiColumnDefinition.Create(0, "Col1"));
        config.AddColumn(EdiColumnDefinition.Create(1, "Col2"));

        var csv = "META LINE 1\nMETA LINE 2\nCol1,Col2\nA,B\nC,D\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, Guid.NewGuid(), config, CancellationToken.None))
            rows.Add(row);

        // Assert — should skip 2 meta + 1 header = 3 lines, parse 2 data rows
        rows.Should().HaveCount(2);
        var parsed0 = JsonSerializer.Deserialize<Dictionary<string, string?>>(rows[0].ParsedColumnsJson)!;
        parsed0["Col1"].Should().Be("A");
        parsed0["Col2"].Should().Be("B");
    }

    [Fact]
    public async Task ParseAsyncShouldHandlePipeDelimiter()
    {
        // Arrange
        var config = EdiFileTypeConfig.Create(
            "PIPE_TEST", "Pipe Test", "^T",
            delimiter: "|", hasHeaderRow: true, headerLineCount: 1);

        config.AddColumn(EdiColumnDefinition.Create(0, "Col1"));
        config.AddColumn(EdiColumnDefinition.Create(1, "Col2"));
        config.AddColumn(EdiColumnDefinition.Create(2, "Col3"));

        var csv = "Col1|Col2|Col3\nA|B|C\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, Guid.NewGuid(), config, CancellationToken.None))
            rows.Add(row);

        // Assert
        rows.Should().HaveCount(1);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(rows[0].ParsedColumnsJson)!;
        parsed["Col1"].Should().Be("A");
        parsed["Col2"].Should().Be("B");
        parsed["Col3"].Should().Be("C");
    }

    [Fact]
    public async Task ParseAsyncWithNoHeaderRowShouldStartFromFirstLine()
    {
        // Arrange
        var config = EdiFileTypeConfig.Create(
            "NO_HEADER", "No Header", "^N",
            hasHeaderRow: false, headerLineCount: 0);

        config.AddColumn(EdiColumnDefinition.Create(0, "Col1"));
        config.AddColumn(EdiColumnDefinition.Create(1, "Col2"));

        var csv = "A,B\nC,D\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<EdiStagingRow>();
        await foreach (var row in _parser.ParseAsync(stream, Guid.NewGuid(), config, CancellationToken.None))
            rows.Add(row);

        // Assert
        rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task ReadRawLinesAsyncShouldReturnFirstNLines()
    {
        // Arrange
        var csv = "Line1\nLine2\nLine3\nLine4\nLine5\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var lines = await ConfigDrivenCsvParser.ReadRawLinesAsync(stream, 3, CancellationToken.None);

        // Assert
        lines.Should().HaveCount(3);
        lines[0].Should().Be("Line1");
        lines[2].Should().Be("Line3");
    }
}
