using FactoryERP.Contracts.Labeling;
using MassTransit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Labeling.Api.Controllers;

[ApiController]
[Route("api/print")]
public class PrintJobsController : ControllerBase
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<PrintJobsController> _logger;

    public PrintJobsController(IPublishEndpoint publishEndpoint, ILogger<PrintJobsController> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    /// <summary>
    /// Lightweight "fire-and-print" endpoint that publishes a PrintZplCommand directly.
    /// For full lifecycle tracking, use POST /api/labeling/print-jobs instead.
    /// </summary>
    [HttpPost("zpl")]
    public async Task<IActionResult> PrintZpl(
        [FromBody] PrintZplRequest request,
        CancellationToken cancellationToken)
    {
        if (request.PrinterId == Guid.Empty || string.IsNullOrWhiteSpace(request.ZplContent))
        {
            return BadRequest("PrinterId and ZplContent are required.");
        }

        var jobId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        var command = new PrintZplCommand(
            JobId: jobId,
            TenantId: request.TenantId ?? string.Empty,
            PrinterId: request.PrinterId,
            Zpl: request.ZplContent,
            Copies: request.Copies > 0 ? request.Copies : 1,
            RequestedBy: request.RequestedBy ?? "API",
            CorrelationId: correlationId,
            TimestampUtc: DateTime.UtcNow
        );

        // Mask/truncate ZPL to avoid bloating logs
        string truncatedZpl = request.ZplContent.Length > 100
            ? request.ZplContent[..100] + "..."
            : request.ZplContent;

        LogPublishingPrintJob(_logger, jobId, request.PrinterId, truncatedZpl);

        await _publishEndpoint.Publish(command, cancellationToken);

        return Accepted(new { JobId = jobId, Message = "Print job queued successfully." });
    }

    private static void LogPublishingPrintJob(ILogger logger, Guid jobId, Guid printerId, string zpl) => logger.LogInformation("Publishing PrintZplCommand for JobId {JobId}, Printer {PrinterId}, ZPL: {Zpl}", jobId, printerId, zpl);
}

public class PrintZplRequest
{
    public Guid PrinterId { get; set; }
    public string ZplContent { get; set; } = string.Empty;
    public string? TenantId { get; set; }
    public string? RequestedBy { get; set; }
    public int Copies { get; set; } = 1;
}
