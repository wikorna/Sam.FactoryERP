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

    /// <summary>
    /// String representation of the requesting user's Guid (from <c>ICurrentUserService.UserId</c>).
    /// Null when the event was published from a system path that lacks user context.
    /// Used by notification consumers for targeted SignalR push.
    /// </summary>
    public string? RequesterUserId { get; init; }

    /// <summary>Human-readable printer name for display in notifications.</summary>
    public string? PrinterName { get; init; }
}
