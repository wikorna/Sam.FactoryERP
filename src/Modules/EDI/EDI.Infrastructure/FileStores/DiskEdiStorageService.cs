using System.Globalization;
using System.Security.Cryptography;
using EDI.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace EDI.Infrastructure.FileStores;

public class DiskEdiStorageService : IEdiStorageService
{
    private readonly string _rootFolder;

    public DiskEdiStorageService(IConfiguration configuration)
    {
        _rootFolder = configuration["EDI:Storage:DiskRoot"] ?? "/var/lib/factoryerp/edi";
    }

    public async Task<StoredObject> SaveAsync(Stream content, string originalFileName, CancellationToken ct)
    {
        var stagingId = Guid.NewGuid().ToString("N");
        var safeFileName = Path.GetFileName(originalFileName);
        
        var year = DateTime.UtcNow.ToString("yyyy", CultureInfo.InvariantCulture);
        var month = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
        var relativePath = Path.Combine("edi", "staging", year, month, stagingId, safeFileName);
        
        var fullPath = Path.Combine(_rootFolder, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var sha256 = SHA256.Create();
        await using var fileStream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
        await using var cryptoStream = new CryptoStream(fileStream, sha256, CryptoStreamMode.Write);
        
        await content.CopyToAsync(cryptoStream, ct);
        
        // Ensure final block is flushed before getting the hash
        if (!cryptoStream.HasFlushedFinalBlock)
        {
            await cryptoStream.FlushFinalBlockAsync(ct);
        }

        var hashBytes = sha256.Hash ?? throw new InvalidOperationException("Hash could not be computed.");
        var hashString = Convert.ToHexString(hashBytes).ToLowerInvariant();

        // Convert path separators to forward slashes for storage key consistency
        var storageKey = relativePath.Replace(Path.DirectorySeparatorChar, '/');

        return new StoredObject(storageKey, "Disk", hashString);
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = Path.Combine(_rootFolder, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Storage file not found: {fullPath}");
        }

        Stream fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult(fileStream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = Path.Combine(_rootFolder, storageKey.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        return Task.CompletedTask;
    }
}
