using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace FactoryERP.ApiHost.Auth;

/// <summary>
/// Maps the authenticated user's <c>sub</c> / <c>NameIdentifier</c> JWT claim to
/// the SignalR user ID, so that <c>IHubContext.Clients.User(userId)</c> correctly
/// routes messages to a specific user regardless of how many connections they have open.
/// </summary>
/// <remarks>
/// Registered as the sole <see cref="IUserIdProvider"/> in ApiHost DI.
/// The claim priority order matches the JWT token structure emitted by
/// <c>Auth.Infrastructure.JwtTokenService</c>:
/// 1. <see cref="ClaimTypes.NameIdentifier"/> (<c>nameidentifier</c>)
/// 2. <c>sub</c> (OpenID Connect standard)
/// </remarks>
public sealed class NotificationUserIdProvider : IUserIdProvider
{
    /// <inheritdoc />
    public string? GetUserId(HubConnectionContext connection)
        => connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
           ?? connection.User?.FindFirst("sub")?.Value;
}

