namespace FactoryERP.Infrastructure.Caching;

using System.Diagnostics;
using FactoryERP.Abstractions.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

/// <summary>
/// DI extension that wires HybridCache (L1 always-on) with optional Redis L2.
/// <para>
/// Hosts call exactly one method:
/// <code>builder.Services.AddFactoryErpCaching(builder.Configuration);</code>
/// </para>
/// <para>
/// Redis is <b>disabled by default</b>.  To enable, set in <c>appsettings.json</c>:
/// <code>
/// "Cache": {
///   "Redis": {
///     "Enabled": true,
///     "ConnectionString": "localhost:6379,password=secret,abortConnect=false"
///   }
/// }
/// </code>
/// Or via environment variable:
/// <code>Cache__Redis__Enabled=true</code>
/// <code>Cache__Redis__ConnectionString=localhost:6379,password=secret,abortConnect=false</code>
/// </para>
/// </summary>
public static partial class ServiceCollectionExtensionsCaching
{
    /// <summary>
    /// Registers HybridCache, ICacheService, and (optionally) Redis L2.
    /// Validates <see cref="CacheOptions"/> at startup — misconfiguration stops the host.
    /// </summary>
    public static IServiceCollection AddFactoryErpCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── 1. Bind and validate CacheOptions ────────────────────────────────
        var section = configuration.GetSection(CacheOptions.SectionName);

        services.Configure<CacheOptions>(section);
        services.AddSingleton<IValidateOptions<CacheOptions>, CacheOptionsValidator>();
        services.AddOptions<CacheOptions>()
                .Bind(section)
                .ValidateOnStart(); // fail fast before the first request

        // Read eagerly for registration-time decisions (no BuildServiceProvider)
        var opts = section.Get<CacheOptions>() ?? new CacheOptions();

        // ── 2. L1 — always-on in-memory (HybridCache) ────────────────────────
        services.AddMemoryCache();
        services.AddHybridCache(hybrid =>
        {
            hybrid.MaximumPayloadBytes = opts.MaximumPayloadBytes;
            hybrid.MaximumKeyLength    = opts.MaximumKeyLength;
            hybrid.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration           = TimeSpan.FromMinutes(opts.DefaultExpirationMinutes),
                LocalCacheExpiration = TimeSpan.FromMinutes(opts.LocalCacheExpirationMinutes),
            };
        });

        // ── 3. L2 — Redis (conditional on Cache:Redis:Enabled) ───────────────
        if (opts.Redis.Enabled)
            RegisterRedisL2(services, opts.Redis);
        else
            services.AddHostedService<CacheL1OnlyLogService>(); // deferred log

        // ── 4. ICacheService ─────────────────────────────────────────────────
        services.AddScoped<ICacheService, HybridCacheService>();

        // ── 5. Health check ──────────────────────────────────────────────────
        // Always registered; RedisHealthCheck reports Healthy when Redis is disabled.
        services.AddHealthChecks()
                .AddCheck<RedisHealthCheck>("redis", tags: ["ready", "cache"]);

        return services;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Redis L2 wiring
    // ─────────────────────────────────────────────────────────────────────────

    private static void RegisterRedisL2(IServiceCollection services, RedisSettings redis)
    {
        // Build ConfigurationOptions from the connection string.
        // All Redis settings (timeout, ssl, password, abortConnect) can be embedded
        // in the connection string token syntax — StackExchange.Redis parses them.
        var configOpts = BuildConfigurationOptions(redis);

        // IConnectionMultiplexer — singleton.
        // GetAwaiter().GetResult() is intentional and safe here:
        //   • This lambda runs inside the DI singleton factory, not on ASP.NET
        //     request thread, so there is no SynchronizationContext to deadlock.
        //   • The call is made exactly once per application lifetime.
        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>()
                           .CreateLogger(LogCategory);

            LogRedisAttempt(logger, redis.InstanceName);

            var sw = Stopwatch.StartNew();
            try
            {
                var mux = ConnectionMultiplexer.ConnectAsync(configOpts)
                                               .GetAwaiter().GetResult();
                sw.Stop();
                LogRedisConnected(logger, redis.InstanceName, sw.ElapsedMilliseconds);
                return mux;
            }
            catch (Exception ex)
            {
                sw.Stop();
                if (redis.AbortOnConnectFail)
                {
                    LogRedisFailFast(logger, redis.InstanceName, ex);
                    throw new InvalidOperationException(
                        $"Redis connection failed (Cache:Redis:AbortOnConnectFail=true). " +
                        $"InstanceName={redis.InstanceName}. See inner exception.", ex);
                }

                // Fail-open: start in L1-only mode, Redis will reconnect on next use.
                LogRedisFailOpen(logger, redis.InstanceName, ex);
                configOpts.AbortOnConnectFail = false; // ensure reconnect attempts
                return ConnectionMultiplexer.ConnectAsync(configOpts).GetAwaiter().GetResult();
            }
        });

        // IDistributedCache backed by Redis — HybridCache auto-detects this and
        // uses it as the L2 distributed store.
        services.AddStackExchangeRedisCache(o =>
        {
            o.InstanceName        = redis.InstanceName;
            o.ConfigurationOptions = configOpts;
        });
    }

    private static ConfigurationOptions BuildConfigurationOptions(RedisSettings redis)
    {
        // Parse the user-supplied connection string; then apply override values for
        // settings that have dedicated properties.
        var cfg = ConfigurationOptions.Parse(redis.ConnectionString, ignoreUnknown: true);

        // Apply overrides only when the connection string does not already set them
        // (avoids silently overwriting explicit user settings).
        cfg.AbortOnConnectFail = redis.AbortOnConnectFail;

        if (cfg.ConnectTimeout == 5_000) // StackExchange.Redis default
            cfg.ConnectTimeout = redis.ConnectTimeoutMs;

        if (cfg.SyncTimeout == 5_000)
            cfg.SyncTimeout = redis.SyncTimeoutMs;

        return cfg;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Structured log messages — no secrets, no connection strings
    // ─────────────────────────────────────────────────────────────────────────

    private const string LogCategory = "FactoryERP.Infrastructure.Caching";

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cache mode: HybridCache L1+L2 (Redis). Connecting — instance={InstanceName}")]
    private static partial void LogRedisAttempt(ILogger logger, string instanceName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cache mode: HybridCache L1+L2 (Redis). Connected in {ElapsedMs}ms — instance={InstanceName}")]
    private static partial void LogRedisConnected(ILogger logger, string instanceName, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Critical,
        Message = "Cache mode: Redis connection FAILED (AbortOnConnectFail=true) — instance={InstanceName}. Host will not start.")]
    private static partial void LogRedisFailFast(ILogger logger, string instanceName, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Cache mode: Redis connection failed — instance={InstanceName}. " +
                  "Continuing in L1-only mode (fail-open). Redis will reconnect automatically.")]
    private static partial void LogRedisFailOpen(ILogger logger, string instanceName, Exception ex);
}

// ─────────────────────────────────────────────────────────────────────────────
// Hosted service — deferred startup log for L1-only mode
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Emits the "L1-only" mode log line after the full logger pipeline (Serilog etc.)
/// is active.  Only registered when <c>Cache:Redis:Enabled=false</c>.
/// </summary>
internal sealed partial class CacheL1OnlyLogService(ILoggerFactory loggerFactory) : BackgroundService
{
    [LoggerMessage(Level = LogLevel.Information,
        Message = "Cache mode: HybridCache L1-only (in-memory). " +
                  "Set Cache:Redis:Enabled=true and Cache:Redis:ConnectionString to enable Redis L2.")]
    private static partial void LogL1Only(ILogger logger);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var logger = loggerFactory.CreateLogger("FactoryERP.Infrastructure.Caching");
        LogL1Only(logger);
        return Task.CompletedTask;
    }
}

