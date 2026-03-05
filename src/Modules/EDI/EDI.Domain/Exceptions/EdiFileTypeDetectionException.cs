namespace EDI.Domain.Exceptions;

/// <summary>
/// Thrown when the EDI file type cannot be determined from the filename or content.
/// </summary>
public sealed class EdiFileTypeDetectionException : Exception
{
    public string FileName { get; }

    public EdiFileTypeDetectionException(string fileName)
        : base($"Unable to detect EDI file type for file: '{fileName}'.")
    {
        FileName = fileName;
    }

    public EdiFileTypeDetectionException(string fileName, string message)
        : base(message)
    {
        FileName = fileName;
    }
}

