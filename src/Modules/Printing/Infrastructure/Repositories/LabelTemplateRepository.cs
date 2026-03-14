using Microsoft.EntityFrameworkCore;
using Printing.Application.Abstractions;
using Printing.Domain;
using Printing.Infrastructure.Persistence;

namespace Printing.Infrastructure.Repositories;

public class LabelTemplateRepository : ILabelTemplateRepository
{
    private readonly PrintingDbContext _context;

    public LabelTemplateRepository(PrintingDbContext context)
    {
        _context = context;
    }

    public Task<LabelTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.LabelTemplates.FirstOrDefaultAsync(lt => lt.Id == id, cancellationToken);
    }

    public Task<LabelTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return _context.LabelTemplates.FirstOrDefaultAsync(lt => lt.Name == name, cancellationToken);
    }
}

