using Printing.Domain;

namespace Printing.Application.Abstractions;

public interface ILabelTemplateRepository
{
    Task<LabelTemplate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<LabelTemplate?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
}

