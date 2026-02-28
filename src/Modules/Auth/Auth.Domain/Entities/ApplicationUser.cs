using Microsoft.AspNetCore.Identity;

namespace Auth.Domain.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    
    // Navigation property
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
