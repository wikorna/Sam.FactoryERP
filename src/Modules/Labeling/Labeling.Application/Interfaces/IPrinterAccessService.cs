namespace Labeling.Application.Interfaces;

public interface IPrinterAccessService
{
    Task<bool> CanAccessPrinterAsync(Guid printerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Guid>> GetAuthorizedPrinterIdsAsync(CancellationToken cancellationToken = default);
}

