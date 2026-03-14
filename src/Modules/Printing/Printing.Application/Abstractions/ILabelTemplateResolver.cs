using Printing.Application.Models;

namespace Printing.Application.Abstractions;

/// <summary>
/// Resolves a <see cref="LabelTemplateSpec"/> from a template ID stored in the database.
/// </summary>
public interface ILabelTemplateResolver
{
    /// <summary>
    /// Returns the <see cref="LabelTemplateSpec"/> for the given template ID.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no active template exists for the given ID.
    /// </exception>
    Task<LabelTemplateSpec> ResolveAsync(Guid templateId, CancellationToken ct = default);
}

