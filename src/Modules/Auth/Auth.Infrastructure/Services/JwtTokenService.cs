using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.Application.Interfaces;
using Auth.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure.Services;

/// <summary>
/// RS256-based JWT token service. Signs tokens with the active RSA private key
/// and includes a 'kid' header for key-rotation support.
/// </summary>
internal sealed class JwtTokenService(
    IKeyStoreService keyStoreService,
    IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<(string Token, string Jti, DateTime ExpiresAtUtc)> GenerateAccessTokenAsync(
        Guid userId, string username, IEnumerable<string> roles,
        CancellationToken cancellationToken = default)
    {
        var (rsaKey, kid) = await keyStoreService.GetActiveSigningKeyAsync(cancellationToken);

        var jti = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var signingCredentials = new SigningCredentials(rsaKey, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            NotBefore = now,
            IssuedAt = now,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            SigningCredentials = signingCredentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(tokenDescriptor);
        token.Header["kid"] = kid;

        var tokenString = handler.WriteToken(token);
        return (tokenString, jti, expires);
    }

    public string? ExtractJtiFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
                return null;

            var jwt = handler.ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        }
        catch
        {
            return null;
        }
    }
}
