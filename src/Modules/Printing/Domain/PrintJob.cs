namespace Printing.Domain;

public class PrintJob
{
    public Guid Id { get; set; }
    public Guid PrinterId { get; set; }
    public Printer Printer { get; set; }
    public Guid PrintRequestId { get; set; }
    public PrintRequest PrintRequest { get; set; }
    public PrintJobStatus Status { get; set; }
    public DateTime CreatedUtc { get; set; }
}

public enum PrintJobStatus
{
    Queued,
    Printing,
    Completed,
    Failed
}

