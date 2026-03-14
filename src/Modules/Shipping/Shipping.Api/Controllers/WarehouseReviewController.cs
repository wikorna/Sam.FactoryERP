using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shipping.Application.Features.WarehouseReview;

namespace Shipping.Api.Controllers;

// ── Request models ────────────────────────────────────────────────────────────

/// <summary>JSON body for approving a shipment batch.</summary>
public sealed class ApproveShipmentBatchRequest
{
    /// <summary>Reviewer's user ID.</summary>
    public Guid ReviewerUserId { get; init; }

    /// <summary>Optional comment.</summary>
    public string? Comment { get; init; }
}

/// <summary>JSON body for rejecting a shipment batch.</summary>
public sealed class RejectShipmentBatchRequest
{
    /// <summary>Reviewer's user ID.</summary>
    public Guid ReviewerUserId { get; init; }

    /// <summary>Rejection reason (required).</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>JSON body for partially approving a shipment batch.</summary>
public sealed class PartiallyApproveShipmentBatchRequest
{
    /// <summary>Reviewer's user ID.</summary>
    public Guid ReviewerUserId { get; init; }

    /// <summary>Per-item approval decisions.</summary>
    public List<ItemDecisionRequest> ItemDecisions { get; init; } = [];

    /// <summary>Optional comment.</summary>
    public string? Comment { get; init; }
}

/// <summary>Per-item decision in a partial-approve request.</summary>
public sealed class ItemDecisionRequest
{
    /// <summary>Item ID.</summary>
    public Guid ItemId { get; init; }

    /// <summary>True = approved, false = excluded.</summary>
    public bool IsApproved { get; init; }

    /// <summary>Optional reason for exclusion.</summary>
    public string? ExclusionReason { get; init; }
}

// ── Controller ────────────────────────────────────────────────────────────────

/// <summary>
/// Warehouse review operations for shipment batches — approve, reject, partial-approve, list pending.
/// </summary>
[ApiController]
[Route("api/v1/warehouse/shipment-batches")]
[Produces("application/json")]
public sealed class WarehouseReviewController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Get all shipment batches pending warehouse review.
    /// Returns batches in Submitted or UnderReview status, ordered by creation date.
    /// </summary>
    /// <response code="200">List of pending batches.</response>
    [HttpGet("pending-review")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingReviewBatchDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReview(CancellationToken ct)
    {
        var result = await mediator.Send(new GetPendingReviewBatchesQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Approve a shipment batch — all items are approved for printing.
    /// </summary>
    /// <response code="200">Batch approved successfully.</response>
    /// <response code="404">Batch not found.</response>
    /// <response code="409">Batch is not in a reviewable state.</response>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(ReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveShipmentBatchRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new ApproveShipmentBatchCommand(
                id, request.ReviewerUserId, request.Comment);

            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Invalid State Transition");
        }
    }

    /// <summary>
    /// Reject a shipment batch — returns it to Marketing for revision.
    /// </summary>
    /// <response code="200">Batch rejected successfully.</response>
    /// <response code="404">Batch not found.</response>
    /// <response code="409">Batch is not in a reviewable state.</response>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(ReviewResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] RejectShipmentBatchRequest request,
        CancellationToken ct)
    {
        try
        {
            var command = new RejectShipmentBatchCommand(
                id, request.ReviewerUserId, request.Reason);

            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Invalid State Transition");
        }
    }

    /// <summary>
    /// Partially approve a shipment batch — approve some items, exclude others.
    /// </summary>
    /// <response code="200">Batch partially approved successfully.</response>
    /// <response code="404">Batch not found.</response>
    /// <response code="409">Batch is not in a reviewable state or invalid item selection.</response>
    [HttpPost("{id:guid}/partial-approve")]
    [ProducesResponseType(typeof(PartialApproveResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PartiallyApprove(
        Guid id,
        [FromBody] PartiallyApproveShipmentBatchRequest request,
        CancellationToken ct)
    {
        try
        {
            var decisions = request.ItemDecisions
                .Select(d => new ItemReviewDecisionDto(d.ItemId, d.IsApproved, d.ExclusionReason))
                .ToList();

            var command = new PartiallyApproveShipmentBatchCommand(
                id, request.ReviewerUserId, decisions, request.Comment);

            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound, title: "Not Found");
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict, title: "Invalid State Transition");
        }
    }
}

