using EDI.Application.Features.GetEdiFileJobs;
using MediatR;

namespace EDI.Application.Features.GetEdiFileJob;

public sealed record GetEdiFileJobQuery(Guid JobId) : IRequest<EdiFileJobDto?>;
