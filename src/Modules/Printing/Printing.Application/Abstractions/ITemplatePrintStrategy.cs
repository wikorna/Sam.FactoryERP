using Printing.Application.Models;

namespace Printing.Application.Abstractions;

/// <summary>
/// Versionable strategy that renders a <see cref="PrintDocument"/> from a
/// <see cref="LabelTemplateSpec"/> and its associated label data.
/// </summary>
/// <remarks>
/// <para>
/// One strategy implementation per template version family (e.g. <c>V1ShipmentLabelStrategy</c>
/// handles <c>Version = "v1"</c>).  New versions are added as new strategy classes;
/// <c>TemplatePrintStrategySelector</c> picks the correct one at runtime.
/// </para>
/// <para>
/// Strategies MUST be stateless — they are registered as singletons.
/// </para>
/// </remarks>
public interface ITemplatePrintStrategy
{
    /// <summary>Template version(s) this strategy handles, e.g. "v1".</summary>
    IReadOnlyCollection<string> SupportedVersions { get; }

    /// <summary>
    /// Renders the label data into a <see cref="PrintDocument"/> using the
    /// ZPL body stored in <paramref name="templateSpec"/>.
    /// </summary>
    PrintDocument Render(ShipmentItemLabelData data, QrPayloadData qr, LabelTemplateSpec templateSpec);
}

