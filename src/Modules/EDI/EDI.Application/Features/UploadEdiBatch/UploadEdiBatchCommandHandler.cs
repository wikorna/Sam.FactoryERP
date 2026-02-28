using System.Security.Cryptography;
using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Domain.Aggregates.EdiFileJobAggregate;
using FactoryERP.Abstractions.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.UploadEdiBatch;

public sealed partial class UploadEdiBatchCommandHandler(
    IEdiFileStore fileStore,
    IEdiFileJobRepository jobs,
    IFileTypeDetector detector,
    IOutboxPublisher outbox,
    ICacheService cache,
    ILogger<UploadEdiBatchCommandHandler> logger)
    : IRequestHandler<UploadEdiBatchCommand, UploadEdiBatchResponse>
{
    public async Task<UploadEdiBatchResponse> Handle(
        UploadEdiBatchCommand request,
        CancellationToken cancellationToken)
    {
        var results = new List<UploadFileResultDto>(request.Files.Count);

        foreach (var file in request.Files)
        {
            var result = await ProcessSingleFileAsync(
                request.PartnerCode, file, cancellationToken);
            results.Add(result);
        }

        // Invalidate job list caches so dashboards reflect the new jobs
        await cache.InvalidateTagAsync(EdiCacheKeys.TagJobs, cancellationToken);

        return new UploadEdiBatchResponse(results);
    }

    private async Task<UploadFileResultDto> ProcessSingleFileAsync(
        string partnerCode,
        UploadFileItem file,
        CancellationToken ct)
    {
        string? fileTypeCode = null;
        string? displayName = null;

        try
        {
            // Auto-detect file type
            var config = await detector.DetectAsync(file.FileName, ct);
            fileTypeCode = config?.FileTypeCode;
            displayName = config?.DisplayName;

            if (config is not null && file.SizeBytes > config.MaxFileSizeBytes)
            {
                LogFileSizeExceeded(logger, file.FileName, file.SizeBytes, config.MaxFileSizeBytes);
                return new UploadFileResultDto(
                    Guid.Empty, file.FileName, fileTypeCode, displayName, "Rejected",
                    $"File size {file.SizeBytes} exceeds maximum {config.MaxFileSizeBytes} bytes");
            }

            // Save to temp, then move to processing
            var tempPath = Path.GetTempFileName();
            await using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await file.Content.CopyToAsync(fs, ct);
            }

            var fileRef = new EdiFileRef(partnerCode, file.FileName, tempPath);
            fileRef = await fileStore.MoveToProcessingAsync(fileRef, ct);

            // Compute SHA-256
            string sha256 = await ComputeSha256Async(fileRef, ct);

            // Deduplication
            bool exists = await jobs.ExistsByChecksumAsync(partnerCode, sha256, ct);
            if (exists)
            {
                await fileStore.MoveToArchiveAsync(fileRef, ct);
                LogDuplicateFile(logger, file.FileName, sha256);
                return new UploadFileResultDto(
                    Guid.Empty, file.FileName, fileTypeCode, displayName, "Duplicate");
            }

            // Resolve partner profile for format/schema
            var partner = await jobs.GetPartnerProfileAsync(partnerCode, ct);

            Guid jobId = Guid.NewGuid();
            var job = EdiFileJob.CreateReceived(
                jobId,
                partner.PartnerCode,
                fileRef.FileName,
                fileRef.FullPath,
                file.SizeBytes,
                sha256,
                partner.Format,
                partner.SchemaVersion,
                fileTypeCode);

            await jobs.AddAsync(job, ct);

            foreach (var ev in job.DomainEvents)
            {
                await outbox.EnqueueAsync(ev, ct);
            }

            job.ClearDomainEvents();

            LogFileUploaded(logger, file.FileName, jobId, fileTypeCode ?? "Unknown");

            return new UploadFileResultDto(
                jobId, file.FileName, fileTypeCode, displayName, "Received");
        }
        catch (Exception ex)
        {
            LogFileUploadError(logger, file.FileName, ex);
            return new UploadFileResultDto(
                Guid.Empty, file.FileName, fileTypeCode, displayName, "Error", ex.Message);
        }
    }

    private async Task<string> ComputeSha256Async(EdiFileRef file, CancellationToken ct)
    {
        await using Stream stream = await fileStore.OpenReadAsync(file, ct);
        byte[] hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "EDI file uploaded: {FileName}, JobId={JobId}, FileType={FileType}")]
    private static partial void LogFileUploaded(ILogger logger, string fileName, Guid jobId, string fileType);

    [LoggerMessage(Level = LogLevel.Warning, Message = "EDI duplicate file skipped: {FileName}, SHA256={Sha256}")]
    private static partial void LogDuplicateFile(ILogger logger, string fileName, string sha256);

    [LoggerMessage(Level = LogLevel.Warning, Message = "EDI file size exceeded: {FileName}, Size={Size}, Max={MaxSize}")]
    private static partial void LogFileSizeExceeded(ILogger logger, string fileName, long size, long maxSize);

    [LoggerMessage(Level = LogLevel.Error, Message = "EDI file upload error: {FileName}")]
    private static partial void LogFileUploadError(ILogger logger, string fileName, Exception ex);
}

