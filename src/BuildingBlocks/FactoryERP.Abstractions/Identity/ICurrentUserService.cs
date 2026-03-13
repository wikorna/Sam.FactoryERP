namespace FactoryERP.Abstractions.Identity;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string? RequestedBy { get; } // For audit, usually username or email
    Guid? DepartmentId { get; }
    Guid? StoreId { get; }
    bool HasPermission(string permission);
    bool IsInRole(string role);
}

