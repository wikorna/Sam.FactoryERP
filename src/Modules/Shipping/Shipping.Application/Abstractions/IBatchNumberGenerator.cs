namespace Shipping.Application.Abstractions;

/// <summary>
/// Generates sequential batch numbers in the format "SB-yyyyMMdd-NNN".
/// </summary>
public interface IBatchNumberGenerator
{
    /// <summary>
    /// Generates the next batch number for today's date.
    /// Thread-safe and unique within the same database.
    /// </summary>
    Task<string> GenerateAsync(CancellationToken ct = default);
}

