using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;
using Printing.Infrastructure.Persistence;

namespace Printing.Infrastructure.Repositories;

public class PrintJobRepository : IPrintJobRepository
{
    private readonly PrintingDbContext _context;

    public PrintJobRepository(PrintingDbContext context)
    {
        _context = context;
    }

    public Task<PrintJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.PrintJobs.FirstOrDefaultAsync(pj => pj.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<PrintJob>> GetByStatusAsync(PrintJobStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.PrintJobs.Where(pj => pj.Status == status).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PrintJob printJob, CancellationToken cancellationToken = default)
    {
        await _context.PrintJobs.AddAsync(printJob, cancellationToken);
    }

    public Task UpdateAsync(PrintJob printJob, CancellationToken cancellationToken = default)
    {
        _context.PrintJobs.Update(printJob);
        return Task.CompletedTask;
    }
}

