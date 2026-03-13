namespace FactoryERP.Abstractions.Realtime;

/// <summary>
/// Authorizes whether a user is allowed to watch the progress of a specific job.
/// The implementation should query the job store to verify ownership or role permissions.
/// </summary>
public interface IJobAccessService
{
    /// <summary>
    /// Returns <c>true</c> if the user identified by <paramref name="userId"/>
    /// is allowed to observe progress for the job identified by <paramref name="jobId"/>.
    /// </summary>
    Task<bool> CanAccessJobAsync(
        string userId,
        string jobId,
        CancellationToken cancellationToken = default);
}

