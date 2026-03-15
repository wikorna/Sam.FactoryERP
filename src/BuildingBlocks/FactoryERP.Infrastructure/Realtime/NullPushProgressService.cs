using FactoryERP.Abstractions.Realtime;
using Microsoft.Extensions.Logging;

namespace FactoryERP.Infrastructure.Realtime;

/// <summary>
/// No-op implementation of <see cref="IPushProgressService"/> used by
/// <c>WorkerHost</c> and any host that does not run a SignalR hub.
/// All calls are logged at <c>Debug</c> level and complete synchronously.
/// </summary>
public sealed class NullPushProgressService : IPushProgressService
{
    private readonly ILogger<NullPushProgressService> _logger;

    public NullPushProgressService(ILogger<NullPushProgressService> logger)
        => _logger = logger;

    /// <inheritdoc />
    public Task SendProgressAsync(
        string jobId, JobProgressDto progress, CancellationToken cancellationToken = default)
    {
        LogProgress(jobId, progress.Percent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JobCompletedAsync(
        string jobId, JobCompletedDto completed, CancellationToken cancellationToken = default)
    {
        LogCompleted(jobId, completed.ResultId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task JobFailedAsync(
        string jobId, JobFailedDto failed, CancellationToken cancellationToken = default)
    {
        LogFailed(jobId, failed.ErrorMessage);
        return Task.CompletedTask;
    }

    // ── Analyzer-compliant log helpers ───────────────────────────────────────

    private void LogProgress(string jobId, int percent) => _logger.LogDebug("NullProgressService: would send progress for job {JobId}, percent={Percent}", jobId, percent);

    private void LogCompleted(string jobId, string resultId) => _logger.LogDebug("NullProgressService: would send completed for job {JobId}, resultId={ResultId}", jobId, resultId);

    private void LogFailed(string jobId, string errorMessage) => _logger.LogDebug("NullProgressService: would send failed for job {JobId}, error={ErrorMessage}", jobId, errorMessage);
}

