using Microsoft.Extensions.Logging;
using Printing.Application.Abstractions;
using Printing.Application.Models;

namespace Printing.Infrastructure.Strategies;

/// <summary>
/// Selects the correct <see cref="ITemplatePrintStrategy"/> based on the
/// <see cref="LabelTemplateSpec.Version"/> of the resolved template.
/// Falls back to the first registered strategy when no exact version match is found.
/// </summary>
public sealed class TemplatePrintStrategySelector(
    IEnumerable<ITemplatePrintStrategy> strategies,
    ILogger<TemplatePrintStrategySelector> logger)
    : ITemplatePrintStrategySelector
{
    private readonly List<ITemplatePrintStrategy> _strategies = strategies.ToList();

    /// <inheritdoc />
    public ITemplatePrintStrategy GetStrategy(string version)
    {
        if (_strategies.Count == 0)
            throw new InvalidOperationException(
                "No ITemplatePrintStrategy implementations are registered. " +
                "Call AddPrintingInfrastructure() in your DI setup.");

        var match = _strategies
            .FirstOrDefault(s => s.SupportedVersions
                .Any(v => string.Equals(v, version, StringComparison.OrdinalIgnoreCase)));

        if (match is not null)
            return match;

        // Fallback — first strategy acts as the default.
        var fallback = _strategies[0];
        LogFallbackStrategy(logger, version, fallback.GetType().Name);
        return fallback;
    }

    private static void LogFallbackStrategy(ILogger logger, string version, string fallbackStrategy) => logger.LogWarning("No strategy found for template version '{Version}'. Falling back to '{FallbackStrategy}'.", version, fallbackStrategy);
}

