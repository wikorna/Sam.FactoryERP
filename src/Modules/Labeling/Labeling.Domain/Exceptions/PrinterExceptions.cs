namespace Labeling.Domain.Exceptions;

/// <summary>
/// Thrown when a transient printer communication error occurs (network timeout, connection refused, etc.).
/// MassTransit retry policy should redeliver the message.
/// </summary>
public sealed class TransientPrinterException : Exception
{
    public string PrinterId { get; }

    public TransientPrinterException(string printerId, string message, Exception? inner = null)
        : base(message, inner)
    {
        PrinterId = printerId;
    }
}

/// <summary>
/// Thrown when a permanent printer error occurs (invalid ZPL, printer disabled, etc.).
/// The message should NOT be retried.
/// </summary>
public sealed class PermanentPrinterException : Exception
{
    public string PrinterId { get; }

    public PermanentPrinterException(string printerId, string message, Exception? inner = null)
        : base(message, inner)
    {
        PrinterId = printerId;
    }
}

