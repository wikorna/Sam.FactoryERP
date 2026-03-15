using System.Security.Claims;
using FactoryERP.Abstractions.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FactoryERP.ApiHost.Hubs;

/// <summary>
/// Real-time notification hub. Angular clients connect to
/// <c>/hubs/notifications</c> and receive typed callbacks defined in
/// <see cref="INotificationClient"/>.
/// </summary>
[Authorize]
public sealed class NotificationHub(ILogger<NotificationHub> logger) : Hub<INotificationClient>
{
    private const string RoleClaimType = "role";
    private const string RoleGroupPrefix = "role:";

    private readonly ILogger<NotificationHub> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? "(anonymous)";
        var connectionId = Context.ConnectionId;

        var roles = GetDistinctRoles(Context.User);

        foreach (var role in roles)
        {
            var groupName = BuildRoleGroupName(role);
            await Groups.AddToGroupAsync(connectionId, groupName);
        }

        LogConnected(userId, connectionId, roles.Length);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? "(anonymous)";
        var connectionId = Context.ConnectionId;

        if (exception is null)
            LogDisconnected(userId, connectionId);
        else
            LogDisconnectedWithError(userId, connectionId, exception);

        await base.OnDisconnectedAsync(exception);
    }

    private static string[] GetDistinctRoles(ClaimsPrincipal? user)
    {
        if (user is null)
            return [];

        return user.Claims
            .Where(static c => c.Type is ClaimTypes.Role or RoleClaimType)
            .Select(static c => c.Value?.Trim())
            .Where(static v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()!;
    }

    private static string BuildRoleGroupName(string role)
        => $"{RoleGroupPrefix}{role}";

    private void LogConnected(string userId, string connectionId, int roleGroupCount) => logger.LogInformation("SignalR connected: user={UserId}, connectionId={ConnectionId}, roleGroups={RoleGroupCount}", userId, connectionId, roleGroupCount);

    private void LogDisconnected(string userId, string connectionId) => logger.LogInformation("SignalR disconnected: user={UserId}, connectionId={ConnectionId}", userId, connectionId);

    private void LogDisconnectedWithError(string userId, string connectionId, Exception ex) => logger.LogWarning(ex, "SignalR disconnected with error: user={UserId}, connectionId={ConnectionId}", userId, connectionId);
}
