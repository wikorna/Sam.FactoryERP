using EDI.Application.Abstractions;
using EDI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EDI.Infrastructure.FileStores;

public sealed class FileSystemEdiFileStore : IEdiFileStore
{
    private readonly EdiDbContext _dbContext;

    public FileSystemEdiFileStore(EdiDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Stream> OpenReadAsync(EdiFileRef file, CancellationToken ct)
    {
        if (!File.Exists(file.FullPath))
        {
            throw new FileNotFoundException($"File not found: {file.FullPath}");
        }

        Stream stream = File.Open(file.FullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task<long> GetSizeAsync(EdiFileRef file, CancellationToken ct)
    {
        var info = new FileInfo(file.FullPath);
        if (!info.Exists)
        {
            throw new FileNotFoundException($"File not found: {file.FullPath}");
        }
        return Task.FromResult(info.Length);
    }

    public async Task<EdiFileRef> MoveToProcessingAsync(EdiFileRef file, CancellationToken ct)
    {
        var partner = await _dbContext.PartnerProfiles
            .FirstOrDefaultAsync(p => p.PartnerCode == file.PartnerCode, ct);

        if (partner == null)
        {
            throw new InvalidOperationException($"Partner not found: {file.PartnerCode}");
        }

        string targetDir = partner.ProcessingPath;
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            throw new InvalidOperationException($"ProcessingPath not configured for partner: {file.PartnerCode}");
        }

        return MoveFile(file, targetDir);
    }

    public async Task<EdiFileRef> MoveToArchiveAsync(EdiFileRef file, CancellationToken ct)
    {
        var partner = await _dbContext.PartnerProfiles
             .FirstOrDefaultAsync(p => p.PartnerCode == file.PartnerCode, ct);

        if (partner == null)
        {
            throw new InvalidOperationException($"Partner not found: {file.PartnerCode}");
        }

        string targetDir = partner.ArchivePath;
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            // Fallback or throw? Let's throw for now to ensure config.
            throw new InvalidOperationException($"ArchivePath not configured for partner: {file.PartnerCode}");
        }

        return MoveFile(file, targetDir);
    }

    public async Task<EdiFileRef> MoveToErrorAsync(EdiFileRef file, string reason, CancellationToken ct)
    {
        var partner = await _dbContext.PartnerProfiles
             .FirstOrDefaultAsync(p => p.PartnerCode == file.PartnerCode, ct);

        string targetDir = partner?.ErrorPath ?? "EdiErrors"; // Fallback if partner unknown

        // Write reason file
        try
        {
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
            string reasonFile = Path.Combine(targetDir, file.FileName + ".error.txt");
            await File.WriteAllTextAsync(reasonFile, reason, ct);
        }
        catch { /* ignore error writing reason */ }

        return MoveFile(file, targetDir);
    }

    private static EdiFileRef MoveFile(EdiFileRef file, string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        string targetPath = Path.Combine(targetDir, file.FileName);

        // Handle overwrite or unique naming? 
        // For now, overwrite or throw. File.Move throws if exists.
        // Let's ensure uniqueness to avoid data loss.
        if (File.Exists(targetPath))
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
            string name = Path.GetFileNameWithoutExtension(file.FileName);
            string ext = Path.GetExtension(file.FileName);
            string newName = $"{name}_{timestamp}{ext}";
            targetPath = Path.Combine(targetDir, newName);
        }

        // If source not found, maybe it was already moved? 
        if (!File.Exists(file.FullPath))
        {
            throw new FileNotFoundException($"Source file not found: {file.FullPath}");
        }

        File.Move(file.FullPath, targetPath);

        return file with { FullPath = targetPath, FileName = Path.GetFileName(targetPath) };
    }
}
