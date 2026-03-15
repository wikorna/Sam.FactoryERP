using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;
using Printing.Infrastructure.Persistence;

namespace Printing.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IPrintJobRepository"/>.
/// </summary>
public sealed class PrintJobRepository : IPrintJobRepository
{
    private readonly PrintingDbContext _db;

    public PrintJobRepository(PrintingDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.PrintJobs
            .FirstOrDefaultAsync(x => x.Id == id, ct);

    /// <inheritdoc />
    public async Task<bool> ExistsByIdempotencyKeyAsync(string key, CancellationToken ct = default)
        => await _db.PrintJobs.AnyAsync(x => x.IdempotencyKey == key, ct);

    /// <inheritdoc />
    public void Add(PrintJob job) => _db.PrintJobs.Add(job);

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
