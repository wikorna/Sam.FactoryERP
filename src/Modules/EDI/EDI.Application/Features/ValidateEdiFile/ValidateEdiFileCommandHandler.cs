using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EDI.Application.Abstractions;
using EDI.Application.Caching;
using EDI.Application.Features.PreviewEdiFile;
using EDI.Domain.Entities;
using FactoryERP.Abstractions.Caching;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.ValidateEdiFile;

public sealed class ValidateEdiFileCommandHandler(
    IEdiFileJobRepository jobs,
    IEdiFileTypeConfigRepository configRepo,
    IStagingRepository staging,
    ICacheService cache,
    ILogger<ValidateEdiFileCommandHandler> logger)
    : IRequestHandler<ValidateEdiFileCommand, ValidateEdiFileResponse>
{
    private const int BatchSize = 500;

    public async Task<ValidateEdiFileResponse> Handle(
        ValidateEdiFileCommand request,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(request.JobId, cancellationToken)
                  ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        if (string.IsNullOrEmpty(job.FileTypeCode))
        {
            throw new InvalidOperationException(
                $"Job {request.JobId} has no file type code — cannot validate without config.");
        }

        var config = await configRepo.GetByCodeAsync(job.FileTypeCode, cancellationToken)
                     ?? throw new InvalidOperationException(
                         $"File type config not found: {job.FileTypeCode}");

        job.MarkValidating();
        await jobs.SaveAsync(job, cancellationToken);

        int totalRows = await staging.GetStagingRowCountAsync(job.Id, cancellationToken);
        int validRows = 0;
        int invalidRows = 0;
        int page = 1;

        while (true)
        {
            var rows = await staging.GetStagingRowsAsync(job.Id, page, BatchSize, cancellationToken);
            if (rows.Count == 0) break;

            foreach (var row in rows)
            {
                var errors = ValidateRow(row, config.Columns);

                row.IsValid = errors.Count == 0;
                row.ValidationErrorsJson = errors.Count > 0
                    ? JsonSerializer.Serialize(errors)
                    : null;

                await staging.UpdateRowValidationAsync(row, cancellationToken);

                if (row.IsValid) validRows++;
                else invalidRows++;
            }

            page++;
        }

        job.MarkValidated();
        await jobs.SaveAsync(job, cancellationToken);

        await cache.RemoveAsync(EdiCacheKeys.JobById(job.Id), cancellationToken);
        await cache.InvalidateTagAsync(EdiCacheKeys.TagJobs, cancellationToken);

        LogValidationComplete(logger, job.Id, totalRows, validRows, invalidRows);

        return new ValidateEdiFileResponse(job.Id, totalRows, validRows, invalidRows);
    }

    private static List<ValidationErrorDto> ValidateRow(
        EdiStagingRow row,
        IReadOnlyList<EdiColumnDefinition> columns)
    {
        var errors = new List<ValidationErrorDto>();
        var parsed = JsonSerializer.Deserialize<Dictionary<string, string?>>(row.ParsedColumnsJson)
                     ?? new Dictionary<string, string?>();

        foreach (var col in columns)
        {
            parsed.TryGetValue(col.ColumnName, out var value);
            string trimmed = value?.Trim() ?? string.Empty;

            // Required check
            if (col.IsRequired && string.IsNullOrWhiteSpace(trimmed))
            {
                errors.Add(new ValidationErrorDto(col.ColumnName, "Value is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            // MaxLength check
            if (col.MaxLength.HasValue && trimmed.Length > col.MaxLength.Value)
            {
                errors.Add(new ValidationErrorDto(col.ColumnName,
                    $"Exceeds maximum length of {col.MaxLength.Value}."));
            }

            // Regex check
            if (!string.IsNullOrEmpty(col.ValidationRegex))
            {
                if (!Regex.IsMatch(trimmed, col.ValidationRegex, RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    errors.Add(new ValidationErrorDto(col.ColumnName,
                        $"Does not match pattern: {col.ValidationRegex}"));
                }
            }

            // Data type check
            switch (col.DataType.ToUpperInvariant())
            {
                case "INTEGER":
                    if (!long.TryParse(trimmed, CultureInfo.InvariantCulture, out _))
                        errors.Add(new ValidationErrorDto(col.ColumnName, "Not a valid integer."));
                    break;

                case "DECIMAL":
                    if (!decimal.TryParse(trimmed, CultureInfo.InvariantCulture, out _))
                        errors.Add(new ValidationErrorDto(col.ColumnName, "Not a valid decimal."));
                    break;

                case "DATE":
                    if (!DateOnly.TryParse(trimmed, CultureInfo.InvariantCulture, out _))
                        errors.Add(new ValidationErrorDto(col.ColumnName, "Not a valid date."));
                    break;

                case "DATETIME":
                    if (!DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        errors.Add(new ValidationErrorDto(col.ColumnName, "Not a valid datetime."));
                    break;
            }
        }

        return errors;
    }

    private static void LogValidationComplete(ILogger logger, Guid jobId, int total, int valid, int invalid) => logger.LogInformation("EDI validation complete: JobId={JobId}, Total={Total}, Valid={Valid}, Invalid={Invalid}", jobId, total, valid, invalid);
}

