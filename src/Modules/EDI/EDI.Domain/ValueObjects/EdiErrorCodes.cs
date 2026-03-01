namespace EDI.Domain.ValueObjects;

/// <summary>
/// Canonical error codes returned by the EDI detection and validation pipeline.
/// </summary>
public static class EdiErrorCodes
{
    public const string InvalidExtension   = "EDI_INVALID_EXTENSION";
    public const string InvalidFilename    = "EDI_INVALID_FILENAME";
    public const string HeaderMismatch     = "EDI_HEADER_MISMATCH";
    public const string InvalidEncoding    = "EDI_INVALID_ENCODING";
    public const string FileTooLarge       = "EDI_FILE_TOO_LARGE";
    public const string EmptyFile          = "EDI_EMPTY_FILE";
    public const string UnknownFileType    = "EDI_UNKNOWN_FILE_TYPE";
    public const string CsvParseError      = "EDI_CSV_PARSE_ERROR";
}

