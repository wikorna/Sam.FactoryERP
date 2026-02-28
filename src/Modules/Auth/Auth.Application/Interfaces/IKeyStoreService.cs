using Microsoft.IdentityModel.Tokens;

namespace Auth.Application.Interfaces;

/// <summary>
/// Manages RSA signing keys for JWT token generation and JWKS publishing.
/// </summary>
public interface IKeyStoreService
{
    /// <summary>Returns the current active RSA key for signing new tokens (includes private key).</summary>
    Task<(RsaSecurityKey Key, string Kid)> GetActiveSigningKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns all non-expired public keys for JWKS endpoint / token validation.</summary>
    Task<IReadOnlyList<(RsaSecurityKey Key, string Kid)>> GetValidationKeysAsync(CancellationToken cancellationToken = default);
}
