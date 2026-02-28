using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Domain.Aggregates.EdiFileJobAggregate;

public sealed record EdiFileReceived(Guid JobId, string PartnerCode, string FileName) : IDomainEvent;

public sealed record EdiFileApplied(Guid JobId, string PartnerCode, string FileName, int RecordsApplied) : IDomainEvent;

public sealed record EdiFileFailed(Guid JobId, string PartnerCode, string ErrorCode) : IDomainEvent;
