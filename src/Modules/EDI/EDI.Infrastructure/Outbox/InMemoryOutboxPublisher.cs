using EDI.Application.Abstractions;
using FactoryERP.SharedKernel.SeedWork;

namespace EDI.Infrastructure.Outbox;

public sealed class InMemoryOutboxPublisher : IOutboxPublisher
{
    public Task EnqueueAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        // TODO: persist to Outbox table
        return Task.CompletedTask;
    }
}
