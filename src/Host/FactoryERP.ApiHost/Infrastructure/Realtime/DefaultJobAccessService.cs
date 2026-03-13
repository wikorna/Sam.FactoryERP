using FactoryERP.Abstractions.Realtime;

namespace FactoryERP.ApiHost.Infrastructure.Realtime;

/// <summary>
/// Default implementation of <see cref="IJobAccessService"/> that allows any
/// authenticated user to observe job progress.
/// </summary>
/// <remarks>
/// <para>
/// Groups in SignalR are <b>not</b> a security boundary — they merely control
/// message routing. The real authorization happens at the API endpoint level when
/// the job is created or queried. This service provides an additional guard at
/// hub-join time.
/// </para>
/// <para>
/// For production hardening, replace this with a composite that delegates to
/// module-specific repositories (EDI jobs, Print jobs, etc.) to verify the
/// authenticated user owns or is permitted to observe the requested job.
/// </para>
/// </remarks>
public sealed partial class DefaultJobAccessService : IJobAccessService
{
    private readonly ILogger<DefaultJobAccessService> _logger;

    public DefaultJobAccessService(ILogger<DefaultJobAccessService> logger)
        => _logger = logger;

    /// <inheritdoc />
    public Task<bool> CanAccessJobAsync(
        string userId,
        string jobId,
        CancellationToken cancellationToken = default)
    {
        // Phase 1: any authenticated user can watch any job.
        // The userId is already validated by the [Authorize] hub attribute +
        // the ProgressHub.JoinGroup null-check.
        //
        // Phase 2 TODO: query module repositories to verify ownership:
        //   - EdiFileJobRepository.GetByIdAsync(jobId) → check CreatedByUserId
        //   - PrintJobRepository.GetByIdAsync(jobId) → check RequestedBy
        //   - Or check if user has Admin / Supervisor role

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(jobId))
            return Task.FromResult(false);

        LogAccessGranted(userId, jobId);
        return Task.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Job access granted: user={UserId}, jobId={JobId}")]
    private partial void LogAccessGranted(string userId, string jobId);
}

