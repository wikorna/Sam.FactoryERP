using System.Security.Cryptography;
using System.Text;
using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Features.Login;

/// <summary>
/// Handles user authentication: validates credentials, enforces lockout,
/// issues access + refresh tokens, and records audit events.
/// Returns <see cref="LoginResult"/> (Either pattern) instead of throwing
/// so the controller can return lockout metadata without exposing user existence.
/// </summary>
public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        IAuthDbContext db,
        IJwtTokenService jwtTokenService,
        IRefreshTokenService refreshTokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _userManager = userManager;
        _db = db;
        _jwtTokenService = jwtTokenService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(request.Username);

        // ── User not found (generic error — no user enumeration) ──
        if (user is null)
        {
            // Dummy hash check to prevent timing attacks
            var dummyUser = new ApplicationUser();
            dummyUser.PasswordHash = _userManager.PasswordHasher.HashPassword(dummyUser, "dummy");
            _userManager.PasswordHasher.VerifyHashedPassword(dummyUser, dummyUser.PasswordHash, request.Password);

            LogLoginFailed(_logger, request.Username, "User not found or invalid password");
            return LoginResult.Fail(new LoginFailedResponse());
        }

        if (!user.IsActive)
        {
            LogLoginFailed(_logger, request.Username, "Account disabled");
            return LoginResult.Fail(new LoginFailedResponse());
        }

        // ── Lockout check ──
        if (await _userManager.IsLockedOutAsync(user))
        {
            LogLoginFailed(_logger, request.Username, "Account locked");
            return LoginResult.Fail(new LoginFailedResponse
            {
                LockoutEndsAtUtc = user.LockoutEnd?.UtcDateTime ?? DateTime.UtcNow,
                RemainingAttempts = 0
            });
        }

        // ── Password verification ──
        bool isValid = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!isValid)
        {
            await _userManager.AccessFailedAsync(user);
            
            if (await _userManager.IsLockedOutAsync(user))
            {
                LogLoginFailed(_logger, request.Username, "Account locked due to repeated failures");
                return LoginResult.Fail(new LoginFailedResponse
                {
                    RemainingAttempts = 0,
                    LockoutEndsAtUtc = user.LockoutEnd?.UtcDateTime ?? DateTime.UtcNow
                });
            }

            LogLoginFailed(_logger, request.Username, "Invalid password");
            var remaining = _userManager.Options.Lockout.MaxFailedAccessAttempts - await _userManager.GetAccessFailedCountAsync(user);
            return LoginResult.Fail(new LoginFailedResponse { RemainingAttempts = remaining < 0 ? 0 : remaining });
        }

        // ── Success ──
        await _userManager.ResetAccessFailedCountAsync(user);
        user.LastLoginAtUtc = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);

        var (accessToken, _, expiresAtUtc) = await _jwtTokenService.GenerateAccessTokenAsync(
            user.Id, user.UserName ?? user.Email ?? string.Empty, roles, cancellationToken);

        // Create refresh token
        var rawRefresh = _refreshTokenService.GenerateRawToken();
        var refreshHash = _refreshTokenService.HashToken(rawRefresh);
        var family = Guid.NewGuid();

        var refreshToken = new Auth.Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            TokenHash = refreshHash,
            Family = family,
            UserId = user.Id,
            CreatedByIp = request.IpAddress,
            UserAgentHash = ComputeSha256(request.UserAgent),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        LogLoginSuccess(_logger, user.Id, user.UserName ?? string.Empty);

        return LoginResult.Ok(new AuthTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefresh,
            ExpiresAtUtc = expiresAtUtc
        });
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }

    private static void LogLoginFailed(ILogger logger, string username, string reason) => logger.LogWarning("Login failed for user '{Username}': {Reason}", username, reason);

    private static void LogLoginSuccess(ILogger logger, Guid userId, string username) => logger.LogInformation("Login succeeded for UserId={UserId}, Username='{Username}'", userId, username);
}
