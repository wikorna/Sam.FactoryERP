namespace Printing.Application.Abstractions;

/// <summary>
/// Selects the correct <see cref="ITemplatePrintStrategy"/> for a given template version string.
/// </summary>
public interface ITemplatePrintStrategySelector
{
    /// <summary>
    /// Returns the strategy that declares support for <paramref name="version"/>,
    /// or the first registered strategy as a safe fallback.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no strategies are registered at all (misconfiguration).
    /// </exception>
    ITemplatePrintStrategy GetStrategy(string version);
}
