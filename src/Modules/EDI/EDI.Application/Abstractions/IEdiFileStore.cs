namespace EDI.Application.Abstractions;

public sealed record EdiFileRef(string PartnerCode, string FileName, string FullPath);

public interface IEdiFileStore
{
    Task<Stream> OpenReadAsync(EdiFileRef file, CancellationToken ct);
    Task<long> GetSizeAsync(EdiFileRef file, CancellationToken ct);

    Task<EdiFileRef> MoveToProcessingAsync(EdiFileRef file, CancellationToken ct);
    Task<EdiFileRef> MoveToArchiveAsync(EdiFileRef file, CancellationToken ct);
    Task<EdiFileRef> MoveToErrorAsync(EdiFileRef file, string reason, CancellationToken ct);
}

