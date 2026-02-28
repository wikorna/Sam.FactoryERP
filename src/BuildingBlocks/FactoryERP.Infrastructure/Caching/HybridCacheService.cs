using System.Text.Json;
using System.Text.RegularExpressions;
using FactoryERP.Abstractions.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactoryERP.Infrastructure.Caching;

/// <summary>
/// Production-grade <see cref="ICacheService"/> backed by <see cref="HybridCache"/> (L1 in-memory + L2 Redis).
/// <list type="bullet">
///   <item>Exception-safe: cache infrastructure failures fall back to the factory — business logic is never broken.</item>
///   <item>Stampede-proof: <see cref="HybridCache"/> GetOrCreateAsync is single-flight per key.</item>
///   <item>Thread-safe: <see cref="HybridCache"/> is designed for concurrent access.</item>
/// </list>
/// </summary>
internal sealed class HybridCacheService : ICacheService
{
    private static readonly Regex AllowedKeyChars = new(@"^[A-Za-z0-9:._\-\/]+$", RegexOptions.Compiled);

    private readonly HybridCache _cache;
    private readonly ILogger<HybridCacheService> _logger;

    private readonly string _instanceName;
    private readonly int _maximumKeyLength;
    private readonly int _maximumPayloadBytes;

    private readonly TimeSpan _defaultExpiration;
    private readonly TimeSpan _defaultLocalExpiration;

    // NOTE: Keep deterministic JSON settings for size checks. (This does NOT control HybridCache serialization.)
    private readonly JsonSerializerOptions _sizeCheckJsonOptions;

    public HybridCacheService(
        HybridCache cache,
        IOptions<CacheOptions> options,
        ILogger<HybridCacheService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var opt = options?.Value ?? throw new ArgumentNullException(nameof(options));

        _instanceName = string.IsNullOrWhiteSpace(opt.Redis.InstanceName) ? "FactoryERP:" : opt.Redis.InstanceName.Trim();
        _maximumKeyLength = opt.MaximumKeyLength > 0 ? opt.MaximumKeyLength : 1024;
        _maximumPayloadBytes = opt.MaximumPayloadBytes > 0 ? opt.MaximumPayloadBytes : 1_048_576;

        _defaultExpiration = NormalizeExpirationMinutes(opt.DefaultExpirationMinutes, fallbackMinutes: 5);
        _defaultLocalExpiration = NormalizeExpirationMinutes(opt.LocalCacheExpirationMinutes, fallbackMinutes: 1);

        _sizeCheckJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            // If you cache polymorphic DTOs, you may want to configure type info resolver explicitly (source-gen).
            // TypeInfoResolver = ...
        };
    }


    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, ValueTask<T>> factory,
        CacheEntrySettings? settings = null,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(factory);

        var key = BuildKey(cacheKey);
        var normalized = settings?.Normalize();

        try
        {
            var entryOptions = BuildEntryOptions(normalized);

            return await _cache.GetOrCreateAsync(
                    key,
                    async ct =>
                    {
                        // NOTE:
                        // HybridCache will cache whatever the factory returns.
                        // On GetOrCreate path, we cannot truly "skip caching" per-result.
                        var value = await factory(ct).ConfigureAwait(false);

                        if (!IsPayloadWithinLimit(value))
                            Log.PayloadTooLarge(_logger, key, _maximumPayloadBytes);

                        return value;
                    },
                    entryOptions,
                    normalized?.Tags,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // IMPORTANT:
            // This fallback can execute factory again in rare cases (e.g., cache fails after the value was computed).
            // If your factory has side effects, redesign to "TryGet + compute + best-effort Set" with stampede protection.
            Log.GetOrCreateFailed(_logger, key, ex);

            return await factory(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task SetAsync<T>(
        string cacheKey,
        T value,
        CacheEntrySettings? settings = null,
        CancellationToken cancellationToken = default)
        where T : notnull
    {
        var key = BuildKey(cacheKey);
        var normalized = settings?.Normalize();

        // Strict enforcement on Set: if too large, do not cache.
        if (!IsPayloadWithinLimit(value))
        {
            Log.PayloadTooLarge(_logger, key, _maximumPayloadBytes);
            return;
        }

        try
        {
            var entryOptions = BuildEntryOptions(normalized);

            await _cache.SetAsync(key, value, entryOptions, normalized?.Tags, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.SetFailed(_logger, key, ex);
        }
    }

    public async Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        var key = BuildKey(cacheKey);

        try
        {
            await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.RemoveFailed(_logger, key, ex);
        }
    }

    public async Task InvalidateTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Cache tag must not be null/empty.", nameof(tag));

        var trimmed = tag.Trim();

        try
        {
            await _cache.RemoveByTagAsync(trimmed, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Log.InvalidateTagFailed(_logger, trimmed, ex);
        }
    }

    private HybridCacheEntryOptions BuildEntryOptions(CacheEntrySettings? settings)
    {
        var exp = NormalizeExpiration(settings?.AbsoluteExpiration, _defaultExpiration);
        var localExp = NormalizeExpiration(settings?.L1Expiration, _defaultLocalExpiration);

        return new HybridCacheEntryOptions
        {
            Expiration = exp,
            LocalCacheExpiration = localExp
        };
    }

    private string BuildKey(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
            throw new ArgumentException("Cache key must not be null/empty.", nameof(cacheKey));

        var trimmed = cacheKey.Trim();

        for (var i = 0; i < trimmed.Length; i++)
        {
            if (char.IsControl(trimmed[i]))
                throw new ArgumentException("Cache key contains control characters.", nameof(cacheKey));
        }

        // Optional but strongly recommended: keep cache keys canonical and safe across modules/tenants.
        // Allowed: A-Z a-z 0-9 : . _ - /
        if (!AllowedKeyChars.IsMatch(trimmed))
            throw new ArgumentException("Cache key contains invalid characters. Allowed: [A-Za-z0-9:._-/].", nameof(cacheKey));

        var key = string.Concat(_instanceName, trimmed);

        if (key.Length > _maximumKeyLength)
            throw new ArgumentException($"Cache key length exceeds maximum of {_maximumKeyLength}.", nameof(cacheKey));

        return key;
    }

    private static TimeSpan NormalizeExpiration(TimeSpan? value, TimeSpan fallback)
        => value is { } ts && ts > TimeSpan.Zero ? ts : fallback;

    private static TimeSpan NormalizeExpirationMinutes(int minutes, int fallbackMinutes)
        => TimeSpan.FromMinutes(minutes > 0 ? minutes : fallbackMinutes);

    private bool IsPayloadWithinLimit<T>(T value)
    {
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, _sizeCheckJsonOptions);
            return bytes.Length <= _maximumPayloadBytes;
        }
        catch (Exception ex)
        {
            // Fail-open: if size check fails, do not block caching.
            Log.PayloadSizeCheckFailed(_logger, ex);
            return true;
        }
    }

    private static class Log
    {
        private static readonly Action<ILogger, string, Exception> _getOrCreateFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(1010, nameof(GetOrCreateFailed)),
                "Cache GetOrCreateAsync failed for key '{CacheKey}'. Executing factory fallback.");

        private static readonly Action<ILogger, string, Exception> _setFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1020, nameof(SetFailed)),
                "Cache SetAsync failed for key '{CacheKey}'.");

        private static readonly Action<ILogger, string, Exception> _removeFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1030, nameof(RemoveFailed)),
                "Cache RemoveAsync failed for key '{CacheKey}'.");

        private static readonly Action<ILogger, string, Exception> _invalidateTagFailed =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(1040, nameof(InvalidateTagFailed)),
                "Cache InvalidateTagAsync failed for tag '{Tag}'.");

        private static readonly Action<ILogger, string, int, Exception?> _payloadTooLarge =
            LoggerMessage.Define<string, int>(
                LogLevel.Warning,
                new EventId(1050, nameof(PayloadTooLarge)),
                // IMPORTANT: Do not claim "skipping cache" on GetOrCreate path.
                "Cache payload too large for key '{CacheKey}'. MaxPayloadBytes={MaxPayloadBytes}.");

        private static readonly Action<ILogger, Exception> _payloadSizeCheckFailed =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(1060, nameof(PayloadSizeCheckFailed)),
                "Cache payload size check failed; continuing without enforcement.");

        public static void GetOrCreateFailed(ILogger logger, string cacheKey, Exception ex) => _getOrCreateFailed(logger, cacheKey, ex);
        public static void SetFailed(ILogger logger, string cacheKey, Exception ex) => _setFailed(logger, cacheKey, ex);
        public static void RemoveFailed(ILogger logger, string cacheKey, Exception ex) => _removeFailed(logger, cacheKey, ex);
        public static void InvalidateTagFailed(ILogger logger, string tag, Exception ex) => _invalidateTagFailed(logger, tag, ex);

        public static void PayloadTooLarge(ILogger logger, string cacheKey, int maxBytes) => _payloadTooLarge(logger, cacheKey, maxBytes, null);
        public static void PayloadSizeCheckFailed(ILogger logger, Exception ex) => _payloadSizeCheckFailed(logger, ex);
    }
}


