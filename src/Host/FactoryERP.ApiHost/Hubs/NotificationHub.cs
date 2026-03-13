using FactoryERP.Abstractions.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FactoryERP.ApiHost.Hubs;

/// <summary>
/// Real-time notification hub.  Angular clients connect to
/// <c>/hubs/notifications</c> and receive typed callbacks defined in
/// <see cref="INotificationClient"/>.
/// </summary>
/// <remarks>
/// <b>Authentication:</b> Every connection must carry a valid JWT (Bearer token
/// via <c>access_token</c> query-string or <c>Authorization</c> header).
/// The <see cref="NotificationUserIdProvider"/> maps the <c>sub</c> claim to
/// the SignalR user ID so <c>IHubContext.Clients.User(userId)</c> routing works.
///
/// <b>Groups:</b> On connect, the user is automatically added to role-named
/// groups (<c>role:Admin</c>, <c>role:Operator</c>, …) so that
/// <see cref="INotificationDispatcher.NotifyRoleAsync"/> works without
/// maintaining a custom mapping table.
/// </remarks>
[Authorize]
public sealed partial class NotificationHub : Hub<INotificationClient>
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
        => _logger = logger;

    /// <summary>
    /// Adds the connecting user to each of their role groups so that
    /// role-scoped notifications can be dispatched via <c>Clients.Group()</c>.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? "(anonymous)";

        // Build role group names from claims and add user to each.
        var roleClaims = Context.User?.Claims
            .Where(c => c.Type is System.Security.Claims.ClaimTypes.Role
                               or "role")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        foreach (var role in roleClaims)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"role:{role}");

        LogConnected(userId, roleClaims.Count);
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? "(anonymous)";

        if (exception is null)
            LogDisconnected(userId);
        else
            LogDisconnectedWithError(userId, exception);

        await base.OnDisconnectedAsync(exception);
    }

    // ── Analyzer-compliant log helpers ───────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SignalR connected: user={UserId}, roleGroups={RoleGroupCount}")]
    private partial void LogConnected(string userId, int roleGroupCount);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "SignalR disconnected: user={UserId}")]
    private partial void LogDisconnected(string userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "SignalR disconnected with error: user={UserId}")]
    private partial void LogDisconnectedWithError(string userId, Exception ex);
}

