namespace Auth.Domain.Entities;

/// <summary>
/// Metadata for an RSA signing key. The actual private key material
/// is stored externally (file system / Key Vault) — this entity
/// only tracks lifecycle for JWKS publishing and key rotation.
/// </summary>
public sealed class SigningKeyInfo
{
    /// <summary>Key identifier (kid) included in JWT headers.</summary>
    public string Kid { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime NotBeforeUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>When true this key is used for signing new tokens.</summary>
    public bool IsActive { get; set; }
}
