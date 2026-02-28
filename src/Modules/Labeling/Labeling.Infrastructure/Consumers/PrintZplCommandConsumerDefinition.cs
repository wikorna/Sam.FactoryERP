using Labeling.Domain.Exceptions;
using MassTransit;

namespace Labeling.Infrastructure.Consumers;

/// <summary>
/// Defines the retry policy for <see cref="PrintZplCommandConsumer"/>.
/// Permanent errors are not retried and go directly to the _error queue.
/// </summary>
public sealed class PrintZplCommandConsumerDefinition : ConsumerDefinition<PrintZplCommandConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PrintZplCommandConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r => r
            .Exponential(5,
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(5))
            .Ignore<PermanentPrinterException>());

        endpointConfigurator.PrefetchCount = 1;
    }
}

