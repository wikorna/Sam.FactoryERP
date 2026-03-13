using MediatR;
using Microsoft.AspNetCore.Mvc;
using Labeling.Application.Features.PrintJobs;
using Labeling.Application.Models;
using Microsoft.AspNetCore.Http;
using Labeling.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using FactoryERP.Abstractions.Identity;

namespace Labeling.Api.Controllers;

[ApiController]
[Route("api/print")]
[Authorize] // Require Auth
public class ProductLabelController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProductLabelController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Print a standard 55x90mm Product Label (supports Portrait/Landscape feed).
    /// </summary>
    [HttpPost("product-label")]
    [ProducesResponseType(typeof(CreatePrintJobResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PrintProductLabel(
        [FromBody] PrintProductLabelRequest request,
        CancellationToken ct)
    {
        var command = new CreateProductLabelJobCommand(
            IdempotencyKey: request.IdempotencyKey ?? Guid.NewGuid().ToString(),
            PrinterId: request.PrinterId,
            LabelData: new ProductLabelData(
                DocNo: request.DocNo,
                PageText: request.PageText ?? "(1/1)",
                ProductName: request.ProductName,
                PartNo: request.PartNo,
                Quantity: request.Quantity,
                PoNumber: request.PoNumber,
                PoItem: request.PoItem,
                DueDate: request.DueDate,
                Description: request.Description,
                RunNo: request.RunNo,
                Store: request.Store,
                QrPayload: request.QrPayload,
                Remarks: request.Remarks
            ),
            Copies: request.Copies,
            RequestedBy: _currentUser.RequestedBy ?? "api"
        );

        try
        {
            var result = await _mediator.Send(command, ct);

            // If message is present, it implies idempotency hit or info
            // Standardizing response to match result shape if possible, or keeping anonymous object
            return Accepted(new
            {
                JobId = result.PrintJobId,
                Status = result.Status,
                Cached = !string.IsNullOrEmpty(result.Message),
                Message = result.Message
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message); // 403
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Error = ex.Message }); // 404
        }
        catch (InvalidOperationException ex)
        {
            // Business rule violation (e.g. Printer Disabled)
            return UnprocessableEntity(new { Error = ex.Message }); // 422
        }
        catch (Exception ex)
        {
            return BadRequest(new { Error = ex.Message }); // Fallback 400
        }
    }
}

public class PrintProductLabelRequest
{
    public string? IdempotencyKey { get; set; }
    public Guid PrinterId { get; set; }
    public int Copies { get; set; } = 1;
    public string? RequestedBy { get; set; }

    // Label Data
    public string DocNo { get; set; } = string.Empty;
    public string? PageText { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string PartNo { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string PoItem { get; set; } = string.Empty;
    public string? DueDate { get; set; }
    public string? Description { get; set; }
    public string? RunNo { get; set; }
    public string? Store { get; set; }
    public string QrPayload { get; set; } = string.Empty;
    public string? Remarks { get; set; }
}
