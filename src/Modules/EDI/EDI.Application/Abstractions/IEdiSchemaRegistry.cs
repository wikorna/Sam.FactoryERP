using EDI.Domain.Exceptions;

namespace EDI.Application.Abstractions;

/// <summary>
/// Registry of EDI schemas loaded at startup, keyed case-insensitively.
/// Provides O(1) schema lookup by key or version.
/// Throws <see cref="EdiSchemaNotFoundException"/> when a schema key is not registered.
/// </summary>
public interface IEdiSchemaRegistry
{
    /// <summary>
    /// Returns the schema for the given key (case-insensitive).
    /// Throws <see cref="EdiSchemaNotFoundException"/> if not found.
    /// </summary>
    EdiSchema GetSchema(string schemaKey);

    /// <summary>
    /// Attempts to get the schema. Returns false if not found.
    /// </summary>
    bool TryGetSchema(string schemaKey, out EdiSchema? schema);

    /// <summary>All registered schema keys.</summary>
    IReadOnlyCollection<string> RegisteredKeys { get; }

    /// <summary>Number of loaded schemas.</summary>
    int Count { get; }
}

