namespace EDI.Domain.Exceptions;

/// <summary>
/// Thrown when an EDI schema cannot be found for the requested file type or key.
/// </summary>
public sealed class EdiSchemaNotFoundException : Exception
{
    public string SchemaKey { get; }

    public EdiSchemaNotFoundException(string schemaKey)
        : base($"EDI schema not found: '{schemaKey}'.")
    {
        SchemaKey = schemaKey;
    }

    public EdiSchemaNotFoundException(string schemaKey, string message)
        : base(message)
    {
        SchemaKey = schemaKey;
    }
}

