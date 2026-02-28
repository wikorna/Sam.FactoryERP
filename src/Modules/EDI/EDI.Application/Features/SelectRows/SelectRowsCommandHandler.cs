using EDI.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EDI.Application.Features.SelectRows;

public sealed partial class SelectRowsCommandHandler(
    IEdiFileJobRepository jobs,
    IStagingRepository staging,
    ILogger<SelectRowsCommandHandler> logger)
    : IRequestHandler<SelectRowsCommand, SelectRowsResponse>,
      IRequestHandler<SelectAllRowsCommand, SelectRowsResponse>
{
    public async Task<SelectRowsResponse> Handle(
        SelectRowsCommand request,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(request.JobId, cancellationToken)
                  ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        await staging.UpdateRowSelectionAsync(
            request.JobId, request.RowIndexes, request.IsSelected, cancellationToken);

        LogRowSelection(logger, request.JobId, request.RowIndexes.Count, request.IsSelected);

        return new SelectRowsResponse(job.Id, request.RowIndexes.Count);
    }

    public async Task<SelectRowsResponse> Handle(
        SelectAllRowsCommand request,
        CancellationToken cancellationToken)
    {
        var job = await jobs.GetAsync(request.JobId, cancellationToken)
                  ?? throw new InvalidOperationException($"EDI job not found: {request.JobId}");

        // Get all row indexes
        int totalCount = await staging.GetStagingRowCountAsync(request.JobId, cancellationToken);
        var allIndexes = Enumerable.Range(1, totalCount).ToList();

        await staging.UpdateRowSelectionAsync(
            request.JobId, allIndexes, request.IsSelected, cancellationToken);

        LogSelectAll(logger, request.JobId, totalCount, request.IsSelected);

        return new SelectRowsResponse(job.Id, totalCount);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "EDI row selection: JobId={JobId}, Count={Count}, Selected={IsSelected}")]
    private static partial void LogRowSelection(ILogger logger, Guid jobId, int count, bool isSelected);

    [LoggerMessage(Level = LogLevel.Information, Message = "EDI select all: JobId={JobId}, Count={Count}, Selected={IsSelected}")]
    private static partial void LogSelectAll(ILogger logger, Guid jobId, int count, bool isSelected);
}

