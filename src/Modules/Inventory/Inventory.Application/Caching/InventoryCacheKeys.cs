using FactoryERP.Abstractions.Caching;

namespace Inventory.Application.Caching;

/// <summary>
/// Centralized cache key/tag/TTL constants for the Inventory module.
/// Convention: <c>{module}:{entity}:{version}:{identifier}</c> — all lowercase via <see cref="CacheKey.Create"/>.
/// </summary>
public static class InventoryCacheKeys
{
    // ── Schema version (increment on DTO shape change to avoid deserialization errors) ──
    private const string Version = "v1";

    // ── Tags ──
    public const string TagItems = "inventory:items";
    public const string TagMetadata = "inventory:metadata";

    public static string TagItem(Guid itemId) => $"inventory:item:{itemId}";

    // ── Keys ──
    public static string ItemById(Guid id)
        => CacheKey.Create("inventory", "item", Version, id.ToString());

    public static string ItemValueHelp(string searchHash)
        => CacheKey.Create("inventory", "items", "valuehelp", Version, searchHash);

    public static string ItemsList(string filterHash)
        => CacheKey.Create("inventory", "items", "list", Version, filterHash);

    public static string ItemsMetadata()
        => CacheKey.Create("inventory", "items", "metadata", Version);

    // ── TTL presets (per CachingConventions.md — Master Data) ──
    public static CacheEntrySettings MasterData(params string[] tags) => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(60),
        L1Expiration = TimeSpan.FromMinutes(5),
        Tags = tags
    };

    public static CacheEntrySettings ValueHelp(params string[] tags) => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(15),
        L1Expiration = TimeSpan.FromMinutes(3),
        Tags = tags
    };

    public static CacheEntrySettings StaticMetadata(params string[] tags) => new()
    {
        AbsoluteExpiration = TimeSpan.FromHours(24),
        L1Expiration = TimeSpan.FromMinutes(60),
        Tags = tags
    };

    public static CacheEntrySettings PaginatedList(params string[] tags) => new()
    {
        AbsoluteExpiration = TimeSpan.FromMinutes(10),
        L1Expiration = TimeSpan.FromMinutes(2),
        Tags = tags
    };
}

