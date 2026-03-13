namespace Labeling.Domain.Entities;

/// <summary>
/// Versioned label template definition.
/// Stores the ZPL template body and metadata needed for label rendering.
/// </summary>
/// <remarks>
/// <para>Templates are referenced by <c>ShipmentBatch.LabelTemplateId</c> when printing.</para>
/// <para>Supports versioning: multiple versions can exist for the same template key,
/// but only one version should be active at a time per key.</para>
/// </remarks>
public sealed class LabelTemplate
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; private set; }

    /// <summary>Canonical template key, e.g. "ProductLabel", "ShippingLabel".</summary>
    public string TemplateKey { get; private set; } = string.Empty;

    /// <summary>Version string, e.g. "v1", "v2.1".</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Human-readable name shown in the UI.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Optional description of what changed in this version.</summary>
    public string? Description { get; private set; }

    /// <summary>ZPL template body with placeholders, e.g. {{PartNo}}, {{QrPayload}}.</summary>
    public string ZplBody { get; private set; } = string.Empty;

    /// <summary>Target DPI the template was designed for (203, 300, 600).</summary>
    public int DesignDpi { get; private set; } = 300;

    /// <summary>Target label width in mm.</summary>
    public int LabelWidthMm { get; private set; }

    /// <summary>Target label height in mm.</summary>
    public int LabelHeightMm { get; private set; }

    /// <summary>Whether this template version is the active/default version for its key.</summary>
    public bool IsActive { get; private set; }

    /// <summary>When the template was created.</summary>
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>When the template was last modified.</summary>
    public DateTime? ModifiedAtUtc { get; private set; }

    /// <summary>Who created the template.</summary>
    public string? CreatedBy { get; private set; }

    // ── EF Core ───────────────────────────────────────────────────────────
    private LabelTemplate() { }

    // ── Factory ───────────────────────────────────────────────────────────
    /// <summary>Creates a new active label template version.</summary>
    public static LabelTemplate Create(
        string templateKey,
        string version,
        string displayName,
        string zplBody,
        int designDpi,
        int labelWidthMm,
        int labelHeightMm,
        string? description = null,
        string? createdBy = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(zplBody);

        if (designDpi is not (203 or 300 or 600))
            throw new ArgumentOutOfRangeException(nameof(designDpi), "DPI must be 203, 300, or 600.");

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(labelWidthMm);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(labelHeightMm);

        return new LabelTemplate
        {
            Id = Guid.NewGuid(),
            TemplateKey = templateKey,
            Version = version,
            DisplayName = displayName,
            ZplBody = zplBody,
            DesignDpi = designDpi,
            LabelWidthMm = labelWidthMm,
            LabelHeightMm = labelHeightMm,
            Description = description,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = createdBy,
        };
    }

    /// <summary>Deactivates this template version.</summary>
    public void Deactivate()
    {
        IsActive = false;
        ModifiedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Activates this template version.</summary>
    public void Activate()
    {
        IsActive = true;
        ModifiedAtUtc = DateTime.UtcNow;
    }
}

