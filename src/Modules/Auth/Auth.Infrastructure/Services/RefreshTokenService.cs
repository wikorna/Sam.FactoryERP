using System.Security.Cryptography;
using System.Text;
using Auth.Application.Interfaces;

namespace Auth.Infrastructure.Services;

/// <summary>
/// Refresh token service. Generates cryptographically secure random tokens
/// and hashes them with HMACSHA256 for safe server-side storage.
/// </summary>
internal sealed class RefreshTokenService : IRefreshTokenService
{
    // HMAC key for token hashing. In production, load from configuration/Key Vault.
    private static readonly byte[] HmacKey = "FactoryERP-RefreshToken-HMAC-Key-2026-Production"u8.ToArray();

    public string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }

    public string HashToken(string rawToken)
    {
        var tokenBytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = HMACSHA256.HashData(HmacKey, tokenBytes);
        return Convert.ToHexStringLower(hash);
    }
}
