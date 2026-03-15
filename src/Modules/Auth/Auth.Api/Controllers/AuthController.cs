using System.Security.Claims;
using Auth.Application.Features.ChangePassword;
using Auth.Application.Features.Login;
using Auth.Application.Features.Logout;
using Auth.Application.Features.CurrentUser;
using Auth.Application.Features.RefreshToken;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;

namespace Auth.Api.Controllers;

/// <summary>
/// Authentication controller providing login, token refresh, logout, profile,
/// and password change endpoints.
/// All tokens are transmitted via JSON body — never query strings.
/// </summary>
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("AuthLoginPolicy")]
public sealed class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IMediator mediator, ILogger<AuthController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>Authenticates a user and returns access + refresh tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(LoginFailedResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = Request.Headers.UserAgent.ToString();

        var result = await _mediator.Send(
            new LoginCommand(request.Username, request.Password, ip, ua), ct);

        if (result.IsSuccess)
            return Ok(result.Tokens);

        LogLoginRejected(_logger, request.Username);

        // Return 401 with lockout metadata (no user enumeration)
        return Unauthorized(result.Failure);
    }

    /// <summary>Rotates a refresh token and issues a new token pair.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var ua = Request.Headers.UserAgent.ToString();

        try
        {
            var result = await _mediator.Send(
                new RefreshTokenCommand(request.RefreshToken, ip, ua), ct);

            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Token refresh failed",
                Detail = ex.Message,
                Status = StatusCodes.Status401Unauthorized
            });
        }
    }

    /// <summary>Revokes the current session.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest? request, CancellationToken ct)
    {
        var accessToken = Request.Headers.Authorization.ToString()
            .Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        await _mediator.Send(
            new LogoutCommand(request?.RefreshToken, accessToken), ct);

        return NoContent();
    }

    /// <summary>Returns the authenticated user's profile.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new GetMeQuery(userId), ct);
        return Ok(result);
    }

    /// <summary>Changes the authenticated user's password.</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? User.FindFirstValue("sub");

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _mediator.Send(
            new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), ct);

        if (result.IsSuccess)
            return NoContent();

        return BadRequest(new ProblemDetails
        {
            Title = "Password change failed",
            Detail = result.ErrorMessage,
            Status = StatusCodes.Status400BadRequest
        });
    }

    private static void LogLoginRejected(ILogger logger, string username) => logger.LogWarning("Login rejected for username '{Username}'", username);
}

/// <summary>Login request body.</summary>
public sealed record LoginRequest(string Username, string Password);

/// <summary>Refresh request body.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Logout request body (optional refresh token for revocation).</summary>
public sealed record LogoutRequest(string? RefreshToken);

/// <summary>Change password request body.</summary>
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
