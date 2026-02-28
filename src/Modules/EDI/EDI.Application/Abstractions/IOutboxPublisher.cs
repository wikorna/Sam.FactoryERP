using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Application.Abstractions;

public interface IOutboxPublisher
{
    public Task EnqueueAsync(IDomainEvent domainEvent, CancellationToken ct);
}
