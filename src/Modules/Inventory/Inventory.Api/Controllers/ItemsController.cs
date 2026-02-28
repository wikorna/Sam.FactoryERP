using FactoryERP.Abstractions.Cqrs;
using FactoryERP.Abstractions.Pagination;
using Inventory.Application.Commands;
using Inventory.Application.Dtos;
using Inventory.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Controllers;

/// <summary>
/// Item Master endpoints following SAP Fiori patterns:
/// List Report, Object Page, Value Help, and CRUD commands.
/// </summary>
[ApiController]
[Route("api/v1/inventory/items")]
[Authorize]
public sealed class ItemsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// List Report: paginated items with filtering, sorting, and search.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<ItemListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery(Name = "q")] string? searchTerm = null,
        CancellationToken ct = default)
    {
        var query = new GetItemsListQuery
        {
            Page = page,
            PageSize = pageSize,
            SearchTerm = searchTerm
        };

        var result = await mediator.Send(query, ct);
        return result.IsSuccess ? Ok(result.Value) : ToError(result);
    }

    /// <summary>
    /// Object Page: full item detail with child sections.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ItemDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItem(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetItemByIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : ToError(result);
    }

    /// <summary>
    /// Value Help (F4): item search for dropdowns. Supports dependent parameters via ?Parameters[Key]=Value.
    /// </summary>
    [HttpGet("value-help")]
    [ProducesResponseType(typeof(ValueHelpResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValueHelp(
        [FromQuery] GetItemValueHelpQuery query,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(query, ct);
        return result.IsSuccess ? Ok(result.Value) : ToError(result);
    }

    /// <summary>
    /// Server-driven UI Metadata: returns filter definitions, allowed sorts, and status enums.
    /// </summary>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ListMetadataResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetadata(CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetItemsListMetadataQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : ToError(result);
    }

    /// <summary>Creates a new item master record.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateItem([FromBody] CreateItemCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetItem), new { id = result.Value }, result.Value)
            : ToError(result);
    }

    /// <summary>Updates an existing item (requires RowVersion for concurrency).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItem(Guid id, [FromBody] UpdateItemRequest body, CancellationToken ct)
    {
        var command = new UpdateItemCommand(
            id, body.Description, body.BaseUom, body.MaterialGroup,
            body.LongDescription, body.GrossWeight, body.NetWeight,
            body.WeightUnit, body.RowVersion);

        var result = await mediator.Send(command, ct);
        return result.IsSuccess ? NoContent() : ToError(result);
    }

    /// <summary>Soft-deletes an item.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateItem(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeactivateItemCommand(id), ct);
        return result.IsSuccess ? NoContent() : ToError(result);
    }

    // ── Private: error code ⟶  HTTP status mapping ──
    private ObjectResult ToError<T>(Result<T> result) => ToError((Result)result);

    private ObjectResult ToError(Result result)
    {
        var statusCode = result.Error.Code switch
        {
            "NotFound" => StatusCodes.Status404NotFound,
            "Conflict" => StatusCodes.Status409Conflict,
            "Validation" => StatusCodes.Status422UnprocessableEntity,
            "Unauthorized" => StatusCodes.Status401Unauthorized,
            "Forbidden" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return Problem(
            title: result.Error.Code,
            detail: result.Error.Message,
            statusCode: statusCode);
    }
}

/// <summary>Update request body (separate from command to bind id from route).</summary>
public sealed record UpdateItemRequest(
    string Description,
    string BaseUom,
    string? MaterialGroup,
    string? LongDescription,
    decimal? GrossWeight,
    decimal? NetWeight,
    string? WeightUnit,
    byte[] RowVersion);
