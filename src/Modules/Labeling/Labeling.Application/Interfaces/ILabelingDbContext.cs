using Labeling.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Labeling.Application.Interfaces;

public interface ILabelingDbContext
{
    DbSet<PrintJob> PrintJobs { get; }
    DbSet<Printer> Printers { get; }
    DbSet<PrinterImage> PrinterImages { get; }
    DbSet<DepartmentPrinter> DepartmentPrinters { get; }
    DbSet<StorePrinter> StorePrinters { get; }
    DbSet<UserPrinterOverride> UserPrinterOverrides { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
