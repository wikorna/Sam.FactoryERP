namespace Labeling.Domain.Entities;

/// <summary>
/// Supported printer transport protocols.
/// </summary>
public enum PrinterProtocol
{
    /// <summary>Raw TCP port 9100 (ZPL direct).</summary>
    Raw9100 = 0,

    /// <summary>LPR/LPD protocol (port 515).</summary>
    Lpr = 1
}

