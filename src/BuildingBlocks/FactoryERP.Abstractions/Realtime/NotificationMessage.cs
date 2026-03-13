namespace FactoryERP.Abstractions.Realtime;

/// <summary>
/// A general-purpose notification pushed from server to client.
/// </summary>
/// <param name="EventType">Machine-readable event discriminator, e.g. "PrintCompleted".</param>
/// <param name="Payload">Domain-specific data object — serialised as JSON by SignalR.</param>
/// <param name="OccurredAt">UTC timestamp of the event origin.</param>
public sealed record NotificationMessage(
    string EventType,
    object Payload,
    DateTimeOffset OccurredAt);

/// <summary>
/// An ephemeral toast/snackbar message pushed to the Angular UI.
/// </summary>
/// <param name="Level">Severity: "info" | "success" | "warning" | "error".</param>
/// <param name="Title">Short display title shown in the toast header.</param>
/// <param name="Body">Human-readable details.</param>
/// <param name="DurationMs">Auto-dismiss timeout in milliseconds (default 5 s).</param>
public sealed record ToastMessage(
    string Level,
    string Title,
    string Body,
    int DurationMs = 5_000);

