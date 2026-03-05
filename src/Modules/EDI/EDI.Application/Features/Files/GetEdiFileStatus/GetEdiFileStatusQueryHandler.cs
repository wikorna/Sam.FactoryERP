using EDI.Application.Abstractions;
using MediatR;

namespace EDI.Application.Features.Files.GetEdiFileStatus;

public sealed class GetEdiFileStatusQueryHandler(IEdiStagingFileRepository repository)
    : IRequestHandler<GetEdiFileStatusQuery, GetEdiFileStatusResult?>
{
    public async Task<GetEdiFileStatusResult?> Handle(GetEdiFileStatusQuery request, CancellationToken cancellationToken)
    {
        var file = await repository.GetByIdWithErrorsAsync(request.StagingId, cancellationToken);
        if (file is null) return null;

        var errorCount = file.Errors.Count;
        var errors = file.Errors.Take(100).Select(e => new EdiFileErrorSummary(
            e.Code,
            e.Message,
            e.RowNumber,
            e.ColumnName,
            e.Severity.ToString()
        )).ToList();

        return new GetEdiFileStatusResult(
            StagingId: file.Id,
            Status: file.Status.ToString(),
            ProgressPercent: file.ProgressPercent,
            RowCountTotal: file.RowCountTotal,
            RowCountProcessed: file.RowCountProcessed,
            FileName: file.OriginalFileName,
            ErrorCode: file.ErrorCode,
            ErrorMessage: file.ErrorMessage,
            ErrorCount: errorCount,
            Errors: errors
        );
    }
}
