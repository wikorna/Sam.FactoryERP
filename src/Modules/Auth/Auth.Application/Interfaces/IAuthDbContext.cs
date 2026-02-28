using Auth.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Auth.Application.Interfaces;

/// <summary>
/// Auth module's data-access abstraction.
/// Infrastructure implements this via EF Core; Application never sees EF internals.
/// </summary>
public interface IAuthDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<SigningKeyInfo> SigningKeys { get; }
    DbSet<JtiBlacklistEntry> JtiBlacklist { get; }
    DbSet<AppDefinition> Apps { get; }
    DbSet<RoleAppAccess> RoleAppAccess { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
