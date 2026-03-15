using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Features.ChangePassword;

/// <summary>
/// Changes the authenticated user's password.
/// Verifies the current password, hashes the new one, and revokes all
/// existing refresh tokens for security (force re-login on all devices).
/// </summary>
public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthDbContext _db;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        UserManager<ApplicationUser> userManager,
        IAuthDbContext db,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    public async Task<ChangePasswordResult> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null)
            return new ChangePasswordResult(false, "User not found.");

        // Verify and change current password
        var changeResult = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        
        if (!changeResult.Succeeded)
        {
            var errors = string.Join("; ", changeResult.Errors.Select(e => e.Description));
            LogPasswordChangeFailed(_logger, request.UserId, errors);
            return new ChangePasswordResult(false, errors);
        }

        // Revoke all refresh tokens → force re-login on all devices
        var activeTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAtUtc = DateTime.UtcNow;
            token.RevokedReason = "Password changed";
        }

        await _db.SaveChangesAsync(cancellationToken);

        LogPasswordChanged(_logger, user.Id, activeTokens.Count);

        return new ChangePasswordResult(true);
    }

    private static void LogPasswordChangeFailed(ILogger logger, Guid userId, string reason) => logger.LogWarning("Password change failed for UserId={UserId}: {Reason}", userId, reason);

    private static void LogPasswordChanged(ILogger logger, Guid userId, int revokedCount) => logger.LogInformation("Password changed for UserId={UserId}, {RevokedCount} refresh tokens revoked", userId, revokedCount);
}
