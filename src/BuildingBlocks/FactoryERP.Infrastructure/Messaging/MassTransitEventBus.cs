using FactoryERP.Abstractions.Messaging;
using MassTransit;

namespace FactoryERP.Infrastructure.Messaging;

/// <summary>
/// Adapts <see cref="IEventBus"/> to MassTransit's <see cref="IPublishEndpoint"/>.
/// Registered as scoped so it participates in the current consume/request scope.
/// </summary>
internal sealed class MassTransitEventBus(IPublishEndpoint publishEndpoint) : IEventBus
{
    public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        => publishEndpoint.Publish(message, cancellationToken);
}
