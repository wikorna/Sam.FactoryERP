using MediatR;
using Labeling.Application.Models;

namespace Labeling.Application.Features.PrintJobs;

public sealed record CreateProductLabelJobCommand(
    string IdempotencyKey,
    Guid PrinterId,
    ProductLabelData LabelData,
    int Copies,
    string RequestedBy
) : IRequest<CreatePrintJobResult>;

