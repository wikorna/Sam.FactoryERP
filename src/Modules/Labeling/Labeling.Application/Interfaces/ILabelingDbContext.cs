using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Application.Interfaces;

public interface ILabelingDbContext
{
    DbSet<PrintJob> PrintJobs { get; }
    DbSet<Printer> Printers { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
