namespace Auth.Application.Interfaces;

/// <summary>
/// Checks whether a JWT's jti (unique token ID) has been revoked.
/// Used in the JWT Bearer validation pipeline to enforce access-token revocation.
/// </summary>
public interface IJtiBlacklistService
{
    /// <summary>Returns true if the JTI has been revoked.</summary>
    Task<bool> IsBlacklistedAsync(string jti, CancellationToken cancellationToken = default);

    /// <summary>Adds a JTI to the blacklist until the token's natural expiry.</summary>
    Task BlacklistAsync(string jti, DateTime expiresAtUtc, CancellationToken cancellationToken = default);
}
