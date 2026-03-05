using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Domain.Events;

public record EdiImportRequestedEvent(
    Guid StagingFileId,
    DateTime RequestedAtUtc,
    string? RequestedByUserId,
    string? CorrelationId) : IDomainEvent;
