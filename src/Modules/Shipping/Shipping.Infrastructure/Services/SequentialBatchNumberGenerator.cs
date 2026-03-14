using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Shipping.Application.Abstractions;
using Shipping.Infrastructure.Persistence;

namespace Shipping.Infrastructure.Services;

/// <summary>
/// Generates sequential batch numbers in the format "SB-yyyyMMdd-NNN".
/// Uses a database query to find the next sequence for today.
/// </summary>
public sealed class SequentialBatchNumberGenerator(ShippingDbContext db) : IBatchNumberGenerator
{
    /// <inheritdoc />
    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var todayPrefix = $"SB-{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-";

        // Find the highest sequence number for today.
        var lastBatchNumber = await db.ShipmentBatches
            .Where(b => b.BatchNumber.StartsWith(todayPrefix))
            .OrderByDescending(b => b.BatchNumber)
            .Select(b => b.BatchNumber)
            .FirstOrDefaultAsync(ct);

        int nextSequence = 1;
        if (lastBatchNumber is not null)
        {
            var sequencePart = lastBatchNumber[todayPrefix.Length..];
            if (int.TryParse(sequencePart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastSeq))
            {
                nextSequence = lastSeq + 1;
            }
        }

        return $"{todayPrefix}{nextSequence:D3}";
    }
}

