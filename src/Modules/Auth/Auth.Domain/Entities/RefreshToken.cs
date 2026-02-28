namespace Auth.Domain.Entities;

/// <summary>
/// Server-side refresh token. Tokens are stored as HMACSHA256 hashes — never plain text.
/// Token families enable reuse detection: if a revoked token is replayed,
/// the entire family is revoked and the user must re-authenticate.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    /// <summary>HMACSHA256 hash of the raw refresh token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Family identifier grouping all tokens in a rotation chain.
    /// Reuse detection works by checking if a played token's family contains revoked siblings.
    /// </summary>
    public Guid Family { get; set; }

    public Guid UserId { get; set; }

    public bool IsRevoked { get; set; }

    /// <summary>When revoked via rotation, points to the replacement token.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>IP address that created this token (for audit).</summary>
    public string CreatedByIp { get; set; } = string.Empty;
    public string? RevokedByIp { get; set; }
    /// <summary>SHA256 hash of the User-Agent header (device fingerprint).</summary>
    public string UserAgentHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>When the token was revoked (null if still active).</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>Reason for revocation (rotation, logout, reuse-detection, admin).</summary>
    public string? RevokedReason { get; set; }
    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
    // Navigation
    public virtual ApplicationUser User { get; set; } = null!;
}
