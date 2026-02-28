using System.Text.Json;
using EDI.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.PreviewEdiFile;

public sealed partial class PreviewEdiFileQueryHandler(
    IEdiFileJobRepository jobs,
    IEdiFileTypeConfigRepository configRepo,
    IStagingRepository staging,
    ILogger<PreviewEdiFileQueryHandler> logger)
    : IRequestHandler<PreviewEdiFileQuery, PreviewEdiFileResponse>
{
    public async Task<PreviewEdiFileResponse> Handle(
        PreviewEdiFileQuery request,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(request.JobId, cancellationToken)
                  ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        // Get file type config for header names
        var config = job.FileTypeCode is not null
            ? await configRepo.GetByCodeAsync(job.FileTypeCode, cancellationToken)
            : null;

        var headerNames = config?.Columns
            .OrderBy(c => c.Ordinal)
            .Select(c => c.DisplayLabel ?? c.ColumnName)
            .ToList() as IReadOnlyList<string> ?? [];

        // Get staging rows (preview = first N rows)
        var stagingRows = await staging.GetStagingRowsAsync(
            request.JobId, 1, request.PreviewLineCount, cancellationToken);

        int totalCount = await staging.GetStagingRowCountAsync(request.JobId, cancellationToken);

        var previewRows = stagingRows.Select(r =>
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(r.ParsedColumnsJson)
                         ?? new Dictionary<string, string?>();

            IReadOnlyList<ValidationErrorDto>? errors = null;
            if (r.ValidationErrorsJson is not null)
            {
                errors = JsonSerializer.Deserialize<List<ValidationErrorDto>>(r.ValidationErrorsJson);
            }

            return new PreviewRowDto(r.RowIndex, r.RawLine, parsed, r.IsValid, errors);
        }).ToList();

        LogPreview(logger, request.JobId, previewRows.Count, totalCount);

        return new PreviewEdiFileResponse(
            job.Id,
            job.FileName,
            job.FileTypeCode,
            config?.DisplayName,
            job.Status.ToString(),
            headerNames,
            previewRows,
            totalCount);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "EDI preview: JobId={JobId}, PreviewRows={PreviewCount}, TotalRows={TotalCount}")]
    private static partial void LogPreview(ILogger logger, Guid jobId, int previewCount, int totalCount);
}

