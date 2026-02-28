namespace FactoryERP.Infrastructure.Caching;

using Microsoft.Extensions.Options;

/// <summary>
/// All caching configuration, bound from the <c>Cache</c> section.
/// Redis L2 is controlled by the nested <see cref="RedisSettings"/> object.
/// </summary>
/// <example>
/// Disabled (default — L1 in-memory only):
/// <code>
/// "Cache": { "Redis": { "Enabled": false } }
/// </code>
/// Enabled:
/// <code>
/// "Cache": { "Redis": { "Enabled": true, "ConnectionString": "localhost:6379,password=secret,abortConnect=false" } }
/// </code>
/// </example>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>Default absolute expiration for cache entries (minutes). Default: 60.</summary>
    public int DefaultExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Default L1 in-memory expiration (minutes).
    /// Must be ≤ <see cref="DefaultExpirationMinutes"/>. Default: 5.
    /// </summary>
    public int LocalCacheExpirationMinutes { get; set; } = 5;

    /// <summary>Maximum serialized payload per cache entry (bytes). Default: 1 MB.</summary>
    public int MaximumPayloadBytes { get; set; } = 1_048_576;

    /// <summary>Maximum length of a cache key. Default: 1024.</summary>
    public int MaximumKeyLength { get; set; } = 1024;

    /// <summary>Redis L2 backend settings. Disabled by default.</summary>
    public RedisSettings Redis { get; set; } = new();
}

/// <summary>
/// Redis L2 connection settings, nested inside <see cref="CacheOptions"/>.
/// Bound from <c>Cache:Redis</c>.
/// </summary>
public sealed class RedisSettings
{
    /// <summary>
    /// Set to <c>true</c> to activate Redis as HybridCache L2 distributed backend.
    /// Default: <c>false</c> — application starts in L1-only (in-memory) mode.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// StackExchange.Redis connection string.
    /// Supports full configuration token syntax:
    /// <c>host:port,password=…,ssl=true,abortConnect=false,connectTimeout=5000</c>
    /// Required when <see cref="Enabled"/> is <c>true</c>.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Key prefix applied to every cache entry to prevent collisions when multiple
    /// apps share the same Redis instance.  Default: <c>FactoryERP:</c>
    /// </summary>
    public string InstanceName { get; set; } = "FactoryERP:";

    /// <summary>
    /// Connection timeout in milliseconds appended to the connection string when
    /// not already present.  Default: 5000.
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5_000;

    /// <summary>
    /// Synchronous operation timeout in milliseconds.  Default: 5000.
    /// </summary>
    public int SyncTimeoutMs { get; set; } = 5_000;

    /// <summary>
    /// When <c>false</c> (default), a connection failure is logged as a warning
    /// and the app continues in L1-only mode (fail-open, container-friendly).
    /// When <c>true</c>, a connection failure throws and stops the host (fail-fast
    /// suitable for production environments where Redis is mandatory).
    /// </summary>
    public bool AbortOnConnectFail { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Validates <see cref="CacheOptions"/> at startup via the Options pipeline.</summary>
public sealed class CacheOptionsValidator : IValidateOptions<CacheOptions>
{
    public ValidateOptionsResult Validate(string? name, CacheOptions options)
    {
        var errors = new List<string>(4);

        if (options.DefaultExpirationMinutes <= 0)
            errors.Add("Cache:DefaultExpirationMinutes must be > 0.");

        if (options.LocalCacheExpirationMinutes <= 0)
            errors.Add("Cache:LocalCacheExpirationMinutes must be > 0.");

        if (options.LocalCacheExpirationMinutes > options.DefaultExpirationMinutes)
            errors.Add("Cache:LocalCacheExpirationMinutes must be ≤ DefaultExpirationMinutes.");

        if (options.Redis.Enabled)
        {
            if (string.IsNullOrWhiteSpace(options.Redis.ConnectionString))
                errors.Add(
                    "Cache:Redis:ConnectionString is required when Cache:Redis:Enabled=true. " +
                    "Example: \"localhost:6379,password=secret,abortConnect=false\"");

            if (options.Redis.ConnectTimeoutMs <= 0)
                errors.Add("Cache:Redis:ConnectTimeoutMs must be > 0.");

            if (options.Redis.SyncTimeoutMs <= 0)
                errors.Add("Cache:Redis:SyncTimeoutMs must be > 0.");

            if (string.IsNullOrWhiteSpace(options.Redis.InstanceName))
                errors.Add("Cache:Redis:InstanceName must not be empty.");
        }

        return errors.Count > 0
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}
