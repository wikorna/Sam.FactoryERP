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
public sealed class EdiSchemaRegistry : IEdiSchemaRegistry
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

    private static readonly Action<ILogger, string, Exception?> _logSchemaDirectoryMissing =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2100, nameof(LogSchemaDirectoryMissing)),
            "EDI schema directory not found: {Path}");

    private static readonly Action<ILogger, string, Exception?> _logSchemaEmpty =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2101, nameof(LogSchemaEmpty)),
            "EDI schema file is empty or invalid JSON: {Path}");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaValidationWarning =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2102, nameof(LogSchemaValidationWarning)),
            "EDI schema validation warning for '{SchemaKey}': {Issue}");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaUnknownFileType =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2103, nameof(LogSchemaUnknownFileType)),
            "EDI schema file '{Path}' has unrecognized file type key: '{SchemaKey}'");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaLoaded =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2104, nameof(LogSchemaLoaded)),
            "EDI schema loaded: {SchemaKey} v{SchemaVersion}");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaLoadError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2105, nameof(LogSchemaLoadError)),
            "EDI schema load error: {Path} — {Detail}");

    private static readonly Action<ILogger, int, string, Exception?> _logRegistryReady =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(2106, nameof(LogRegistryReady)),
            "EDI Schema Registry ready: {Count} schemas loaded. Keys: {Keys}");

    private static void LogSchemaDirectoryMissing(ILogger logger, string path) =>
        _logSchemaDirectoryMissing(logger, path, null);

    private static void LogSchemaEmpty(ILogger logger, string path) =>
        _logSchemaEmpty(logger, path, null);

    private static void LogSchemaValidationWarning(ILogger logger, string schemaKey, string issue) =>
        _logSchemaValidationWarning(logger, schemaKey, issue, null);

    private static void LogSchemaUnknownFileType(ILogger logger, string path, string schemaKey) =>
        _logSchemaUnknownFileType(logger, path, schemaKey, null);

    private static void LogSchemaLoaded(ILogger logger, string schemaKey, string schemaVersion) =>
        _logSchemaLoaded(logger, schemaKey, schemaVersion, null);

    private static void LogSchemaLoadError(ILogger logger, string path, string detail) =>
        _logSchemaLoadError(logger, path, detail, null);

    private static void LogRegistryReady(ILogger logger, int count, string keys) =>
        _logRegistryReady(logger, count, keys, null);
}

