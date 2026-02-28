namespace Auth.Domain.Entities;

/// <summary>
/// Revoked JWT access-token identifier (jti).
/// Entries are kept until the original token's expiry, then pruned.
/// </summary>
public sealed class JtiBlacklistEntry
{
    /// <summary>The JWT 'jti' claim value.</summary>
    public string Jti { get; set; } = string.Empty;

    /// <summary>UTC time when the original access token expires. Used for TTL-based cleanup.</summary>
    public DateTime ExpiresAtUtc { get; set; }
}
