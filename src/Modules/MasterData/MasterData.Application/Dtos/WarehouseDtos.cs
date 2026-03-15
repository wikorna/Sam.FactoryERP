namespace MasterData.Application.Dtos;

/// <summary>Warehouse row for list views.</summary>
public sealed record WarehouseListDto(
    Guid Id,
    string Code,
    string Name,
    string Plant,
    bool IsActive);

/// <summary>Full warehouse detail.</summary>
public sealed record WarehouseDetailDto(
    Guid Id,
    string Code,
    string Name,
    string Plant,
    string? Description,
    bool IsActive,
    byte[] RowVersion,
    DateTime CreatedAtUtc,
    DateTime ModifiedAtUtc);
