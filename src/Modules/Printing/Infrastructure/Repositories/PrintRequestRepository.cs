using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;
using Printing.Infrastructure.Persistence;

namespace Printing.Infrastructure.Repositories;

public class PrintRequestRepository : IPrintRequestRepository
{
    private readonly PrintingDbContext _context;

    public PrintRequestRepository(PrintingDbContext context)
    {
        _context = context;
    }

    public Task<PrintRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.PrintRequests.FirstOrDefaultAsync(pr => pr.Id == id, cancellationToken);
    }

    public async Task AddAsync(PrintRequest printRequest, CancellationToken cancellationToken = default)
    {
        await _context.PrintRequests.AddAsync(printRequest, cancellationToken);
    }
}

