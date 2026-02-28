namespace FactoryERP.Contracts.Messaging;

/// <summary>
/// Base record for all cross-module integration events.
/// Domain layer must NOT reference this — it lives in BuildingBlocks.Contracts.
/// </summary>
public abstract record IntegrationEvent
{
    public Guid MessageId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
    public string RequestedBy { get; init; } = string.Empty;
}
