using System.Text;
using EDI.Domain.Enums;
using EDI.Domain.ValueObjects;
using EDI.Infrastructure.Detection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace EDI.Tests;

/// <summary>
/// Integration tests for <see cref="CsvEdiFileDetector"/> against realistic SAP MCP CSV content.
/// Uses the real <see cref="JsonEdiSchemaProvider"/> with schema JSON files.
/// </summary>
public sealed class CsvEdiFileDetectorIntegrationTests : IDisposable
{
    private readonly string _schemaDir;
    private readonly CsvEdiFileDetector _detector;

    public CsvEdiFileDetectorIntegrationTests()
    {
        // Create temp dir with real schema files
        _schemaDir = Path.Combine(Path.GetTempPath(), $"edi_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_schemaDir);

        // Copy schema content from the Infrastructure project
        WriteSchemaFile("forecast.v1.json", ForecastSchemaJson);
        WriteSchemaFile("purchaseorder.v1.json", PurchaseOrderSchemaJson);

        var schemaLogger = new Mock<ILogger<JsonEdiSchemaProvider>>();
        var schemaProvider = new JsonEdiSchemaProvider(schemaLogger.Object, _schemaDir);

        var detectorLogger = new Mock<ILogger<CsvEdiFileDetector>>();
        _detector = new CsvEdiFileDetector(schemaProvider, detectorLogger.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_schemaDir))
            Directory.Delete(_schemaDir, true);

        GC.SuppressFinalize(this);
    }

    private void WriteSchemaFile(string fileName, string content) =>
        File.WriteAllText(Path.Combine(_schemaDir, fileName), content, Encoding.UTF8);

    private static MemoryStream ToStream(string content) =>
        new(Encoding.UTF8.GetBytes(content));

    // ── Forecast tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task ForecastRealFormatShouldDetectSuccessfully()
    {
        // Arrange — matches actual F10042926010001.CSV structure
        var csv = """
                  Transmission Date,Transmission Time,Messge ID No,Number of Item,Forecast Year,Forecast Dai No. (Mass),Forecast Dai No. (Service)
                  05/01/2026,10:32:15,F10042926010001.CSV,004410,2025,202528.01,202521.05
                  Item No.,Description,UM,Vendor,Material Group,Mat.Grp.Desc.,Type,Plant,AprA,AprB,MayA,MayB,Total,Remark1
                  BC79F232H01,NOTICE,PC,100429,PR01,PRINTING PARTS,EXPORT,1001,0,0,0,0,369,,,
                  """;

        using var stream = ToStream(csv);

        // Act
        var result = await _detector.DetectAsync(
            "F10042926010001.CSV", stream, stream.Length, null, CancellationToken.None);

        // Assert
        result.Detected.Should().BeTrue();
        result.FileType.Should().Be(EdiFileType.Forecast);
        result.SchemaKey.Should().Be("Forecast");
        result.SchemaVersion.Should().Be("v1");
        result.DocumentNo.Should().Be("F10042926010001");
        result.FileTypeDisplayName.Should().Be("SAP MCP Forecast");

        // Verify metadata extraction
        result.Header.Should().ContainKey("meta:Transmission Date");
        result.Header!["meta:Transmission Date"].Should().Be("05/01/2026");
        result.Header.Should().ContainKey("meta:Transmission Time");
        result.Header["meta:Transmission Time"].Should().Be("10:32:15");
    }

    [Fact]
    public async Task ForecastWithExtraColumnsShouldDetectWithWarnings()
    {
        var csv = """
                  Transmission Date,Transmission Time
                  05/01/2026,10:32:15
                  Item No.,Description,UM,Vendor,Material Group,Type,Plant,CustomColumn
                  BC79F232H01,NOTICE,PC,100429,PR01,EXPORT,1001,EXTRA
                  """;

        using var stream = ToStream(csv);

        var result = await _detector.DetectAsync(
            "F12345.CSV", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeTrue();
        result.Warnings.Should().ContainSingle();
        result.Warnings[0].Should().Contain("CustomColumn");
    }

    [Fact]
    public async Task ForecastMissingRequiredColumnShouldFail()
    {
        // Missing "Vendor" and "Plant"
        var csv = """
                  Transmission Date,Transmission Time
                  05/01/2026,10:32:15
                  Item No.,Description,UM,Material Group,Type
                  BC79F232H01,NOTICE,PC,PR01,EXPORT
                  """;

        using var stream = ToStream(csv);

        var result = await _detector.DetectAsync(
            "F12345.CSV", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.HeaderMismatch);
        result.Errors[0].Message.Should().Contain("Vendor");
    }

    [Fact]
    public async Task ForecastTooFewLinesShouldFailWithHeaderMismatch()
    {
        // Only 1 line when schema expects skipLines=2 + header
        var csv = "Only one line here";

        using var stream = ToStream(csv);

        var result = await _detector.DetectAsync(
            "F12345.CSV", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.HeaderMismatch);
    }

    // ── Purchase Order tests ──────────────────────────────────────────────────

    [Fact]
    public async Task PurchaseOrderRealFormatShouldDetectSuccessfully()
    {
        // Arrange — matches actual P10042926020009.CSV structure
        var csv = """
                  S1,These Order require ROHS compliance product only,Transmission Date,07/02/2026,Transmission Time,19:02:41
                  S2,PO File,Number of Record,Supplier Code,Supplier Name,Contact Name,Manager Name
                  S3,P10042926020009.CSV,001863,100429,"QUALITY PUBLISHING CO.,LTD.",K.CHAYADA,MR. SHIMADA
                  H1,PO Status,PO No.,PO Item,Item No.,Description,BOI Name,Due Qty,UM,Due Date,Unit Price,Amount,Currency,Type of Parts,Production Month,Production Lot,Delivery Spot,PO Date,Delivery Type,PO Comment2,Ship By,Ship To,Terms of Trade,Payment term,Incharge Name,Last Status,Changes Note,Old value,New value
                  D1,P/O New,5100091949,00001,BT79C258K01,LABEL(D.P),STICKER LABEL,140,PC,11/02/2026,4.4600,624.40,THB,DO,032026,B,"KANAYAMA",07/02/2026,Dir Delivery,MASS,,,,Receive,Piyanuch,,,,
                  """;

        using var stream = ToStream(csv);

        var result = await _detector.DetectAsync(
            "P10042926020009.CSV", stream, stream.Length, null, CancellationToken.None);

        // Assert
        result.Detected.Should().BeTrue();
        result.FileType.Should().Be(EdiFileType.PurchaseOrder);
        result.SchemaKey.Should().Be("PurchaseOrder");
        result.SchemaVersion.Should().Be("v1");
        result.DocumentNo.Should().Be("P10042926020009");
        result.FileTypeDisplayName.Should().Be("SAP MCP Purchase Order");

        // Verify metadata extraction from S3 row
        result.Header.Should().ContainKey("meta:S3.SupplierCode");
        result.Header!["meta:S3.SupplierCode"].Should().Be("100429");
        result.Header.Should().ContainKey("meta:S3.SupplierName");

        // Verify S1 metadata
        result.Header.Should().ContainKey("meta:S1.TransmissionDate");
    }

    [Fact]
    public async Task PurchaseOrderMissingH1MarkerShouldFail()
    {
        // No H1 row in the file
        var csv = """
                  S1,ROHS notice
                  S2,Some header
                  S3,some data
                  D1,data row
                  """;

        using var stream = ToStream(csv);

        var result = await _detector.DetectAsync(
            "P12345.CSV", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.HeaderMismatch);
        result.Errors[0].Message.Should().Contain("H1");
    }

    [Fact]
    public async Task PurchaseOrderMissingRequiredHeadersShouldFail()
    {
        // H1 present but missing "Due Qty", "Currency"
        var csv = """
                  S1,ROHS
                  H1,PO Status,PO No.,PO Item,Item No.,Description,UM,Due Date,Unit Price,Amount
                  D1,P/O New,51000,00001,ITEM1,DESC,PC,11/02/2026,4.46,624.40
                  """;

        using var stream = ToStream(csv);

        var result = await _detector.DetectAsync(
            "P12345.CSV", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.HeaderMismatch);
        result.Errors[0].Message.Should().Contain("Due Qty");
    }

    // ── General tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task InvalidFilenamePrefixShouldFail()
    {
        using var stream = ToStream("data");

        var result = await _detector.DetectAsync(
            "X12345.CSV", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.InvalidFilename);
    }

    [Fact]
    public async Task NonCsvExtensionShouldFail()
    {
        using var stream = ToStream("data");

        var result = await _detector.DetectAsync(
            "F12345.txt", stream, stream.Length, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.InvalidExtension);
    }

    [Fact]
    public async Task EmptyFileShouldFail()
    {
        using var stream = new MemoryStream();

        var result = await _detector.DetectAsync(
            "F12345.CSV", stream, 0, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.EmptyFile);
    }

    [Fact]
    public async Task FileTooLargeShouldFail()
    {
        using var stream = ToStream("data");

        var result = await _detector.DetectAsync(
            "F12345.CSV", stream, 11L * 1024 * 1024, null, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.FileTooLarge);
    }

    [Theory]
    [InlineData("f12345.CSV", EdiFileType.Forecast)]
    [InlineData("F12345.csv", EdiFileType.Forecast)]
    [InlineData("p12345.CSV", EdiFileType.PurchaseOrder)]
    [InlineData("P12345.csv", EdiFileType.PurchaseOrder)]
    public async Task CaseInsensitiveFilenameShouldResolveCorrectly(string fileName, EdiFileType expected)
    {
        // Build minimal valid content for each type
        string csv;
        if (expected == EdiFileType.Forecast)
        {
            csv = """
                  meta1
                  meta2
                  Item No.,Description,UM,Vendor,Material Group,Type,Plant
                  ITEM1,DESC,PC,V1,GRP,EXP,1001
                  """;
        }
        else
        {
            csv = """
                  S1,ROHS
                  H1,PO Status,PO No.,PO Item,Item No.,Description,Due Qty,UM,Due Date,Unit Price,Amount,Currency
                  D1,New,51000,001,ITEM1,DESC,100,PC,11/02/2026,1.00,100.00,THB
                  """;
        }

        using var stream = ToStream(csv);
        var result = await _detector.DetectAsync(fileName, stream, stream.Length, null, CancellationToken.None);

        result.FileType.Should().Be(expected);
        result.Detected.Should().BeTrue();
    }

    // ── Schema JSON (embedded for test isolation) ─────────────────────────────

    private const string ForecastSchemaJson = """
        {
          "schemaKey": "Forecast",
          "schemaVersion": "v1",
          "fileType": "Forecast",
          "displayName": "SAP MCP Forecast",
          "filenamePrefixPattern": "^[Ff]",
          "delimiter": ",",
          "hasHeaderRow": true,
          "hasSegmentMarkers": false,
          "skipLines": 2,
          "metadataLineCount": 2,
          "metadataRowMarkers": [],
          "metadataFields": {
            "line0": ["_label:Transmission Date", "_label:Transmission Time", "_label:Messge ID No", "_label:Number of Item", "_label:Forecast Year", "_label:Forecast Dai No. (Mass)", "_label:Forecast Dai No. (Service)"],
            "line1": ["Transmission Date", "Transmission Time", "Messge ID No", "Number of Item", "Forecast Year", "Forecast Dai No. (Mass)", "Forecast Dai No. (Service)"]
          },
          "requiredHeaders": [
            "Item No.",
            "Description",
            "UM",
            "Vendor",
            "Material Group",
            "Type",
            "Plant"
          ],
          "optionalHeaders": [
            "Mat.Grp.Desc.",
            "AprA", "AprB", "MayA", "MayB", "JunA", "JunB",
            "JulA", "JulB", "AugA", "AugB", "SepA", "SepB",
            "OctA", "OctB", "NovA", "NovB", "DecA", "DecB",
            "JanA", "JanB", "FebA", "FebB", "MarA", "MarB",
            "Total", "Remark1", "Remark2", "Remark3", "Remark4"
          ],
          "headerAliases": {},
          "extractRules": {
            "documentNo": { "source": "filename", "stripExtension": true }
          }
        }
        """;

    private const string PurchaseOrderSchemaJson = """
        {
          "schemaKey": "PurchaseOrder",
          "schemaVersion": "v1",
          "fileType": "PurchaseOrder",
          "displayName": "SAP MCP Purchase Order",
          "filenamePrefixPattern": "^[Pp]",
          "delimiter": ",",
          "hasHeaderRow": true,
          "hasSegmentMarkers": true,
          "headerRowMarker": "H1",
          "segmentMarkerColumn": 0,
          "skipLines": 0,
          "metadataRowMarkers": ["S1", "S2", "S3"],
          "metadataFields": {
            "S1": ["RohsNotice", "TransmissionDate", "TransmissionDateValue", "TransmissionTime", "TransmissionTimeValue"],
            "S3": ["FileName", "NumberOfRecord", "SupplierCode", "SupplierName", "ContactName", "ManagerName"]
          },
          "requiredHeaders": [
            "PO Status", "PO No.", "PO Item", "Item No.", "Description",
            "Due Qty", "UM", "Due Date", "Unit Price", "Amount", "Currency"
          ],
          "optionalHeaders": [
            "BOI Name", "Type of Parts", "Production Month", "Production Lot",
            "Delivery Spot", "PO Date", "Delivery Type", "PO Comment2",
            "Ship By", "Ship To", "Terms of Trade", "Payment term",
            "Incharge Name", "Last Status", "Changes Note", "Old value", "New value"
          ],
          "headerAliases": {},
          "extractRules": {
            "documentNo": { "source": "filename", "stripExtension": true },
            "supplierCode": { "source": "metadata", "marker": "S3", "fieldIndex": 2 }
          }
        }
        """;
}

