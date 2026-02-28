using EDI.Domain.Aggregates.EdiFileJobAggregate;
using MediatR;

namespace EDI.Application.Features.GetEdiFileJobs;

public sealed record GetEdiFileJobsQuery(
    string? PartnerCode = null,
    EdiFileJobStatus? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<GetEdiFileJobsResponse>;

public sealed record GetEdiFileJobsResponse(
    IReadOnlyList<EdiFileJobDto> Jobs,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record EdiFileJobDto(
    Guid Id,
    string PartnerCode,
    string FileName,
    long SizeBytes,
    string Format,
    string SchemaVersion,
    DateTime ReceivedAtUtc,
    DateTime? AppliedAtUtc,
    string Status,
    string? ErrorCode,
    string? ErrorMessage,
    int ParsedRecords,
    int AppliedRecords);
