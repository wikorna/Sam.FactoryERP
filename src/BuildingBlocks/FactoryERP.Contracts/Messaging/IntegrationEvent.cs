namespace FactoryERP.Contracts.Messaging;

/// <summary>
/// Base record for all cross-module integration events.
/// Domain layer must NOT reference this — it lives in BuildingBlocks.Contracts.
/// </summary>
/// <remarks>
/// <para><b>MessageId</b>: unique per message instance (idempotency key for consumers).</para>
/// <para><b>CorrelationId</b>: ties all messages in a logical flow (e.g. batch lifecycle).</para>
/// <para><b>CausationId</b>: the MessageId of the event that caused this one (event chain tracing).</para>
/// <para><b>SchemaVersion</b>: monotonic version for forward-compatible deserialization.</para>
/// </remarks>
public abstract record IntegrationEvent
{
    /// <summary>Unique message identifier — doubles as idempotency key for consumers.</summary>
    public Guid MessageId { get; init; } = Guid.NewGuid();

    /// <summary>Groups all events in a single business flow (e.g. shipment batch lifecycle).</summary>
    public Guid CorrelationId { get; init; }

    /// <summary>MessageId of the event that caused this event (event chain tracing).</summary>
    public Guid? CausationId { get; init; }

    /// <summary>When this event occurred.</summary>
    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>User who triggered the original action.</summary>
    public string RequestedBy { get; init; } = string.Empty;

    /// <summary>Monotonic schema version for forward-compatible deserialization (default 1).</summary>
    public int SchemaVersion { get; init; } = 1;
}
