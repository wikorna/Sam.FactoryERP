using FactoryERP.Abstractions.Realtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FactoryERP.ApiHost.Hubs;

[Authorize]
public sealed partial class ProgressHub : Hub<IProgressClient>
{
    private readonly ILogger<ProgressHub> _logger;
    private readonly IJobAccessService _jobAccessService;

    public ProgressHub(
        ILogger<ProgressHub> logger,
        IJobAccessService jobAccessService)
    {
        _logger = logger;
        _jobAccessService = jobAccessService;
    }

    public async Task JoinGroup(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new HubException("Invalid job id.");

        var userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId))
            throw new HubException("Unauthorized.");

        var allowed = await _jobAccessService.CanAccessJobAsync(
            userId,
            jobId,
            Context.ConnectionAborted);

        if (!allowed)
        {
            LogJoinDenied(userId, jobId);
            throw new HubException("You are not allowed to access this job.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, BuildJobGroupName(jobId));
        LogJoined(userId, jobId);
    }

    public async Task LeaveGroup(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            return;

        var userId = Context.UserIdentifier ?? "(anonymous)";

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, BuildJobGroupName(jobId));
        LogLeft(userId, jobId);
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? "(anonymous)";
        LogConnected(userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier ?? "(anonymous)";

        if (exception is null)
            LogDisconnected(userId);
        else
            LogDisconnectedWithError(userId, exception);

        await base.OnDisconnectedAsync(exception);
    }

    internal static string BuildJobGroupName(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
            throw new ArgumentException("Job id is required.", nameof(jobId));

        return $"job:{jobId.Trim()}";
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Progress hub connected: user={UserId}")]
    private partial void LogConnected(string userId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Progress hub disconnected: user={UserId}")]
    private partial void LogDisconnected(string userId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Progress hub disconnected with error: user={UserId}")]
    private partial void LogDisconnectedWithError(string userId, Exception ex);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Progress hub group joined: user={UserId}, jobId={JobId}")]
    private partial void LogJoined(string userId, string jobId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Progress hub group left: user={UserId}, jobId={JobId}")]
    private partial void LogLeft(string userId, string jobId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Progress hub join denied: user={UserId}, jobId={JobId}")]
    private partial void LogJoinDenied(string userId, string jobId);
}
