using Labeling.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Printing.Application.Abstractions;
using Printing.Application.Models;

namespace Printing.Infrastructure.Services;

/// <summary>
/// Resolves label templates from <c>labeling.LabelTemplates</c> via
/// <see cref="ILabelingDbContext"/>.
/// </summary>
public sealed class LabelingDbTemplateResolver(
    ILabelingDbContext dbContext,
    ILogger<LabelingDbTemplateResolver> logger)
    : ILabelTemplateResolver
{
    /// <inheritdoc />
    public async Task<LabelTemplateSpec> ResolveAsync(Guid templateId, CancellationToken ct = default)
    {
        var template = await dbContext.LabelTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId, ct);

        if (template is null)
            throw new InvalidOperationException(
                $"Label template '{templateId}' not found. " +
                "Seed a LabelTemplate row with this ID before enabling printing.");

        if (!template.IsActive)
            throw new InvalidOperationException(
                $"Label template '{templateId}' (key='{template.TemplateKey}', version='{template.Version}') " +
                "is inactive. Activate the template or update ShippingPrint:LabelTemplateId.");

        LogResolved(logger, templateId, template.TemplateKey, template.Version);

        return new LabelTemplateSpec
        {
            Id             = template.Id,
            TemplateKey    = template.TemplateKey,
            Version        = template.Version,
            ZplBody        = template.ZplBody,
            DesignDpi      = template.DesignDpi,
            LabelWidthMm   = template.LabelWidthMm,
            LabelHeightMm  = template.LabelHeightMm,
        };
    }

    private static void LogResolved(ILogger logger, Guid templateId, string templateKey, string version) => logger.LogDebug("Label template resolved: Id={TemplateId}, Key={TemplateKey}, Version={Version}", templateId, templateKey, version);
}

