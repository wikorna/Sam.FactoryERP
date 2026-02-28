using System.Security.Cryptography;

namespace Auth.Application.Interfaces;

/// <summary>
/// Generates and validates JWT access tokens using RS256.
/// Infrastructure owns the RSA key material; Application sees only this contract.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Creates a signed JWT access token.
    /// </summary>
    /// <param name="userId">Subject claim.</param>
    /// <param name="username">Username claim.</param>
    /// <param name="roles">Role claims.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tuple of (token string, jti, expiresAtUtc).</returns>
    Task<(string Token, string Jti, DateTime ExpiresAtUtc)> GenerateAccessTokenAsync(
        Guid userId, string username, IEnumerable<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the JTI claim from a token without full validation (for blacklisting on logout).
    /// </summary>
    string? ExtractJtiFromToken(string token);
}
