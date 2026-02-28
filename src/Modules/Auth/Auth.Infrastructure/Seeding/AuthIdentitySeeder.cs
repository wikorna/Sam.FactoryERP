using Auth.Domain.Entities;
using Auth.Domain.Enums;
using Auth.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Auth.Infrastructure.Seeding;

/// <summary>
/// Seeds default admin user, roles, apps, and role-app mappings for Development/SIT environments.
/// Uses <see cref="UserManager{TUser}"/> and <see cref="RoleManager{TRole}"/> for correct hashing/normalization.
/// Idempotent: running multiple times does not create duplicates.
/// </summary>
public static partial class AuthIdentitySeeder
{
    // Dev-only defaults — meets Identity password policy (≥12 chars, upper, lower, digit, special).
    private const string DefaultUserName    = "wikorna";
    private const string DefaultEmail       = "wikorna@gmail.com";
    private const string DefaultDisplayName = "Wikorn";
    private const string DefaultPassword    = "Dev@dmin2024!";

    /// <summary>
    /// Orchestrates all dev-environment seeding: roles → user → apps → role-app mappings.
    /// Must be called inside a DI scope.
    /// </summary>
    public static async Task SeedDevUserAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct = default)
    {
        await SeedRolesAsync(services, logger);
        await SeedUserAsync(services, logger);
        await SeedAppsAndRoleAccessAsync(services, logger, ct);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Roles
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task SeedRolesAsync(IServiceProvider services, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        string[] roles = [AuthRole.Admin, AuthRole.Manager, AuthRole.Supervisor, AuthRole.Operator, AuthRole.Auditor];

        foreach (var roleName in roles)
        {
            if (await roleManager.RoleExistsAsync(roleName))
                continue;

            var result = await roleManager.CreateAsync(new ApplicationRole(roleName));
            if (result.Succeeded)
            {
                LogRoleCreated(logger, roleName);
            }
            else
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                LogRoleCreationFailed(logger, roleName, errors);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // User
    // ══════════════════════════════════════════════════════════════════════════

    private static async Task SeedUserAsync(IServiceProvider services, ILogger logger)
    {
        LogSeedingStarted(logger);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await userManager.FindByNameAsync(DefaultUserName);

        if (existing is not null)
        {
            LogUserAlreadyExists(logger, existing.UserName ?? DefaultUserName, existing.Id);

            // Ensure flags are correct (idempotent fix-up)
            var needsUpdate = false;

            if (!existing.EmailConfirmed)
            {
                existing.EmailConfirmed = true;
                needsUpdate = true;
            }

            if (!existing.IsActive)
            {
                existing.IsActive = true;
                needsUpdate = true;
            }

            if (needsUpdate)
            {
                var updateResult = await userManager.UpdateAsync(existing);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join("; ", updateResult.Errors.Select(e => e.Description));
                    LogFixUpFailed(logger, errors);
                }
                else
                {
                    LogFixUpApplied(logger);
                }
            }

            // Ensure user is in Admin role
            if (!await userManager.IsInRoleAsync(existing, AuthRole.Admin))
            {
                await userManager.AddToRoleAsync(existing, AuthRole.Admin);
                LogUserRoleAssigned(logger, existing.UserName ?? DefaultUserName, AuthRole.Admin);
            }

            return;
        }

        // Create new user
        var user = new ApplicationUser
        {
            UserName         = DefaultUserName,
            Email            = DefaultEmail,
            EmailConfirmed   = true,
            IsActive         = true,
            DisplayName      = DefaultDisplayName,
            TwoFactorEnabled = false,
            CreatedAtUtc     = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(user, DefaultPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            LogCreationFailed(logger, errors);
            throw new InvalidOperationException(
                $"Failed to seed dev user '{DefaultUserName}': {errors}");
        }

        // Assign Admin role
        await userManager.AddToRoleAsync(user, AuthRole.Admin);
        LogUserRoleAssigned(logger, user.UserName ?? DefaultUserName, AuthRole.Admin);

        LogUserCreated(logger, user.UserName ?? DefaultUserName, user.Id);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Apps + RoleAppAccess
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>App catalog — defines all ERP modules available in the system.</summary>
    private static readonly (string Key, string Title, string Route, string? Icon, int Sort)[] AppCatalog =
    [
        ("admin",      "Admin",                "/admin",      "pi pi-cog",       0),
        ("sales",      "Sales",                "/sales",      "pi pi-shopping-cart", 10),
        ("purchasing", "Purchasing",           "/purchasing", "pi pi-truck",     20),
        ("inventory",  "Inventory",            "/inventory",  "pi pi-box",       30),
        ("production", "Production",           "/production", "pi pi-wrench",    40),
        ("quality",    "Quality",              "/quality",    "pi pi-check-circle", 50),
        ("costing",    "Costing",              "/costing",    "pi pi-calculator", 60),
        ("labeling",   "Labeling",             "/labeling",   "pi pi-tag",       70),
        ("edi",        "EDI",                  "/edi",        "pi pi-exchange",  80),
        ("notification","Notification",        "/notification","pi pi-bell",     90),
    ];

    /// <summary>Role → app key mapping. Admin gets all; others get specific subsets.</summary>
    private static readonly Dictionary<string, string[]> RoleAppMapping = new()
    {
        [AuthRole.Admin]      = AppCatalog.Select(a => a.Key).ToArray(),
        [AuthRole.Manager]    = ["sales", "purchasing", "inventory", "production", "quality", "costing", "labeling", "edi"],
        [AuthRole.Supervisor] = ["inventory", "production", "quality", "labeling"],
        [AuthRole.Operator]   = ["inventory", "production", "labeling"],
        [AuthRole.Auditor]    = ["inventory", "quality", "costing", "sales"],
    };

    private static async Task SeedAppsAndRoleAccessAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct)
    {
        var db = services.GetRequiredService<AuthDbContext>();

        // ── Seed AppDefinitions ──
        var existingKeys = await db.Apps
            .AsNoTracking()
            .Select(a => a.Key)
            .ToListAsync(ct);

        var newApps = new List<AppDefinition>();
        foreach (var (key, title, route, icon, sort) in AppCatalog)
        {
            if (existingKeys.Contains(key))
                continue;

            newApps.Add(new AppDefinition
            {
                Id           = Guid.NewGuid(),
                Key          = key,
                Title        = title,
                Route        = route,
                IconCssClass = icon,
                IsActive     = true,
                SortOrder    = sort,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        if (newApps.Count > 0)
        {
            db.Apps.AddRange(newApps);
            await db.SaveChangesAsync(ct);
            LogAppsSeeded(logger, newApps.Count);
        }

        // ── Seed RoleAppAccess mappings ──
        var allApps = await db.Apps
            .AsNoTracking()
            .ToDictionaryAsync(a => a.Key, a => a.Id, ct);

        var allRoles = await db.Roles
            .AsNoTracking()
            .Where(r => r.NormalizedName != null)
            .ToDictionaryAsync(r => r.NormalizedName!, r => r.Id, ct);

        var existingMappings = await db.RoleAppAccess
            .AsNoTracking()
            .Select(ra => new { ra.RoleId, ra.AppId })
            .ToListAsync(ct);

        var existingSet = existingMappings
            .Select(m => (m.RoleId, m.AppId))
            .ToHashSet();

        var newMappings = new List<RoleAppAccess>();

        foreach (var (roleName, appKeys) in RoleAppMapping)
        {
            var normalizedRole = roleName.ToUpperInvariant();
            if (!allRoles.TryGetValue(normalizedRole, out var roleId))
            {
                LogRoleNotFoundForMapping(logger, roleName);
                continue;
            }

            foreach (var appKey in appKeys)
            {
                if (!allApps.TryGetValue(appKey, out var appId))
                    continue;

                if (existingSet.Contains((roleId, appId)))
                    continue;

                newMappings.Add(new RoleAppAccess { RoleId = roleId, AppId = appId });
            }
        }

        if (newMappings.Count > 0)
        {
            db.RoleAppAccess.AddRange(newMappings);
            await db.SaveChangesAsync(ct);
            LogRoleAppMappingsSeeded(logger, newMappings.Count);
        }
    }

    // ── High-performance LoggerMessage delegates ─────────────────────────────

    [LoggerMessage(Level = LogLevel.Information, Message = "Auth dev-user seeding started")]
    private static partial void LogSeedingStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auth dev-user '{UserName}' already exists (Id={UserId}), skipping creation")]
    private static partial void LogUserAlreadyExists(ILogger logger, string userName, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auth dev-user fix-up failed: {Errors}")]
    private static partial void LogFixUpFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auth dev-user flags updated (EmailConfirmed, IsActive)")]
    private static partial void LogFixUpApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Auth dev-user creation failed: {Errors}")]
    private static partial void LogCreationFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auth dev-user '{UserName}' created successfully (Id={UserId})")]
    private static partial void LogUserCreated(ILogger logger, string userName, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auth role '{RoleName}' created")]
    private static partial void LogRoleCreated(ILogger logger, string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Auth role '{RoleName}' creation failed: {Errors}")]
    private static partial void LogRoleCreationFailed(ILogger logger, string roleName, string errors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Auth user '{UserName}' assigned to role '{RoleName}'")]
    private static partial void LogUserRoleAssigned(ILogger logger, string userName, string roleName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} app definition(s)")]
    private static partial void LogAppsSeeded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} role-app access mapping(s)")]
    private static partial void LogRoleAppMappingsSeeded(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Role '{RoleName}' not found in DB — skipping role-app mapping")]
    private static partial void LogRoleNotFoundForMapping(ILogger logger, string roleName);
}
