using MediatR;

namespace Printing.Application.Features.PrintJobs;

public record CreatePrintJobCommand(Guid PrinterId, Guid PrintRequestId) : IRequest<Guid>;

