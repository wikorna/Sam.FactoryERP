namespace Auth.Application.Interfaces;

/// <summary>
/// Manages refresh token lifecycle: creation, hashing, rotation, and reuse detection.
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>Generates a cryptographically random raw refresh token (Base64Url, 32+ bytes).</summary>
    string GenerateRawToken();

    /// <summary>Produces an HMACSHA256 hash of the raw token for safe storage.</summary>
    string HashToken(string rawToken);
}
