using MediatR;

namespace EDI.Application.Features.SelectRows;

/// <summary>
/// Select or deselect specific rows for import.
/// </summary>
public sealed record SelectRowsCommand(
    Guid JobId,
    IReadOnlyList<int> RowIndexes,
    bool IsSelected = true) : IRequest<SelectRowsResponse>;

/// <summary>
/// Select all rows in the job for import.
/// </summary>
public sealed record SelectAllRowsCommand(Guid JobId, bool IsSelected = true) : IRequest<SelectRowsResponse>;

public sealed record SelectRowsResponse(Guid JobId, int AffectedRows);

