using Labeling.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Printing.Application.Abstractions;
using Printing.Application.Models;

namespace Printing.Infrastructure.Services;

/// <summary>
/// Resolves printer connection and media profiles from <c>labeling.Printers</c>.
/// </summary>
public sealed class LabelingDbPrinterProfileResolver(
    ILabelingDbContext dbContext,
    ILogger<LabelingDbPrinterProfileResolver> logger)
    : IPrinterProfileResolver
{
    /// <inheritdoc />
    public async Task<PrinterProfile> ResolveAsync(Guid printerId, CancellationToken ct = default)
    {
        var printer = await dbContext.Printers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == printerId, ct);

        if (printer is null)
            throw new InvalidOperationException(
                $"Printer '{printerId}' not found in the registry. " +
                "Register the printer in the Labeling module before enabling printing.");

        if (!printer.IsEnabled)
            throw new InvalidOperationException(
                $"Printer '{printer.Name}' (id='{printerId}') is disabled. " +
                "Enable it in the printer registry or update ShippingPrint:PrinterId.");

        LogResolved(logger, printerId, printer.Name, printer.Host, printer.Port);

        return new PrinterProfile
        {
            Id           = printer.Id,
            Name         = printer.Name,
            Host         = printer.Host,
            Port         = printer.Port,
            Protocol     = printer.Protocol.ToString(),
            Dpi          = printer.Dpi == 0 ? 203 : printer.Dpi,
            LabelWidthMm = printer.LabelWidthMm,
            LabelHeightMm = printer.LabelHeightMm,
            IsEnabled    = printer.IsEnabled,
        };
    }

    private static void LogResolved(ILogger logger, Guid printerId, string name, string host, int port) => logger.LogDebug("Printer profile resolved: Id={PrinterId}, Name={Name}, Host={Host}, Port={Port}", printerId, name, host, port);
}

