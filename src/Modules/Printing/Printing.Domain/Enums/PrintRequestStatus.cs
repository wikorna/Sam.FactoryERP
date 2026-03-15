namespace Printing.Domain.Enums;

public enum PrintRequestStatus
{
    /// <summary>Request has been created and is waiting to be dispatched.</summary>
    Pending,

    /// <summary>One or more items are currently being printed.</summary>
    Processing,

    /// <summary>All items were printed successfully.</summary>
    Completed,

    /// <summary>Some items printed, some failed.</summary>
    PartiallyCompleted,

    /// <summary>All items failed to print.</summary>
    Failed
}
