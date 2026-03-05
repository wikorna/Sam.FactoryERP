namespace EDI.Application.Abstractions;

public record StoredObject(string StorageKey, string ProviderName, string Sha256);

public interface IEdiStorageService
{
    Task<StoredObject> SaveAsync(Stream content, string originalFileName, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task DeleteAsync(string storageKey, CancellationToken ct);
}
