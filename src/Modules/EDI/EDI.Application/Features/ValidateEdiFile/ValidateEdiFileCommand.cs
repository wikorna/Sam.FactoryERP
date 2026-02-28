using MediatR;

namespace EDI.Application.Features.ValidateEdiFile;

/// <summary>
/// Validates all staging rows for a job against the column definitions
/// from the file type config (required, regex, type, maxLength).
/// </summary>
public sealed record ValidateEdiFileCommand(Guid JobId) : IRequest<ValidateEdiFileResponse>;

public sealed record ValidateEdiFileResponse(
    Guid JobId,
    int TotalRows,
    int ValidRows,
    int InvalidRows);

