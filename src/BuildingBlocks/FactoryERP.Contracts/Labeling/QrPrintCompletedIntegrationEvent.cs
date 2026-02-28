using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Labeling;

/// <summary>
/// Published when a QR print job completes successfully.
/// Other modules can subscribe to react to print completion.
/// </summary>
public sealed record QrPrintCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid PrintJobId { get; init; }
    public required Guid PrinterId { get; init; }
    public required DateTime CompletedAtUtc { get; init; }
}
