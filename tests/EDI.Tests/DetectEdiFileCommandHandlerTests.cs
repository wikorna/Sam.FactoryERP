using System.Text;
using EDI.Application.Abstractions;
using EDI.Application.Features.DetectEdiFile;
using EDI.Domain.Enums;
using EDI.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace EDI.Tests;

public class DetectEdiFileCommandHandlerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Build a mock IEdiFileDetector that returns the given result for any input.
    /// </summary>
    private static Mock<IEdiFileDetector> BuildDetector(DetectEdiFileResult result)
    {
        var mock = new Mock<IEdiFileDetector>();
        mock.Setup(d => d.DetectAsync(
                It.IsAny<string>(),
                It.IsAny<Stream>(),
                It.IsAny<long>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return mock;
    }

    private static DetectEdiFileCommandHandler BuildHandler(
        Mock<IEdiFileDetector>? detector = null)
    {
        var logger = new Mock<ILogger<DetectEdiFileCommandHandler>>();
        return new DetectEdiFileCommandHandler(
            detector?.Object ?? new Mock<IEdiFileDetector>().Object,
            logger.Object);
    }

    /// <summary>Build a memory stream from a plain UTF-8 string.</summary>
    private static MemoryStream ToStream(string content) =>
        new MemoryStream(Encoding.UTF8.GetBytes(content));

    private static DetectEdiFileResult MakeSuccess(
        string fileName,
        EdiFileType fileType,
        string schemaKey = "SAP_FORECAST",
        string schemaVersion = "v1") =>
        DetectEdiFileResult.Success(
            fileName:            fileName,
            fileType:            fileType,
            fileTypeDisplayName: fileType.ToString(),
            documentNo:          Path.GetFileNameWithoutExtension(fileName),
            schemaKey:           schemaKey,
            schemaVersion:       schemaVersion,
            header:              new Dictionary<string, string?> { ["Col1"] = "col_0" });

    private static DetectEdiFileResult MakeFailure(
        string fileName,
        EdiFileType fileType,
        string errorCode,
        string errorMessage) =>
        DetectEdiFileResult.Failure(
            fileName,
            fileType,
            [new EdiDetectionError(errorCode, errorMessage)]);

    // ── Tests: handler delegates to IEdiFileDetector ─────────────────────────

    [Fact]
    public async Task DetectorSuccessResultShouldBeReturnedByHandler()
    {
        var expected = MakeSuccess("F12345.CSV", EdiFileType.Forecast);
        var detector = BuildDetector(expected);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("data"), "F12345.CSV", 4);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeTrue();
        result.FileType.Should().Be(EdiFileType.Forecast);
        result.SchemaKey.Should().Be("SAP_FORECAST");
    }

    [Fact]
    public async Task DetectorFailureResultShouldBeReturnedByHandler()
    {
        var failure  = MakeFailure("A12345.CSV", EdiFileType.Unknown, EdiErrorCodes.InvalidFilename, "Bad prefix");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("data"), "A12345.CSV", 4);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.InvalidFilename);
    }

    [Fact]
    public async Task HandlerCallsDetectorWithCorrectArguments()
    {
        var expected = MakeSuccess("F12345.CSV", EdiFileType.Forecast);
        var detector = BuildDetector(expected);
        var handler  = BuildHandler(detector);

        var stream  = ToStream("content");
        var command = new DetectEdiFileCommand(stream, "F12345.CSV", 7);
        await handler.Handle(command, CancellationToken.None);

        detector.Verify(d => d.DetectAsync(
            "F12345.CSV",
            stream,
            7L,
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForecastFileShouldReturnForecastFileType()
    {
        var success  = MakeSuccess("F10042926010001.CSV", EdiFileType.Forecast, "SAP_FORECAST");
        var detector = BuildDetector(success);
        var handler  = BuildHandler(detector);

        var content = "Document No_,Item No_,Quantity,Due Date\r\nF001,ITEM1,100,2026-03-01\r\n";
        var command = new DetectEdiFileCommand(
            ToStream(content), "F10042926010001.CSV", Encoding.UTF8.GetByteCount(content));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeTrue();
        result.FileType.Should().Be(EdiFileType.Forecast);
        result.SchemaKey.Should().Be("SAP_FORECAST");
        result.DocumentNo.Should().Be("F10042926010001");
    }

    [Fact]
    public async Task PurchaseOrderFileShouldReturnPurchaseOrderFileType()
    {
        var success  = MakeSuccess("P10042926020009.CSV", EdiFileType.PurchaseOrder, "SAP_PO");
        var detector = BuildDetector(success);
        var handler  = BuildHandler(detector);

        var content = "Document No_,Buy-from Vendor No_,No_,Quantity\r\nP001,VND1,ITEM1,50\r\n";
        var command = new DetectEdiFileCommand(
            ToStream(content), "P10042926020009.CSV", Encoding.UTF8.GetByteCount(content));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeTrue();
        result.FileType.Should().Be(EdiFileType.PurchaseOrder);
        result.SchemaKey.Should().Be("SAP_PO");
        result.DocumentNo.Should().Be("P10042926020009");
    }

    [Fact]
    public async Task FileTooLargeShouldReturnFileTooLargeError()
    {
        var failure  = MakeFailure("F12345.CSV", EdiFileType.Unknown, EdiErrorCodes.FileTooLarge, "Too large");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("data"), "F12345.CSV", 11L * 1024 * 1024);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.FileTooLarge);
    }

    [Fact]
    public async Task InvalidExtensionShouldReturnInvalidExtensionError()
    {
        var failure  = MakeFailure("F12345.txt", EdiFileType.Unknown, EdiErrorCodes.InvalidExtension, "Not CSV");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("data"), "F12345.txt", 4);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.InvalidExtension);
    }

    [Fact]
    public async Task InvalidFilenamePrefixShouldReturnInvalidFilenameError()
    {
        var failure  = MakeFailure("X12345.CSV", EdiFileType.Unknown, EdiErrorCodes.InvalidFilename, "Bad prefix");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("data"), "X12345.CSV", 4);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.InvalidFilename);
    }

    [Fact]
    public async Task HeaderMismatchShouldReturnHeaderMismatchError()
    {
        var failure  = MakeFailure("P12345.CSV", EdiFileType.PurchaseOrder, EdiErrorCodes.HeaderMismatch, "Missing cols");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("WrongCol\r\n"), "P12345.CSV", 10);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.HeaderMismatch);
    }

    [Fact]
    public async Task InvalidEncodingShouldReturnEncodingError()
    {
        var failure  = MakeFailure("F12345.CSV", EdiFileType.Forecast, EdiErrorCodes.InvalidEncoding, "Bad UTF-8");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(
            new MemoryStream(new byte[] { 0x46, 0xC3, 0x28 }), "F12345.CSV", 3);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.InvalidEncoding);
    }

    [Fact]
    public async Task EmptyFileShouldReturnEmptyFileError()
    {
        var failure  = MakeFailure("F12345.CSV", EdiFileType.Forecast, EdiErrorCodes.EmptyFile, "Empty");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(new MemoryStream(), "F12345.CSV", 0);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.EmptyFile);
    }

    [Fact]
    public async Task UnknownFileTypeShouldReturnUnknownFileTypeError()
    {
        var failure  = MakeFailure("F12345.CSV", EdiFileType.Unknown, EdiErrorCodes.UnknownFileType, "No schema");
        var detector = BuildDetector(failure);
        var handler  = BuildHandler(detector);

        var command = new DetectEdiFileCommand(ToStream("data"), "F12345.CSV", 4);
        var result  = await handler.Handle(command, CancellationToken.None);

        result.Detected.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Code == EdiErrorCodes.UnknownFileType);
    }
}

