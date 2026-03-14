using Microsoft.Extensions.Options;
using Shipping.Application.Abstractions;

namespace Shipping.Infrastructure.Services;

/// <summary>
/// Resolves printer and label template IDs from configuration
/// (<c>ShippingPrint</c> appsettings section).
/// </summary>
public sealed class ConfigurationShipmentPrinterResolver(IOptions<ShippingPrintOptions> options)
    : IShipmentPrinterResolver
{
    private readonly ShippingPrintOptions _options = options.Value;

    /// <inheritdoc />
    public Task<(Guid PrinterId, Guid LabelTemplateId)> ResolveAsync(CancellationToken ct = default)
    {
        if (_options.PrinterId == Guid.Empty)
            throw new InvalidOperationException(
                "ShippingPrint:PrinterId is not configured. " +
                "Add a valid Printer GUID to appsettings under 'ShippingPrint:PrinterId'.");

        if (_options.LabelTemplateId == Guid.Empty)
            throw new InvalidOperationException(
                "ShippingPrint:LabelTemplateId is not configured. " +
                "Add a valid LabelTemplate GUID to appsettings under 'ShippingPrint:LabelTemplateId'.");

        return Task.FromResult((_options.PrinterId, _options.LabelTemplateId));
    }
}

