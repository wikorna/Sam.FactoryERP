using FactoryERP.Abstractions.Identity;

namespace FactoryERP.WorkerHost.Auth;

/// <summary>
/// A dummy implementation of ICurrentUserService for background workers
/// where no HTTP context or user principal is available.
/// </summary>
public sealed class WorkerCurrentUserService : ICurrentUserService
{
    public Guid UserId => Guid.Empty; // System or unidentified
    public string? RequestedBy => "System"; // Background worker
    public Guid? DepartmentId => null;
    public Guid? StoreId => null;

    public bool HasPermission(string permission)
    {
        // Background tasks typically have full permission if they need to check,
        // or no permission if meant to restrict user actions.
        // Since workers execute trusted commands from the queue,
        // they usually bypass auth checks or assume system privilege.
        return true;
    }

    public bool IsInRole(string role)
    {
        return true; // System role
    }
}

