using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Features.Logout;

/// <summary>
/// Revokes refresh token session and blacklists the current access token JTI
/// so it cannot be replayed until natural expiry.
/// </summary>
public sealed class LogoutCommandHandler(
    IAuthDbContext db,
    IRefreshTokenService refreshTokenService,
    IJwtTokenService jwtTokenService,
    IJtiBlacklistService jtiBlacklistService,
    ILogger<LogoutCommandHandler> logger) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Revoke refresh token if provided
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = refreshTokenService.HashToken(request.RefreshToken);
            var token = await db.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.TokenHash == hash && !rt.IsRevoked, cancellationToken);

            if (token is not null)
            {
                token.IsRevoked = true;
                token.RevokedAtUtc = DateTime.UtcNow;
                token.RevokedReason = "Logout";
                await db.SaveChangesAsync(cancellationToken);
                LogRefreshTokenRevoked(logger, token.UserId);
            }
        }

        // Blacklist access token JTI if provided
        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            var jti = jwtTokenService.ExtractJtiFromToken(request.AccessToken);
            if (jti is not null)
            {
                await jtiBlacklistService.BlacklistAsync(jti, DateTime.UtcNow.AddMinutes(30), cancellationToken);
                LogJtiBlacklisted(logger, jti);
            }
        }
    }

    private static void LogRefreshTokenRevoked(ILogger logger, Guid userId) => logger.LogInformation("Refresh token revoked via logout for UserId={UserId}", userId);

    private static void LogJtiBlacklisted(ILogger logger, string jti) => logger.LogInformation("Access token JTI blacklisted: {Jti}", jti);
}
