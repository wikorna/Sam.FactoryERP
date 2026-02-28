using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Labeling;

/// <summary>
/// Published when a new QR print job is created and ready for processing.
/// Consumed by QrPrintRequestedConsumer in WorkerHost.
/// </summary>
public sealed record QrPrintRequestedIntegrationEvent : IntegrationEvent
{
    public required Guid PrintJobId { get; init; }
    public required Guid PrinterId { get; init; }
}
