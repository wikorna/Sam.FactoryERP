namespace EDI.Domain.ValueObjects;

/// <summary>
/// Represents the result of file type detection, including a confidence score.
/// </summary>
/// <param name="FileTypeCode">Canonical file type code, e.g. "Forecast", "PurchaseOrder". Never lowercase.</param>
/// <param name="ConfidenceScore">Confidence of the detection: 1.0 = certain, 0.0 = no match.</param>
/// <param name="DetectionMethod">How the type was detected: "FileNamePrefix", "HeaderMarker", etc.</param>
public sealed record DetectionResult(
    string FileTypeCode,
    double ConfidenceScore,
    string DetectionMethod)
{
    /// <summary>Returns true when confidence is considered sufficient for processing (>= 0.8).</summary>
    public bool IsConfident => ConfidenceScore >= 0.8;

    /// <summary>Creates a high-confidence result from filename prefix matching.</summary>
    public static DetectionResult FromFilenamePrefix(string fileTypeCode) =>
        new(fileTypeCode, 1.0, "FileNamePrefix");

    /// <summary>Creates a medium-confidence result from a header marker (row segment marker).</summary>
    public static DetectionResult FromHeaderMarker(string fileTypeCode) =>
        new(fileTypeCode, 0.8, "HeaderMarker");

    /// <summary>Represents no detection.</summary>
    public static DetectionResult Unknown =>
        new("Unknown", 0.0, "None");
}

