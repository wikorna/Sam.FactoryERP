namespace FactoryERP.Abstractions.Caching;

/// <summary>
/// Transport-agnostic caching abstraction.
/// Application layer depends on this; Infrastructure implements it via HybridCache + Redis.
/// </summary>
public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntrySettings? options = null,
        CancellationToken ct = default)
        where T : notnull;

    Task SetAsync<T>(
        string key,
        T value,
        CacheEntrySettings? options = null,
        CancellationToken ct = default)
        where T : notnull;

    Task RemoveAsync(string key, CancellationToken ct = default);

    Task InvalidateTagAsync(string tag, CancellationToken ct = default);
}


/// <summary>
/// Per-entry cache options. When <c>null</c>, the global defaults from <c>CacheOptions</c> are used.
/// </summary>
public sealed record CacheEntrySettings
{
    public TimeSpan? AbsoluteExpiration { get; init; }
    public TimeSpan? SlidingExpiration { get; init; }

    public TimeSpan? L1Expiration { get; init; }

    public IReadOnlyCollection<string>? Tags { get; init; }

    public bool EnableCompression { get; init; } = true;

    public bool EnableEncryption { get; init; }
    public CacheEntrySettings Normalize() { if (Tags is null || Tags.Count == 0) return this; var normalized = Tags .Where(t => !string.IsNullOrWhiteSpace(t)) .Select(t => t.Trim()) .Distinct(StringComparer.OrdinalIgnoreCase) .ToArray(); return this with { Tags = normalized.Length == 0 ? null : normalized }; }
}

