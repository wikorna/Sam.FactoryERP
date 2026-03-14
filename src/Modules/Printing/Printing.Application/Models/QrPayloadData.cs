namespace Printing.Application.Models;

/// <summary>
/// The canonical QR code payload string plus metadata.
/// </summary>
/// <remarks>
/// <para>
/// Format (v1, pipe-delimited, scanner-routable):
/// <c>v1|{CustomerCode}|{PartNo}|{ProductName}|{Quantity}|{PoNumber}|{PoItem}|{DueDate}|{BatchNumber}|{LineNumber}</c>
/// </para>
/// <para>
/// The <c>v1|</c> prefix lets scanner firmware detect the payload version and
/// route to the correct parsing logic without a URL round-trip.
/// </para>
/// </remarks>
public sealed record QrPayloadData
{
    /// <summary>The ready-to-encode QR string.</summary>
    public required string Payload { get; init; }

    /// <summary>Payload format version — incremented when field layout changes.</summary>
    public required string Version { get; init; }

    /// <summary>Human-readable label for audit/logging.</summary>
    public required string PartNo { get; init; }
}

