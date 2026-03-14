using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;
using Printing.Infrastructure.Persistence;

namespace Printing.Infrastructure.Repositories;

public class PrinterRepository : IPrinterRepository
{
    private readonly PrintingDbContext _context;

    public PrinterRepository(PrintingDbContext context)
    {
        _context = context;
    }

    public Task<Printer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.Printers.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task<Printer?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _context.Printers.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
    }
}

