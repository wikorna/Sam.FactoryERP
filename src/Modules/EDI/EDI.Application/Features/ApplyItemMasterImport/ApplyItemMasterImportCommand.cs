using MediatR;

namespace EDI.Application.Features.ApplyItemMasterImport;

public sealed record ApplyItemMasterImportCommand(Guid JobId) : IRequest<int>;
