namespace Printing.Application.Models;

/// <summary>
/// A fully rendered label document ready for dispatch to a physical printer.
/// Produced by <see cref="Abstractions.ITemplatePrintStrategy.Render"/>.
/// </summary>
public sealed record PrintDocument
{
    /// <summary>Rendered ZPL string, ready to stream to the printer port.</summary>
    public required string ZplContent { get; init; }

    /// <summary>Number of physical copies to print.</summary>
    public required int Copies { get; init; }

    /// <summary>Correlation ID for log tracing.</summary>
    public required Guid CorrelationId { get; init; }

    /// <summary>Idempotency key carried from the originating command.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>DPI the ZPL was generated for (informs printer profile validation).</summary>
    public required int RenderedDpi { get; init; }
}

