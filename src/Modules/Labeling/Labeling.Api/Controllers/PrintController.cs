using Labeling.Application.Features.PrintJobs;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Labeling.Api.Controllers;

[ApiController]
[Route("api/labeling/print-jobs")]
public class PrintController : ControllerBase
{
    private readonly IMediator _mediator;

    public PrintController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Submit a new print job. Idempotent: duplicate IdempotencyKey returns the existing job.
    /// Returns HTTP 202 Accepted with Location header.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), 202)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreatePrintJob([FromBody] CreatePrintJobRequest request, CancellationToken ct)
    {
        var command = new CreatePrintJobCommand(
            IdempotencyKey: request.IdempotencyKey,
            PrinterId: request.PrinterId,
            ZplContent: request.ZplContent,
            Copies: request.Copies,
            RequestedBy: request.RequestedBy ?? "anonymous");

        var result = await _mediator.Send(command, ct);

        // Check if message indicates existing job
        var alreadyExisted = !string.IsNullOrEmpty(result.Message);

        return AcceptedAtAction(nameof(GetPrintJob),
            new { jobId = result.PrintJobId },
            new
            {
                JobId = result.PrintJobId,
                Status = result.Status,
                Message = result.Message,
                AlreadyExisted = alreadyExisted
            });
    }

    /// <summary>
    /// Query the current status of a print job.
    /// </summary>
    [HttpGet("{jobId:guid}")]
    [ProducesResponseType(typeof(PrintJobDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPrintJob(Guid jobId, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetPrintJobQuery(jobId), ct);
        return dto is not null ? Ok(dto) : NotFound();
    }
}

/// <summary>
/// Request body for print job submission.
/// </summary>
public sealed record CreatePrintJobRequest
{
    public required string IdempotencyKey { get; init; }
    public required Guid PrinterId { get; init; }
    public required string ZplContent { get; init; }
    public int Copies { get; init; } = 1;
    public string? RequestedBy { get; init; }
}
