using Auth.Application.Interfaces;
using Auth.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Auth.Application.Features.CurrentUser;

/// <summary>
/// Loads the authenticated user's profile: display name, roles, and accessible ERP apps.
/// Apps are resolved from the <c>RoleAppAccess</c> join table — only active apps are returned.
/// </summary>
public sealed class GetMeQueryHandler : IRequestHandler<GetMeQuery, MeResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuthDbContext _db;
    private readonly ILogger<GetMeQueryHandler> _logger;

    public GetMeQueryHandler(
        UserManager<ApplicationUser> userManager,
        IAuthDbContext db,
        ILogger<GetMeQueryHandler> logger)
    {
        _userManager = userManager;
        _db = db;
        _logger = logger;
    }

    public async Task<MeResponse> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId.ToString());

        if (user is null || !user.IsActive)
        {
            LogUserNotFound(_logger, request.UserId);
            throw new UnauthorizedAccessException("User not found or inactive.");
        }

        var roles = await _userManager.GetRolesAsync(user);

        // Resolve accessible apps via role-app mapping (only active apps, ordered by SortOrder)
        var roleIds = await _db.RoleAppAccess
            .AsNoTracking()
            .Where(ra => roles.Contains(ra.Role.Name!))
            .Select(ra => ra.AppId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var apps = await _db.Apps
            .AsNoTracking()
            .Where(a => roleIds.Contains(a.Id) && a.IsActive)
            .OrderBy(a => a.SortOrder)
            .Select(a => new AppDto
            {
                Key = a.Key,
                Title = a.Title,
                Route = a.Route,
            })
            .ToListAsync(cancellationToken);

        LogMeResolved(_logger, request.UserId, roles.Count, apps.Count);

        return new MeResponse
        {
            UserId = request.UserId,
            DisplayName = user.DisplayName,
            Roles = roles.ToList().AsReadOnly(),
            Apps = apps.AsReadOnly(),
        };
    }

    private static void LogUserNotFound(ILogger logger, Guid userId) => logger.LogWarning("GetMe: user not found or inactive (UserId={UserId})", userId);

    private static void LogMeResolved(ILogger logger, Guid userId, int roleCount, int appCount) => logger.LogDebug("GetMe resolved UserId={UserId}: {RoleCount} role(s), {AppCount} app(s)", userId, roleCount, appCount);
}

