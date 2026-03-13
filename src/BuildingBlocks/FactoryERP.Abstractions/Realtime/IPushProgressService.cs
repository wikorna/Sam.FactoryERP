namespace FactoryERP.Abstractions.Realtime;

/// <summary>
/// Application-level abstraction for pushing real-time job progress to clients.
/// Production implementation lives in ApiHost and uses <c>IHubContext&lt;ProgressHub&gt;</c>.
/// WorkerHost uses <c>NullPushProgressService</c> — a no-op that logs without blocking.
/// </summary>
public interface IPushProgressService
{
    /// <summary>Pushes a progress update to all clients watching the specified job.</summary>
    Task SendProgressAsync(
        string jobId,
        JobProgressDto progress,
        CancellationToken cancellationToken = default);

    /// <summary>Pushes a job-completed notification to all clients watching the specified job.</summary>
    Task JobCompletedAsync(
        string jobId,
        JobCompletedDto completed,
        CancellationToken cancellationToken = default);

    /// <summary>Pushes a job-failed notification to all clients watching the specified job.</summary>
    Task JobFailedAsync(
        string jobId,
        JobFailedDto failed,
        CancellationToken cancellationToken = default);
}

