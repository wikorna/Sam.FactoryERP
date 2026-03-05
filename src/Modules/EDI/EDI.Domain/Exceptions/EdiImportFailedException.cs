namespace EDI.Domain.Exceptions;

/// <summary>
/// Thrown when the EDI import pipeline fails in a non-recoverable way.
/// </summary>
public sealed class EdiImportFailedException : Exception
{
    public Guid StagingFileId { get; }
    public string ErrorCode { get; }

    public EdiImportFailedException(Guid stagingFileId, string errorCode, string message)
        : base(message)
    {
        StagingFileId = stagingFileId;
        ErrorCode = errorCode;
    }

    public EdiImportFailedException(Guid stagingFileId, string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        StagingFileId = stagingFileId;
        ErrorCode = errorCode;
    }
}

