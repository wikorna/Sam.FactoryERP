using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Domain.Events;

public record EdiStagingRequestedEvent(
    Guid StagingFileId,
    DateTime RequestedAtUtc,
    string? CorrelationId) : IDomainEvent;
