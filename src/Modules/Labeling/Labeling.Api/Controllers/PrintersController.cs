using MediatR;
using Microsoft.AspNetCore.Mvc;
using Labeling.Application.Features.Printers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Labeling.Api.Controllers;

[ApiController]
[Route("api/print/printers")]
[Authorize] // Require Auth
public class PrintersController : ControllerBase
{
    private readonly IMediator _mediator;

    public PrintersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(IEnumerable<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPrinters(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAuthorizedPrintersQuery(), ct);
        return Ok(result);
    }
}

