namespace Inventory.Domain.Enums;

/// <summary>Item lifecycle status.</summary>
public enum ItemStatus
{
    /// <summary>Item is fully active and available.</summary>
    Active = 0,

    /// <summary>Item is blocked from transactions but still visible.</summary>
    Blocked = 1,

    /// <summary>Item is soft-deleted / archived.</summary>
    Inactive = 2
}
