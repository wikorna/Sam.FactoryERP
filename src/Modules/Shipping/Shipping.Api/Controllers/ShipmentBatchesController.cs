using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shipping.Application.Features.GetBatch;
using Shipping.Application.Features.GetBatchErrors;
using Shipping.Application.Features.UploadBatch;

namespace Shipping.Api.Controllers;

/// <summary>
/// Multipart/form-data request for Marketing CSV upload.
/// </summary>
public sealed class UploadShipmentBatchRequest
{
    /// <summary>The CSV file to upload (required).</summary>
    public IFormFile? File { get; init; }

    /// <summary>Optional PO reference override. If omitted, derived from CSV data.</summary>
    public string? PoReference { get; init; }
}

/// <summary>
/// Handles shipment batch operations — upload, retrieve, and error inspection.
/// </summary>
[ApiController]
[Route("api/v1/shipping/batches")]
[Produces("application/json")]
public sealed class ShipmentBatchesController(IMediator mediator) : ControllerBase
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>
    /// Upload a Marketing CSV file to create a new Draft shipment batch.
    /// </summary>
    /// <remarks>
    /// <para>Accepts a multipart/form-data body with:</para>
    /// <list type="bullet">
    ///   <item><c>file</c> — the CSV file (required, max 10 MB).</item>
    ///   <item><c>poReference</c> — optional PO reference override.</item>
    /// </list>
    /// <para>Expected CSV columns (header row required):</para>
    /// <c>CustomerCode, PartNo, ProductName, Description, Quantity, PoNumber, PoItem, DueDate, RunNo, Store, Remarks, LabelCopies</c>
    /// </remarks>
    /// <response code="201">Batch created successfully. Returns batch details including any row errors.</response>
    /// <response code="400">File missing, empty, too large, or not a CSV.</response>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadShipmentBatchResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload(
        [FromForm] UploadShipmentBatchRequest request,
        CancellationToken ct)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return Problem(
                detail: "A non-empty CSV file is required.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation Error");
        }

        if (request.File.Length > MaxFileSizeBytes)
        {
            return Problem(
                detail: $"File size {request.File.Length} bytes exceeds the maximum of {MaxFileSizeBytes / (1024 * 1024)} MB.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "File Too Large");
        }

        if (!request.File.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return Problem(
                detail: "Only CSV files are accepted.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid File Type");
        }

        await using var stream = request.File.OpenReadStream();

        var command = new UploadShipmentBatchCommand(
            FileStream: stream,
            FileName: request.File.FileName,
            FileSizeBytes: request.File.Length,
            PoReference: request.PoReference);

        var result = await mediator.Send(command, ct);

        return CreatedAtAction(
            actionName: nameof(GetById),
            routeValues: new { id = result.BatchId },
            value: result);
    }

    /// <summary>
    /// Get a shipment batch by ID, including all line items.
    /// </summary>
    /// <response code="200">Returns the batch with items.</response>
    /// <response code="404">Batch not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ShipmentBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetShipmentBatchQuery(id), ct);

        return result is null
            ? NotFound()
            : Ok(result);
    }

    /// <summary>
    /// Get CSV parse/validation errors for a shipment batch.
    /// </summary>
    /// <response code="200">Returns the error list.</response>
    /// <response code="404">Batch not found.</response>
    [HttpGet("{id:guid}/errors")]
    [ProducesResponseType(typeof(ShipmentBatchErrorsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetErrors(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetShipmentBatchErrorsQuery(id), ct);

        return result is null
            ? NotFound()
            : Ok(result);
    }
}

