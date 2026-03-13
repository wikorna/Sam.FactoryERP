using FactoryERP.Abstractions.Realtime;
using FactoryERP.ApiHost.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace FactoryERP.ApiHost.Infrastructure.Realtime;

/// <summary>
/// Production implementation of <see cref="IPushProgressService"/> that
/// pushes job progress updates to connected Angular clients via the
/// <see cref="ProgressHub"/> SignalR hub.
/// </summary>
/// <remarks>
/// Registered as <b>Scoped</b> in ApiHost DI.
/// WorkerHost uses <see cref="FactoryERP.Infrastructure.Realtime.NullPushProgressService"/> instead.
/// </remarks>
public sealed partial class SignalRPushProgressService : IPushProgressService
{
    private readonly IHubContext<ProgressHub, IProgressClient> _hubContext;
    private readonly ILogger<SignalRPushProgressService> _logger;

    public SignalRPushProgressService(
        IHubContext<ProgressHub, IProgressClient> hubContext,
        ILogger<SignalRPushProgressService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendProgressAsync(
        string jobId,
        JobProgressDto progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        await _hubContext.Clients
            .Group(ProgressHub.BuildJobGroupName(jobId))
            .ReceiveProgress(progress);

        LogProgress(jobId, progress.Percent);
    }

    /// <inheritdoc />
    public async Task JobCompletedAsync(
        string jobId,
        JobCompletedDto completed,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        await _hubContext.Clients
            .Group(ProgressHub.BuildJobGroupName(jobId))
            .JobCompleted(completed);

        LogCompleted(jobId, completed.ResultId);
    }

    /// <inheritdoc />
    public async Task JobFailedAsync(
        string jobId,
        JobFailedDto failed,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        await _hubContext.Clients
            .Group(ProgressHub.BuildJobGroupName(jobId))
            .JobFailed(failed);

        LogFailed(jobId, failed.ErrorMessage);
    }

    // ── Analyzer-compliant log helpers ───────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Pushed progress for job {JobId}: {Percent}%")]
    private partial void LogProgress(string jobId, int percent);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Pushed job completed for {JobId}, resultId={ResultId}")]
    private partial void LogCompleted(string jobId, string resultId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Pushed job failed for {JobId}: {ErrorMessage}")]
    private partial void LogFailed(string jobId, string errorMessage);
}

