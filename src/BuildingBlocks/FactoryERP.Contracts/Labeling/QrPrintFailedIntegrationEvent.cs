using FactoryERP.Contracts.Messaging;

namespace FactoryERP.Contracts.Labeling;

/// <summary>
/// Published when a QR print job fails permanently (after all retries exhausted).
/// Can trigger alerting, dashboards, or compensating workflows.
/// </summary>
public sealed record QrPrintFailedIntegrationEvent : IntegrationEvent
{
    public required Guid PrintJobId { get; init; }
    public required Guid PrinterId { get; init; }
    public required string FailureReason { get; init; }
    public required DateTime FailedAtUtc { get; init; }
}
