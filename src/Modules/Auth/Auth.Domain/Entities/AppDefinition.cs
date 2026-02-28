namespace Auth.Domain.Entities;

/// <summary>
/// Represents an ERP application/module that users can access based on their roles.
/// Stored in <c>auth."Apps"</c>.
/// </summary>
public class AppDefinition
{
    public Guid Id { get; set; }

    /// <summary>Unique lowercase key used in code/routing (e.g. "inventory", "labeling").</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable display title (e.g. "Inventory Management").</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Client-side route (e.g. "/inventory").</summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>Optional CSS class for icon display in the sidebar.</summary>
    public string? IconCssClass { get; set; }

    /// <summary>Whether the app is currently active and visible.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Display order in the sidebar/navigation menu.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation
    public virtual ICollection<RoleAppAccess> RoleAppAccesses { get; set; } = [];
}

