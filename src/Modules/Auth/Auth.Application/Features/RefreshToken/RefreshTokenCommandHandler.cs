using System.Security.Cryptography;
using System.Text;
using Auth.Application.Features.Login;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Features.RefreshToken;

/// <summary>
/// Rotates refresh tokens with reuse detection.
/// If a revoked token is replayed, the entire token family is revoked
/// and the user must re-authenticate (bank-grade protection against token theft).
/// </summary>
public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthTokenResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        UserManager<ApplicationUser> userManager,
        IAuthDbContext db,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _db = db;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<AuthTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _refreshTokenService.HashToken(request.RefreshToken);

        var existing = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existing is null)
        {
            LogRefreshFailed(_logger, "Token not found");
            throw new UnauthorizedAccessException("Invalid refresh token.");
        }

        // ──── Reuse detection ────
        if (existing.IsRevoked)
        {
            // A revoked token was replayed → compromise detected.
            // Revoke the entire family to force re-login.
            LogTokenReuse(_logger, existing.Family, existing.UserId);

            var familyTokens = await _db.RefreshTokens
                .Where(rt => rt.Family == existing.Family && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var ft in familyTokens)
            {
                ft.IsRevoked = true;
                ft.RevokedAtUtc = DateTime.UtcNow;
                ft.RevokedReason = "Reuse detection — family revoked";
            }

            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedAccessException("Refresh token reuse detected. Please re-authenticate.");
        }

        // ──── Expiry check ────
        if (existing.ExpiresAtUtc < DateTime.UtcNow)
        {
            LogRefreshFailed(_logger, "Token expired");
            throw new UnauthorizedAccessException("Refresh token has expired.");
        }

        // ──── User status check ────
        if (!existing.User.IsActive)
        {
            LogRefreshFailed(_logger, "User account disabled");
            throw new UnauthorizedAccessException("Account is disabled.");
        }

        // ──── Rotate ────
        // 1. Revoke old token
        existing.IsRevoked = true;
        existing.RevokedAtUtc = DateTime.UtcNow;
        existing.RevokedReason = "Rotated";

        // 2. Create new token in the same family
        var rawNewToken = _refreshTokenService.GenerateRawToken();
        var newTokenHash = _refreshTokenService.HashToken(rawNewToken);

        var newRefreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = newTokenHash,
            Family = existing.Family, // same family for reuse detection
            UserId = existing.UserId,
            CreatedByIp = request.IpAddress,
            UserAgentHash = ComputeSha256(request.UserAgent),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
        };

        existing.ReplacedByTokenId = newRefreshToken.Id;
        _db.RefreshTokens.Add(newRefreshToken);

        // 3. Generate new access token
        var user = existing.User;
        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, _, expiresAtUtc) = await _jwtTokenService.GenerateAccessTokenAsync(
            user.Id, user.UserName ?? user.Email ?? string.Empty, roles, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        LogRefreshSuccess(_logger, user.Id);

        return new AuthTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawNewToken,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static void LogRefreshFailed(ILogger logger, string reason) => logger.LogWarning("Refresh token failed: {Reason}", reason);

    private static void LogTokenReuse(ILogger logger, Guid family, Guid userId) => logger.LogError("Refresh token reuse detected! Family={Family}, UserId={UserId}. Revoking entire family.", family, userId);

    private static void LogRefreshSuccess(ILogger logger, Guid userId) => logger.LogInformation("Refresh token rotated for UserId={UserId}", userId);
}
