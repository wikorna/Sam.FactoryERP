namespace Auth.Domain.Entities;

/// <summary>
/// Many-to-many join entity: which <see cref="ApplicationRole"/> can access which <see cref="AppDefinition"/>.
/// Stored in <c>auth."RoleAppAccess"</c>.
/// </summary>
public class RoleAppAccess
{
    public Guid RoleId { get; set; }
    public Guid AppId { get; set; }

    // Navigation
    public virtual ApplicationRole Role { get; set; } = null!;
    public virtual AppDefinition App { get; set; } = null!;
}

