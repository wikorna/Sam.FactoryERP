using EDI.Application.Abstractions;
using MediatR;

namespace EDI.Application.Features.Files.GetEdiFilePreview;

public sealed class GetEdiFilePreviewQueryHandler(IEdiStagingFileRepository repository)
    : IRequestHandler<GetEdiFilePreviewQuery, GetEdiFilePreviewResult?>
{
    public async Task<GetEdiFilePreviewResult?> Handle(GetEdiFilePreviewQuery request, CancellationToken cancellationToken)
    {
        var stagingFile = await repository.GetByIdAsync(request.StagingId, cancellationToken);

        if (stagingFile is null) return null;

        var totalRows = await repository.GetStagingRowCountAsync(request.StagingId, cancellationToken);

        var skip = (request.PageNumber - 1) * request.PageSize;
        var pagedRows = await repository.GetStagingRowsAsync(request.StagingId, skip, request.PageSize, cancellationToken);

        var rows = pagedRows
            .Select(r => new EdiStagingRowDto(
                r.Id,
                r.RowIndex,
                r.RawLine,
                r.ParsedColumnsJson,
                r.IsSelected,
                r.IsValid,
                r.ValidationErrorsJson))
            .ToList();

        return new GetEdiFilePreviewResult(
            request.StagingId,
            totalRows,
            request.PageNumber,
            request.PageSize,
            rows);
    }
}
