using MassTransit;

namespace Shipping.Infrastructure.Consumers;

/// <summary>
/// Defines retry policy and endpoint configuration for
/// <see cref="ShipmentApprovedForPrintingConsumer"/>.
/// </summary>
public sealed class ShipmentApprovedForPrintingConsumerDefinition
    : ConsumerDefinition<ShipmentApprovedForPrintingConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<ShipmentApprovedForPrintingConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Exponential back-off: 3 attempts, initial 2 s, max 30 s, delta 5 s.
        // Covers transient DB / RabbitMQ failures during printer resolution or SaveChanges.
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(3,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(5)));

        // Process one batch at a time — prevents concurrent mutations of the same aggregate.
        endpointConfigurator.PrefetchCount = 1;
    }
}

