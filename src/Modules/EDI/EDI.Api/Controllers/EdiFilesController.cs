using EDI.Application.Features.DetectEdiFile;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EDI.Api.Controllers;

// ── Request model ─────────────────────────────────────────────────────────────

/// <summary>
/// Multipart/form-data request for EDI file detection.
/// Bound by <see cref="Microsoft.AspNetCore.Mvc.FromFormAttribute"/> so Swagger renders
/// both the <c>file</c> upload control and the optional <c>clientId</c> text field.
/// </summary>
public sealed class DetectEdiFileRequest
{
    /// <summary>The CSV file to detect (required).</summary>
    public IFormFile? File { get; init; }

    /// <summary>
    /// Optional caller identifier — forwarded into the MediatR command for tracing/audit.
    /// Accepts any non-empty string (UUID, username, system name, etc.).
    /// </summary>
    public string? ClientId { get; init; }
}

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// Handles EDI file operations — detection, validation, and import.
/// </summary>
[ApiController]
[Route("api/edi/files")]
[Produces("application/json")]
public sealed class EdiFilesController(IMediator mediator) : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Detect the EDI file type and validate its CSV structure against the configured schema.
    /// </summary>
    /// <remarks>
    /// Accepts a multipart/form-data body with two fields:
    /// - <c>file</c> — the CSV file (required).
    /// - <c>clientId</c> — optional caller identifier for tracing.
    ///
    /// Detection rules:
    /// - Filename must have a <c>.CSV</c> extension (case-insensitive).
    /// - Filename starting with <c>F</c> → Forecast.
    /// - Filename starting with <c>P</c> → PurchaseOrder.
    /// - CSV header row is validated against the configured schema.
    /// - Extra columns produce warnings; missing required columns produce errors.
    ///
    /// No database record is created by this endpoint.
    ///
    /// Example curl:
    /// <code>
    /// curl -i -X POST "http://localhost:5001/api/edi/files/detect" \
    ///   -F "file=@/path/to/F10042926010001.CSV" \
    ///   -F "clientId=my-client-id"
    /// </code>
    /// </remarks>
    /// <param name="request">Multipart form containing the file and optional clientId.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 — file detected successfully with schema metadata.
    /// 400 — file field is missing or empty.
    /// 413 — file exceeds the 10 MB size limit.
    /// 422 — file format is invalid (wrong extension, prefix, encoding, or header mismatch).
    /// </returns>
    [HttpPost("detect")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(DetectEdiFileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(typeof(DetectEdiFileErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DetectAsync(
        [FromForm] DetectEdiFileRequest request,
        CancellationToken ct)
    {
        var file = request.File;

        if (file is null || file.Length == 0)
        {
            return Problem(
                detail: "A non-empty file field named 'file' is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "File Missing");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return Problem(
                detail: $"File size {file.Length:N0} bytes exceeds the 10 MB limit.",
                statusCode: StatusCodes.Status413RequestEntityTooLarge,
                title: "File Too Large");
        }

        // Path traversal guard — use only the bare filename, never the full path.
        var safeFileName = Path.GetFileName(file.FileName);

        // IFormFile must not cross the API boundary — open the stream here and pass it forward.
        await using var stream = file.OpenReadStream();

        var command = new DetectEdiFileCommand(
            Content:   stream,
            FileName:  safeFileName,
            SizeBytes: file.Length,
            ClientId:  request.ClientId?.Trim());

        var result = await mediator.Send(command, ct);

        if (result.Detected)
        {
            return Ok(new DetectEdiFileResponse(
                FileName:      result.FileName,
                FileType:      result.FileType.ToString(),
                Detected:      true,
                DocumentNo:    result.DocumentNo,
                SchemaKey:     result.SchemaKey,
                SchemaVersion: result.SchemaVersion,
                Header:        result.Header,
                Warnings:      result.Warnings));
        }

        return UnprocessableEntity(new DetectEdiFileErrorResponse(
            FileName: result.FileName,
            FileType: result.FileType.ToString(),
            Detected: false,
            Errors:   result.Errors
                .Select(e => new EdiErrorDto(e.Code, e.Message))
                .ToList()));
    }
}

// ── Response DTOs ──────────────────────────────────────────────────────────────

/// <summary>Successful detection response (HTTP 200).</summary>
public sealed record DetectEdiFileResponse(
    string                                FileName,
    string                                FileType,
    bool                                  Detected,
    string?                               DocumentNo,
    string?                               SchemaKey,
    string?                               SchemaVersion,
    IReadOnlyDictionary<string, string?>? Header,
    IReadOnlyList<string>                 Warnings);

/// <summary>Detection failure response (HTTP 422).</summary>
public sealed record DetectEdiFileErrorResponse(
    string                     FileName,
    string                     FileType,
    bool                       Detected,
    IReadOnlyList<EdiErrorDto> Errors);

/// <summary>A single EDI validation error.</summary>
public sealed record EdiErrorDto(string Code, string Message);

