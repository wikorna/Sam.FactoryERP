using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Domain.Aggregates.EdiFileJobAggregate;
using EDI.Domain.Entities;
using FactoryERP.Abstractions.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.ParseEdiFile;

public sealed class ParseEdiFileCommandHandler(
    IEdiFileStore fileStore,
    IEdiFileJobRepository jobs,
    IEdiParserFactory parsers,
    IStagingRepository staging,
    IEdiFileTypeConfigRepository configRepo,
    ICacheService cache,
    ILogger<ParseEdiFileCommandHandler> logger)
    : IRequestHandler<ParseEdiFileCommand, int>
{
    public async Task<int> Handle(ParseEdiFileCommand request, CancellationToken cancellationToken)
    {
        EdiFileJob job = await jobs.GetAsync(request.JobId, cancellationToken).ConfigureAwait(false)
                         ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        job.MarkParsing();
        await jobs.SaveAsync(job, cancellationToken).ConfigureAwait(false);

        var partner = await jobs.GetPartnerProfileAsync(job.PartnerCode, cancellationToken).ConfigureAwait(false);

        await staging.ClearJobAsync(job.Id, cancellationToken).ConfigureAwait(false);

        EdiFileRef file = new(job.PartnerCode, job.FileName, job.SourcePath);
        int count;

        // Config-driven path: if the job has a FileTypeCode, use the generic parser
        if (!string.IsNullOrEmpty(job.FileTypeCode))
        {
            var config = await configRepo.GetByCodeAsync(job.FileTypeCode, cancellationToken);
            if (config is not null)
            {
                LogConfigDrivenParse(logger, job.Id, job.FileTypeCode);
                count = await ParseWithConfigAsync(job.Id, file, config, cancellationToken);
            }
            else
            {
                count = await ParseLegacyAsync(job.Id, file, partner, cancellationToken);
            }
        }
        else
        {
            count = await ParseLegacyAsync(job.Id, file, partner, cancellationToken);
        }

        job.MarkParsed(count);
        await jobs.SaveAsync(job, cancellationToken).ConfigureAwait(false);

        // Invalidate job caches after state transition (Parsing → Parsed)
        await cache.RemoveAsync(EdiCacheKeys.JobById(job.Id), cancellationToken).ConfigureAwait(false);
        await cache.InvalidateTagAsync(EdiCacheKeys.TagJobs, cancellationToken).ConfigureAwait(false);

        return count;
    }

    private async Task<int> ParseWithConfigAsync(
        Guid jobId, EdiFileRef file, EdiFileTypeConfig config, CancellationToken ct)
    {
        // This handler is in Application layer — it calls IStagingRepository
        // to insert the parsed rows. The actual parsing is done via the
        // config columns defined in the config entity.
        await using Stream stream = await fileStore.OpenReadAsync(file, ct).ConfigureAwait(false);

        // Simple line-by-line parsing using config metadata
        using var reader = new StreamReader(stream, leaveOpen: true);
        var columns = config.Columns.OrderBy(c => c.Ordinal).ToList();
        char delimiter = config.Delimiter.Length > 0 ? config.Delimiter[0] : ',';
        int totalSkip = config.SkipLines + (config.HasHeaderRow ? config.HeaderLineCount : 0);

        int lineNo = 0;
        int dataRowIndex = 0;
        var batch = new List<EdiStagingRow>(100);

        string? line;
        while ((line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            lineNo++;

            if (lineNo <= totalSkip) continue;
            if (string.IsNullOrWhiteSpace(line)) continue;

            dataRowIndex++;

            var parsed = ParseCsvLine(line, columns, delimiter);

            batch.Add(new EdiStagingRow
            {
                Id = Guid.NewGuid(),
                JobId = jobId,
                FileTypeCode = config.FileTypeCode,
                RowIndex = dataRowIndex,
                IsSelected = true,
                RawLine = line,
                ParsedColumnsJson = System.Text.Json.JsonSerializer.Serialize(parsed),
                IsValid = true
            });

            if (batch.Count >= 100)
            {
                await staging.InsertStagingRowsAsync(batch, ct).ConfigureAwait(false);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await staging.InsertStagingRowsAsync(batch, ct).ConfigureAwait(false);
        }

        return dataRowIndex;
    }

    private static Dictionary<string, string?> ParseCsvLine(
        string line, List<EdiColumnDefinition> columns, char delimiter)
    {
        var fields = line.Split(delimiter);
        var result = new Dictionary<string, string?>(columns.Count);

        for (int i = 0; i < columns.Count; i++)
        {
            string? value = i < fields.Length ? fields[i].Trim() : null;
            result[columns[i].ColumnName] = string.IsNullOrEmpty(value) ? null : value;
        }

        return result;
    }

    private async Task<int> ParseLegacyAsync(
        Guid jobId, EdiFileRef file, EDI.Domain.Entities.PartnerProfile partner, CancellationToken ct)
    {
        await using Stream stream = await fileStore.OpenReadAsync(file, ct).ConfigureAwait(false);
        int count = 0;

        if (partner.Format.Value == "custom-po")
        {
            var parser = parsers.GetParser<EDI.Application.DTOs.PurchaseOrderDto>(partner);
            await foreach (var row in parser.ParseAsync(stream, ct).ConfigureAwait(false))
            {
                await staging.InsertPurchaseOrderAsync(jobId, row, ct).ConfigureAwait(false);
                count++;
            }
        }
        else
        {
            var parser = parsers.GetParser<ItemMasterStagingRow>(partner);
            await foreach (var row in parser.ParseAsync(stream, ct).ConfigureAwait(false))
            {
                await staging.InsertItemMasterRowAsync(jobId, row, ct).ConfigureAwait(false);
                count++;
            }
        }

        return count;
    }

    private static void LogConfigDrivenParse(ILogger logger, Guid jobId, string fileTypeCode) => logger.LogInformation("Config-driven parse: JobId={JobId}, FileType={FileTypeCode}", jobId, fileTypeCode);
}
