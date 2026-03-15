namespace Printing.Domain.Enums;

public enum PrintItemStatus
{
    /// <summary>Item is queued, no print job dispatched yet.</summary>
    Pending,

    /// <summary>A PrintJob has been created and dispatched to the printer.</summary>
    Dispatched,

    /// <summary>Item was printed successfully.</summary>
    Printed,

    /// <summary>Print dispatch failed permanently.</summary>
    Failed,

    /// <summary>Item was skipped — already printed (idempotent guard).</summary>
    Skipped
}
