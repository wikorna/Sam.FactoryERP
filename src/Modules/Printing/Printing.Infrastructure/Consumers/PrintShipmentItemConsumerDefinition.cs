using MassTransit;

namespace Printing.Infrastructure.Consumers;

/// <summary>
/// Retry and endpoint configuration for <see cref="PrintShipmentItemConsumer"/>.
/// </summary>
public sealed class PrintShipmentItemConsumerDefinition
    : ConsumerDefinition<PrintShipmentItemConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PrintShipmentItemConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Exponential back-off: 5 attempts, 2 s → 60 s.
        // Covers transient TCP timeouts (ZebraLabelPrinterClient already retries
        // twice internally, so this outer retry handles multi-minute network outages).
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(6)));

        // One label at a time per consumer instance — avoids saturating the printer port.
        endpointConfigurator.PrefetchCount = 1;
    }
}

