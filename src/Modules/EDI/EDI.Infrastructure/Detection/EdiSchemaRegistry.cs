using System.Collections.Frozen;
using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Domain.Enums;
using EDI.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Detection;

/// <summary>
/// Loads and caches EDI schemas from JSON files at startup.
/// Auto-discovers all <c>*.json</c> files in the schema directory.
/// Registry uses <see cref="StringComparer.OrdinalIgnoreCase"/> for all lookups.
/// Schemas are immutable after construction — zero allocation on hot path.
/// </summary>
public sealed partial class EdiSchemaRegistry : IEdiSchemaRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly FrozenDictionary<string, EdiSchema> _schemas;

    public IReadOnlyCollection<string> RegisteredKeys => _schemas.Keys;
    public int Count => _schemas.Count;

    public EdiSchemaRegistry(ILogger<EdiSchemaRegistry> logger, string? schemaDirectory = null)
    {
        var dir = schemaDirectory ?? Path.Combine(AppContext.BaseDirectory, "Schemas");

        var schemas = new Dictionary<string, EdiSchema>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(dir))
        {
            LogSchemaDirectoryMissing(logger, dir);
            _schemas = schemas.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            return;
        }

        foreach (var path in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var dto = JsonSerializer.Deserialize<EdiSchemaDto>(stream, JsonOptions);
                if (dto is null)
                {
                    LogSchemaEmpty(logger, path);
                    continue;
                }

                // Validate schema invariants at load time
                var validationIssues = dto.Validate();
                foreach (var issue in validationIssues)
                    LogSchemaValidationWarning(logger, dto.SchemaKey, issue);

                if (!Enum.TryParse<EdiFileType>((string?)dto.SchemaKey, ignoreCase: true, out var fileType)
                    || fileType == EdiFileType.Unknown)
                {
                    LogSchemaUnknownFileType(logger, path, dto.SchemaKey);
                    continue;
                }

                var schema = dto.ToSchema(fileType);
                schemas[schema.SchemaKey] = schema;
                LogSchemaLoaded(logger, schema.SchemaKey, dto.SchemaVersion);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                LogSchemaLoadError(logger, path, ex.Message);
            }
        }

        _schemas = schemas.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

        var registeredKeys = string.Join(", ", _schemas.Keys);
        LogRegistryReady(logger, _schemas.Count, registeredKeys);
    }

    /// <inheritdoc/>
    public EdiSchema GetSchema(string schemaKey)
    {
        if (_schemas.TryGetValue(schemaKey, out var schema))
            return schema;

        throw new EdiSchemaNotFoundException(schemaKey);
    }

    /// <inheritdoc/>
    public bool TryGetSchema(string schemaKey, out EdiSchema? schema)
    {
        var found = _schemas.TryGetValue(schemaKey, out var s);
        schema = s;
        return found;
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema directory not found: {Path}")]
    private static partial void LogSchemaDirectoryMissing(ILogger l, string path);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema file is empty or invalid JSON: {Path}")]
    private static partial void LogSchemaEmpty(ILogger l, string path);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema validation warning for '{SchemaKey}': {Issue}")]
    private static partial void LogSchemaValidationWarning(ILogger l, string schemaKey, string issue);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema file '{Path}' has unrecognized file type key: '{SchemaKey}'")]
    private static partial void LogSchemaUnknownFileType(ILogger l, string path, string schemaKey);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI schema loaded: {SchemaKey} v{SchemaVersion}")]
    private static partial void LogSchemaLoaded(ILogger l, string schemaKey, string schemaVersion);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "EDI schema load error: {Path} — {Detail}")]
    private static partial void LogSchemaLoadError(ILogger l, string path, string detail);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI Schema Registry ready: {Count} schemas loaded. Keys: {Keys}")]
    private static partial void LogRegistryReady(ILogger l, int count, string keys);
}

