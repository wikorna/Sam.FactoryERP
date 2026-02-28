using MediatR;

namespace EDI.Application.Features.ImportSelectedRows;

/// <summary>
/// Import only selected + valid staging rows for a job.
/// </summary>
public sealed record ImportSelectedRowsCommand(Guid JobId) : IRequest<ImportSelectedRowsResponse>;

public sealed record ImportSelectedRowsResponse(
    Guid JobId,
    int ImportedRows,
    int SkippedRows,
    string Status);

