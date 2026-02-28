using FactoryERP.Abstractions.Caching;

namespace Auth.Application.Caching;

/// <summary>
/// Centralized cache key/tag/TTL constants for the Auth module.
/// Convention: <c>{module}:{entity}:{version}:{identifier}</c> — all lowercase via <see cref="CacheKey.Create"/>.
/// </summary>
public static class AuthCacheKeys
{
    private const string Version = "v1";

    // ── Tags ──
    public const string TagUsers = "auth:users";
    public const string TagApps  = "auth:apps";

    public static string TagUser(Guid userId) => $"auth:user:{userId}";

    // ── Keys ──
    public static string UserProfile(Guid userId)
        => CacheKey.Create("auth", "user", Version, userId.ToString());

    // ── TTL presets (per CachingConventions.md — Auth/Security) ──

    /// <summary>User profile + apps: called every page load. L2=10 min, L1=2 min.</summary>
    public static CacheEntrySettings UserProfileSettings(Guid userId) => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(10),
        L1Expiration = TimeSpan.FromMinutes(2),
        Tags = [TagUsers, TagApps, TagUser(userId)]
    };
}

