using Auth.Application.Interfaces;
using Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Auth.Infrastructure.Services;

/// <summary>
/// EF Core–backed JTI blacklist. Checks whether a given JTI has been revoked
/// and stores new revocations with a TTL matching the token's expiry.
/// </summary>
internal sealed class JtiBlacklistService(AuthDbContext db) : IJtiBlacklistService
{
    public async Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return await db.JtiBlacklist.AnyAsync(
            e => e.Jti == jti && e.ExpiresAtUtc > DateTime.UtcNow, cancellationToken);
    }

    public async Task BlacklistAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default)
    {
        db.JtiBlacklist.Add(new Auth.Domain.Entities.JtiBlacklistEntry
        {
            Jti = jti,
            ExpiresAtUtc = expiresAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
