namespace Printing.Domain.Enums;

public enum PrintJobStatus
{
    /// <summary>Job has been created and is waiting for the printer client.</summary>
    Queued,

    /// <summary>ZPL is currently being streamed to the printer port.</summary>
    Printing,

    /// <summary>Printer accepted the job successfully.</summary>
    Completed,

    /// <summary>Printer dispatch failed (permanent or exhausted retries).</summary>
    Failed
}
