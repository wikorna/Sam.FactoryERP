using Inventory.Domain.Enums;

namespace Inventory.Application.Dtos;

/// <summary>Flat DTO for Fiori List Report rows.</summary>
public sealed record ItemListDto
{
    public required Guid Id { get; init; }
    public required string ItemNumber { get; init; }
    public required string Description { get; init; }
    public required string BaseUom { get; init; }
    public required string? MaterialGroup { get; init; }
    public required ItemStatus Status { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime? ModifiedAtUtc { get; init; }
}

/// <summary>Full DTO for Fiori Object Page (header + sections).</summary>
public sealed record ItemDetailDto
{
    // ── Header ──
    public required Guid Id { get; init; }
    public required string ItemNumber { get; init; }
    public required string Description { get; init; }
    public required string? LongDescription { get; init; }
    public required string BaseUom { get; init; }
    public required string? MaterialGroup { get; init; }
    public required ItemStatus Status { get; init; }
    public required decimal? GrossWeight { get; init; }
    public required decimal? NetWeight { get; init; }
    public required string? WeightUnit { get; init; }
    public required byte[] RowVersion { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required string? CreatedBy { get; init; }
    public required DateTime? ModifiedAtUtc { get; init; }
    public required string? ModifiedBy { get; init; }

    // ── Sections (child collections) ──
    public required IReadOnlyList<ItemUomDto> Uoms { get; init; }
    public required IReadOnlyList<ItemLocationDto> Locations { get; init; }
}

/// <summary>UOM conversion row.</summary>
public sealed record ItemUomDto(Guid Id, string UomCode, decimal ConversionFactor);

/// <summary>Location assignment row.</summary>
public sealed record ItemLocationDto(Guid Id, string Plant, string StorageLocation, bool IsDefault);
