namespace EDI.Domain.Enums;

/// <summary>
/// Identifies the business type of an EDI file from SAP MCP.
/// </summary>
public enum EdiFileType
{
    /// <summary>File type could not be determined from filename prefix or header.</summary>
    Unknown = 0,

    /// <summary>Forecast file — filename starts with 'F'.</summary>
    Forecast = 1,

    /// <summary>Purchase Order file — filename starts with 'P'.</summary>
    PurchaseOrder = 2,
}

