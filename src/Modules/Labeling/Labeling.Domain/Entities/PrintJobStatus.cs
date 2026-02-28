namespace Labeling.Domain.Entities;

/// <summary>
/// Represents the lifecycle states of a print job.
/// Transitions are enforced inside the <see cref="PrintJob"/> aggregate.
/// </summary>
public enum PrintJobStatus
{
    /// <summary>Job created, waiting for a consumer to pick it up.</summary>
    Queued = 0,

    /// <summary>Consumer has picked the job and is sending ZPL to the printer.</summary>
    Dispatching = 1,

    /// <summary>ZPL successfully delivered to the printer.</summary>
    Printed = 2,

    /// <summary>A transient error occurred; MassTransit will retry.</summary>
    FailedRetrying = 3,

    /// <summary>All retries exhausted; job moved to dead-letter queue.</summary>
    DeadLettered = 4,

    /// <summary>Job was explicitly canceled by an operator or system.</summary>
    Canceled = 5
}

