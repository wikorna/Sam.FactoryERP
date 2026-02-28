using Labeling.Domain.Exceptions;
using MassTransit;

namespace Labeling.Infrastructure.Consumers;

/// <summary>
/// Defines the retry policy and endpoint configuration for QrPrintRequestedConsumer.
/// Transient exceptions get exponential retry; permanent exceptions skip retry.
/// After all retries exhausted, the message goes to the _error queue (DLQ).
/// </summary>
public sealed class QrPrintRequestedConsumerDefinition : ConsumerDefinition<QrPrintRequestedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<QrPrintRequestedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Retry transient faults with exponential backoff: 5 attempts, 1s → 30s
        endpointConfigurator.UseMessageRetry(r => r
            .Exponential(5,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(5))
            .Ignore<PermanentPrinterException>()); // permanent errors skip retry → go straight to _error queue

        // Prefetch 1 — ensures per-printer serialization safety on a single consumer instance
        endpointConfigurator.PrefetchCount = 1;
    }
}
