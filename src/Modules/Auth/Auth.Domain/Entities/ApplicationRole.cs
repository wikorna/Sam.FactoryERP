using Microsoft.AspNetCore.Identity;

namespace Auth.Domain.Entities;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }
    public ApplicationRole(string name) : base(name) { }

    // Navigation
    public virtual ICollection<RoleAppAccess> RoleAppAccesses { get; set; } = [];
}
