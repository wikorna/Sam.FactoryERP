using Microsoft.EntityFrameworkCore;
using Printing.Domain;

namespace Printing.Application.Abstractions;

/// <summary>
/// Application-layer abstraction over the Printing persistence store.
/// Infrastructure provides the concrete <c>PrintingDbContext</c>.
/// </summary>
public interface IPrintingDbContext
{
    /// <summary>Print requests — one per approved shipment batch.</summary>
    DbSet<PrintRequest> PrintRequests { get; }

    /// <summary>Per-item print tasks within a request.</summary>
    DbSet<PrintRequestItem> PrintRequestItems { get; }

    /// <summary>Individual print dispatches to a physical printer.</summary>
    DbSet<PrintJob> PrintJobs { get; }

    /// <summary>Immutable outcome records — one per dispatch attempt.</summary>
    DbSet<PrintResult> PrintResults { get; }

    /// <summary>Persists all pending changes.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
