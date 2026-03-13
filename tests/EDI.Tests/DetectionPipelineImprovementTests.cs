using System.Text;
using EDI.Application.Utilities;
using EDI.Domain.Enums;
using EDI.Infrastructure.Detection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace EDI.Tests;

public sealed class DetectionPipelineImprovementTests : IDisposable
{
    private readonly string _schemaDir;

    public DetectionPipelineImprovementTests()
    {
        _schemaDir = Path.Combine(Path.GetTempPath(), $"edi_improve_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_schemaDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_schemaDir))
            Directory.Delete(_schemaDir, true);
    }

    private void WriteSchemaFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_schemaDir, fileName), content, Encoding.UTF8);

    private static MemoryStream ToStream(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    // ── SplitLine RFC 4180 ────────────────────────────────────────────────────

    [Fact]
    public void SplitLineEscapedDoubleQuotesShouldParse()
    {
        var line = "He said,\"She said \"\"hello\"\"\",Done";
        var fields = CsvReaderUtility.SplitLine(line, ',');
        fields.Should().HaveCount(3);
        fields[0].Should().Be("He said");
        fields[1].Should().Be("She said \"hello\"");
        fields[2].Should().Be("Done");
    }

    [Fact]
    public void SplitLineQuotedFieldWithCommaShouldParse()
    {
        var line = "ITEM1,\"QUALITY PUBLISHING CO.,LTD.\",THB";
        var fields = CsvReaderUtility.SplitLine(line, ',');
        fields.Should().HaveCount(3);
        fields[1].Should().Be("QUALITY PUBLISHING CO.,LTD.");
    }

    [Fact]
    public void SplitLineEmptyQuotedFieldShouldParse()
    {
        var line = "A,\"\",C";
        var fields = CsvReaderUtility.SplitLine(line, ',');
        fields.Should().HaveCount(3);
        fields[1].Should().Be(string.Empty);
    }

    [Fact]
    public void SplitLineTrailingCommaShouldProduceExtraField()
    {
        var line = "A,B,C,";
        var fields = CsvReaderUtility.SplitLine(line, ',');
        fields.Should().HaveCount(4);
        fields[3].Should().Be(string.Empty);
    }

    // ── Duplicate Column Detection ────────────────────────────────────────────

    [Fact]
    public async Task DuplicateHeadersShouldProduceWarning()
    {
        WriteSchemaFile("forecast.v1.json", MinimalForecastSchema);
        var detector = BuildDetector();
        var csv = "meta1\nmeta2\nItem No.,Description,UM,Vendor,Material Group,Type,Plant,Description\nITEM1,DESC,PC,V1,GRP,EXP,1001,DUPE\n";
        using var stream = ToStream(csv);
        var result = await detector.DetectAsync("F12345.CSV", stream, stream.Length, null, CancellationToken.None);
        result.Detected.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    // ── Schema Auto-Discovery ─────────────────────────────────────────────────

    [Fact]
    public void SchemaRegistryShouldAutoDiscoverAllJsonFiles()
    {
        WriteSchemaFile("forecast.v1.json", MinimalForecastSchema);
        WriteSchemaFile("purchaseorder.v1.json", MinimalPurchaseOrderSchema);
        var logger = new Mock<ILogger<EdiSchemaRegistry>>();
        var registry = new EdiSchemaRegistry(logger.Object, _schemaDir);
        registry.Count.Should().Be(2);
        registry.RegisteredKeys.Should().Contain("Forecast");
        registry.RegisteredKeys.Should().Contain("PurchaseOrder");
    }

    [Fact]
    public void SchemaRegistryEmptyDirShouldReturnZero()
    {
        var emptyDir = Path.Combine(_schemaDir, "empty");
        Directory.CreateDirectory(emptyDir);
        var logger = new Mock<ILogger<EdiSchemaRegistry>>();
        var registry = new EdiSchemaRegistry(logger.Object, emptyDir);
        registry.Count.Should().Be(0);
    }

    [Fact]
    public void SchemaRegistryMissingDirShouldReturnZero()
    {
        var logger = new Mock<ILogger<EdiSchemaRegistry>>();
        var registry = new EdiSchemaRegistry(logger.Object, Path.Combine(_schemaDir, "no_exist"));
        registry.Count.Should().Be(0);
    }

    [Fact]
    public async Task SchemaProviderShouldAutoDiscoverAllJsonFiles()
    {
        WriteSchemaFile("forecast.v1.json", MinimalForecastSchema);
        WriteSchemaFile("purchaseorder.v1.json", MinimalPurchaseOrderSchema);
        var logger = new Mock<ILogger<JsonEdiSchemaProvider>>();
        var provider = new JsonEdiSchemaProvider(logger.Object, _schemaDir);
        var forecast = await provider.GetSchemaAsync(EdiFileType.Forecast, CancellationToken.None);
        var po = await provider.GetSchemaAsync(EdiFileType.PurchaseOrder, CancellationToken.None);
        forecast.Should().NotBeNull();
        po.Should().NotBeNull();
    }

    [Fact]
    public async Task SchemaProviderShouldSkipInvalidJsonFiles()
    {
        WriteSchemaFile("forecast.v1.json", MinimalForecastSchema);
        WriteSchemaFile("broken.json", "not valid json");
        var logger = new Mock<ILogger<JsonEdiSchemaProvider>>();
        var provider = new JsonEdiSchemaProvider(logger.Object, _schemaDir);
        var forecast = await provider.GetSchemaAsync(EdiFileType.Forecast, CancellationToken.None);
        forecast.Should().NotBeNull();
    }

    [Fact]
    public async Task SchemaProviderMissingDirShouldReturnNull()
    {
        var logger = new Mock<ILogger<JsonEdiSchemaProvider>>();
        var provider = new JsonEdiSchemaProvider(logger.Object, Path.Combine(_schemaDir, "no_exist"));
        var result = await provider.GetSchemaAsync(EdiFileType.Forecast, CancellationToken.None);
        result.Should().BeNull();
    }

    // ── BOM Handling ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Utf8BomShouldDetectSuccessfully()
    {
        WriteSchemaFile("forecast.v1.json", MinimalForecastSchema);
        var detector = BuildDetector();
        var csv = "meta1\nmeta2\nItem No.,Description,UM,Vendor,Material Group,Type,Plant\nITEM1,DESC,PC,V1,GRP,EXP,1001\n";
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var csvBytes = Encoding.UTF8.GetBytes(csv);
        var withBom = new byte[bom.Length + csvBytes.Length];
        bom.CopyTo(withBom, 0);
        csvBytes.CopyTo(withBom, bom.Length);
        using var stream = new MemoryStream(withBom);
        var result = await detector.DetectAsync("F12345.CSV", stream, stream.Length, null, CancellationToken.None);
        result.Detected.Should().BeTrue();
        result.FileType.Should().Be(EdiFileType.Forecast);
    }

    // ── Windows Line Endings ──────────────────────────────────────────────────

    [Fact]
    public async Task WindowsCrLfShouldDetectSuccessfully()
    {
        WriteSchemaFile("forecast.v1.json", MinimalForecastSchema);
        var detector = BuildDetector();
        var csv = "meta1\r\nmeta2\r\nItem No.,Description,UM,Vendor,Material Group,Type,Plant\r\nITEM1,DESC,PC,V1,GRP,EXP,1001\r\n";
        using var stream = ToStream(csv);
        var result = await detector.DetectAsync("F12345.CSV", stream, stream.Length, null, CancellationToken.None);
        result.Detected.Should().BeTrue();
    }

    // ── EdiSchemaDto.Validate() ───────────────────────────────────────────────

    [Fact]
    public void ValidateEmptySchemaKeyShouldReturnIssue()
    {
        var dto = new EdiSchemaDto { SchemaKey = "", RequiredHeaders = ["Col1"] };
        dto.Validate().Should().Contain(i => i.Contains("SchemaKey"));
    }

    [Fact]
    public void ValidateNoRequiredHeadersShouldReturnIssue()
    {
        var dto = new EdiSchemaDto { SchemaKey = "Test", RequiredHeaders = [] };
        dto.Validate().Should().Contain(i => i.Contains("required header"));
    }

    [Fact]
    public void ValidateSegmentMarkersNoHeaderRowMarkerShouldReturnIssue()
    {
        var dto = new EdiSchemaDto { SchemaKey = "Test", RequiredHeaders = ["Col1"], HasSegmentMarkers = true, HeaderRowMarker = null };
        dto.Validate().Should().Contain(i => i.Contains("HeaderRowMarker"));
    }

    [Fact]
    public void ValidateNegativeSkipLinesShouldReturnIssue()
    {
        var dto = new EdiSchemaDto { SchemaKey = "Test", RequiredHeaders = ["Col1"], SkipLines = -1 };
        dto.Validate().Should().Contain(i => i.Contains("SkipLines"));
    }

    [Fact]
    public void ValidateDuplicateRequiredHeadersShouldReturnIssue()
    {
        var dto = new EdiSchemaDto { SchemaKey = "Test", RequiredHeaders = ["Col1", "Col1"] };
        dto.Validate().Should().Contain(i => i.Contains("Duplicate"));
    }

    [Fact]
    public void ValidateValidSchemaShouldReturnNoIssues()
    {
        var dto = new EdiSchemaDto { SchemaKey = "Forecast", RequiredHeaders = ["Item No.", "Description", "UM"], SkipLines = 2 };
        dto.Validate().Should().BeEmpty();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private CsvEdiFileDetector BuildDetector()
    {
        var schemaLogger = new Mock<ILogger<JsonEdiSchemaProvider>>();
        var schemaProvider = new JsonEdiSchemaProvider(schemaLogger.Object, _schemaDir);
        var detectorLogger = new Mock<ILogger<CsvEdiFileDetector>>();
        return new CsvEdiFileDetector(schemaProvider, detectorLogger.Object);
    }

    private const string MinimalForecastSchema = "{\"schemaKey\":\"Forecast\",\"schemaVersion\":\"v1\",\"displayName\":\"SAP MCP Forecast\",\"hasSegmentMarkers\":false,\"skipLines\":2,\"metadataRowMarkers\":[],\"metadataFields\":{},\"requiredHeaders\":[\"Item No.\",\"Description\",\"UM\",\"Vendor\",\"Material Group\",\"Type\",\"Plant\"],\"optionalHeaders\":[],\"headerAliases\":{}}";

    private const string MinimalPurchaseOrderSchema = "{\"schemaKey\":\"PurchaseOrder\",\"schemaVersion\":\"v1\",\"displayName\":\"SAP MCP Purchase Order\",\"hasSegmentMarkers\":true,\"headerRowMarker\":\"H1\",\"segmentMarkerColumn\":0,\"skipLines\":0,\"metadataRowMarkers\":[\"S1\",\"S2\",\"S3\"],\"metadataFields\":{},\"requiredHeaders\":[\"PO Status\",\"PO No.\",\"PO Item\",\"Item No.\",\"Description\",\"Due Qty\",\"UM\",\"Due Date\",\"Unit Price\",\"Amount\",\"Currency\"],\"optionalHeaders\":[],\"headerAliases\":{}}";
}
