using Printing.Application.Abstractions;

namespace Printing.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly PrintingDbContext _context;

    public UnitOfWork(PrintingDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}

