using System.Security.Cryptography;
using Auth.Application.Interfaces;
using Auth.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Auth.Infrastructure.Services;

/// <summary>
/// File-based RSA key store. Loads PEM key files from
/// the configured <c>Jwt:KeyDirectory</c>. Supports multiple keys
/// for rotation: the newest active key signs, all non-expired keys validate.
/// <para>
/// Key file naming convention: <c>{kid}.pem</c> (private key).
/// If no keys exist on first run, a new 2048-bit RSA key is generated.
/// </para>
/// </summary>
internal sealed class FileKeyStoreService : IKeyStoreService
{
    private readonly JwtOptions _options;
    private readonly ILogger<FileKeyStoreService> _logger;
    private RsaSecurityKey? _cachedKey;
    private string? _cachedKid;

    public FileKeyStoreService(IOptions<JwtOptions> options, ILogger<FileKeyStoreService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<(RsaSecurityKey Key, string Kid)> GetActiveSigningKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedKey is not null && _cachedKid is not null)
            return Task.FromResult((_cachedKey, _cachedKid));

        var keyDir = Path.GetFullPath(_options.KeyDirectory);
        Directory.CreateDirectory(keyDir);

        var pemFiles = Directory.GetFiles(keyDir, "*.pem");

        if (pemFiles.Length == 0)
        {
            // Auto-generate a key for development
            var kid = $"key-{DateTime.UtcNow:yyyyMMddHHmmss}";
            var rsa = RSA.Create(2048);
            var pemContent = rsa.ExportRSAPrivateKeyPem();
            var filePath = Path.Combine(keyDir, $"{kid}.pem");
            File.WriteAllText(filePath, pemContent);
            LogKeyGenerated(_logger, kid, filePath);

            _cachedKey = new RsaSecurityKey(rsa) { KeyId = kid };
            _cachedKid = kid;
            return Task.FromResult((_cachedKey, _cachedKid));
        }

        // Use the most recently modified key file as the active signing key
        var activeFile = pemFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
        var activeKid = Path.GetFileNameWithoutExtension(activeFile);

        var activeRsa = RSA.Create();
        activeRsa.ImportFromPem(File.ReadAllText(activeFile));

        _cachedKey = new RsaSecurityKey(activeRsa) { KeyId = activeKid };
        _cachedKid = activeKid;

        LogKeyLoaded(_logger, activeKid);

        return Task.FromResult((_cachedKey, _cachedKid));
    }

    public Task<IReadOnlyList<(RsaSecurityKey Key, string Kid)>> GetValidationKeysAsync(CancellationToken cancellationToken = default)
    {
        var keyDir = Path.GetFullPath(_options.KeyDirectory);

        if (!Directory.Exists(keyDir))
            return Task.FromResult<IReadOnlyList<(RsaSecurityKey, string)>>(Array.Empty<(RsaSecurityKey, string)>());

        var pemFiles = Directory.GetFiles(keyDir, "*.pem");
        var keys = new List<(RsaSecurityKey, string)>(pemFiles.Length);

        foreach (var file in pemFiles)
        {
            var kid = Path.GetFileNameWithoutExtension(file);
            var rsa = RSA.Create();
            rsa.ImportFromPem(File.ReadAllText(file));
            keys.Add((new RsaSecurityKey(rsa) { KeyId = kid }, kid));
        }

        return Task.FromResult<IReadOnlyList<(RsaSecurityKey, string)>>(keys);
    }

    private static void LogKeyGenerated(ILogger logger, string kid, string filePath) => logger.LogWarning("No RSA key found. Generated new key Kid={Kid} at {FilePath}", kid, filePath);

    private static void LogKeyLoaded(ILogger logger, string kid) => logger.LogInformation("Loaded active signing key Kid={Kid}", kid);
}
