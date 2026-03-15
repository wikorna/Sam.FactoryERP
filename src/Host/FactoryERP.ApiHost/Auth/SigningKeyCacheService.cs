using Auth.Application.Interfaces;
using Microsoft.IdentityModel.Tokens;

namespace FactoryERP.ApiHost.Auth;

/// <summary>
/// Periodically refreshes RSA validation keys from <see cref="IKeyStoreService"/>
/// and exposes them as a synchronous, O(1) in-memory read for <c>IssuerSigningKeyResolver</c>.
/// <para>
/// This eliminates the need for <c>GetAwaiter().GetResult()</c> inside the JWT Bearer
/// token-validation pipeline, which would block a thread-pool thread on every request.
/// </para>
/// </summary>
internal sealed class SigningKeyCacheService : BackgroundService
{
    /// <summary>How often to re-read keys from the key store.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(5);

    private readonly IKeyStoreService _keyStore;
    private readonly ILogger<SigningKeyCacheService> _logger;
    private readonly TaskCompletionSource _firstLoadDone = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private volatile IReadOnlyList<SecurityKey> _keys = Array.Empty<SecurityKey>();

    public SigningKeyCacheService(IKeyStoreService keyStore, ILogger<SigningKeyCacheService> logger)
    {
        _keyStore = keyStore;
        _logger   = logger;
    }

    /// <summary>
    /// Returns the most recently loaded validation keys.
    /// Designed to be called from the synchronous <c>IssuerSigningKeyResolver</c> delegate
    /// with zero I/O and zero blocking.
    /// </summary>
    public IReadOnlyList<SecurityKey> ValidationKeys => _keys;

    /// <summary>
    /// Pre-seeds the cache before the hosted service starts.
    /// Called once during startup (before <c>app.Run()</c>).
    /// </summary>
    public void SeedKeys(IReadOnlyList<(RsaSecurityKey Key, string Kid)> pairs)
    {
        var keys = pairs.Select(p => (SecurityKey)p.Key).ToList().AsReadOnly();
        _keys = keys;
        _firstLoadDone.TrySetResult();
        LogKeysRefreshed(_logger, keys.Count);
    }

    /// <summary>
    /// Blocks until the first key refresh has completed.
    /// Used during startup to ensure keys are loaded before the host starts accepting requests.
    /// </summary>
    public Task WaitForFirstLoadAsync(CancellationToken ct = default)
        => _firstLoadDone.Task.WaitAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial load — retry up to 5 times before giving up.
        const int maxRetries = 5;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await RefreshKeysAsync(stoppingToken);
                _firstLoadDone.TrySetResult();
                break;
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogKeyRefreshFailed(_logger, attempt, maxRetries, ex);
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), stoppingToken);
            }
            catch (Exception ex)
            {
                _firstLoadDone.TrySetException(ex);
                throw;
            }
        }

        // Periodic refresh
        using var timer = new PeriodicTimer(RefreshInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RefreshKeysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Stale keys remain valid; log and retry next cycle.
                LogKeyRefreshFailed(_logger, 0, 0, ex);
            }
        }
    }

    private async Task RefreshKeysAsync(CancellationToken ct)
    {
        var pairs = await _keyStore.GetValidationKeysAsync(ct);
        var keys  = pairs.Select(p => (SecurityKey)p.Key).ToList().AsReadOnly();

        _keys = keys;

        LogKeysRefreshed(_logger, keys.Count);
    }

    // ── Structured logging ──────────────────────────────────────────────
    private static void LogKeysRefreshed(ILogger logger, int count) => logger.LogInformation("JWT signing-key cache refreshed ({Count} key(s) loaded)", count);

    private static void LogKeyRefreshFailed(ILogger logger, int attempt, int maxAttempts, Exception ex) => logger.LogWarning(ex, "JWT signing-key refresh failed (attempt {Attempt}/{MaxAttempts})", attempt, maxAttempts);
}

