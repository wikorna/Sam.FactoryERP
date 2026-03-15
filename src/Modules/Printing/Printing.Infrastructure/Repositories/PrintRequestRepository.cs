using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;
using Printing.Infrastructure.Persistence;

namespace Printing.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPrintRequestRepository"/>.
/// Loads the full aggregate (items) on every read.
/// </summary>
public sealed class PrintRequestRepository : IPrintRequestRepository
{
    private readonly PrintingDbContext _db;

    public PrintRequestRepository(PrintingDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PrintRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PrintRequests
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc />
    public async Task<PrintRequest?> GetByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await _db.PrintRequests
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct);

    /// <inheritdoc />
    public void Add(PrintRequest request) => _db.PrintRequests.Add(request);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
