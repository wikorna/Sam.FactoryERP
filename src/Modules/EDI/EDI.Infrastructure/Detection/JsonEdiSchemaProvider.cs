using System.Collections.Frozen;
using System.Text.Json;
using EDI.Application.Abstractions;
using EDI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Detection;

/// <summary>
/// Loads EDI schemas from JSON files in the schema directory.
/// Schemas are auto-discovered (all <c>*.json</c> files), validated at startup,
/// and cached immutably in a <see cref="FrozenDictionary{TKey,TValue}"/>.
/// </summary>
public sealed class JsonEdiSchemaProvider : IEdiSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
    };

    private readonly FrozenDictionary<EdiFileType, EdiSchema> _schemas;

    /// <summary>
    /// Creates a provider that auto-discovers and loads all <c>*.json</c> schema files
    /// from the given directory. Pass <c>null</c> to use the default <c>Schemas</c> folder.
    /// </summary>
    public JsonEdiSchemaProvider(
        ILogger<JsonEdiSchemaProvider> logger,
        string? schemaDirectory = null)
    {
        var dir = schemaDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Schemas");

        var schemas = new Dictionary<EdiFileType, EdiSchema>();

        if (!Directory.Exists(dir))
        {
            LogSchemaDirectoryMissing(logger, dir);
            _schemas = schemas.ToFrozenDictionary();
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
                    // Try mapping from the "fileType" field in JSON if schemaKey doesn't match enum
                    LogSchemaUnknownFileType(logger, path, dto.SchemaKey);
                    continue;
                }

                schemas[fileType] = dto.ToSchema(fileType);
                var fileTypeName = fileType.ToString();
                LogSchemaLoaded(logger, fileTypeName, dto.SchemaVersion);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                LogSchemaLoadError(logger, path, ex.Message);
            }
        }

        _schemas = schemas.ToFrozenDictionary();

        var loadedKeys = string.Join(", ", _schemas.Keys);
        LogProviderReady(logger, _schemas.Count, loadedKeys);
    }

    /// <inheritdoc/>
    public Task<EdiSchema?> GetSchemaAsync(EdiFileType fileType, CancellationToken ct)
    {
        _schemas.TryGetValue(fileType, out var schema);
        return Task.FromResult(schema);
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────────

    private static readonly Action<ILogger, string, Exception?> _logSchemaDirectoryMissing =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2200, nameof(LogSchemaDirectoryMissing)),
            "EDI schema directory not found: {Path}");

    private static readonly Action<ILogger, string, Exception?> _logSchemaEmpty =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2201, nameof(LogSchemaEmpty)),
            "EDI schema file is empty or invalid JSON: {Path}");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaValidationWarning =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2202, nameof(LogSchemaValidationWarning)),
            "EDI schema validation warning for '{SchemaKey}': {Issue}");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaUnknownFileType =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2203, nameof(LogSchemaUnknownFileType)),
            "EDI schema file '{Path}' has unrecognized file type key: '{SchemaKey}'");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaLoaded =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2204, nameof(LogSchemaLoaded)),
            "EDI schema loaded: {FileType} v{SchemaVersion}");

    private static readonly Action<ILogger, string, string, Exception?> _logSchemaLoadError =
        LoggerMessage.Define<string, string>(
            LogLevel.Error,
            new EventId(2205, nameof(LogSchemaLoadError)),
            "EDI schema load error: {Path} — {Detail}");

    private static readonly Action<ILogger, int, string, Exception?> _logProviderReady =
        LoggerMessage.Define<int, string>(
            LogLevel.Information,
            new EventId(2206, nameof(LogProviderReady)),
            "EDI Schema Provider ready: {Count} schemas loaded. Keys: {Keys}");

    private static void LogSchemaDirectoryMissing(ILogger logger, string path) =>
        _logSchemaDirectoryMissing(logger, path, null);

    private static void LogSchemaEmpty(ILogger logger, string path) =>
        _logSchemaEmpty(logger, path, null);

    private static void LogSchemaValidationWarning(ILogger logger, string schemaKey, string issue) =>
        _logSchemaValidationWarning(logger, schemaKey, issue, null);

    private static void LogSchemaUnknownFileType(ILogger logger, string path, string schemaKey) =>
        _logSchemaUnknownFileType(logger, path, schemaKey, null);

    private static void LogSchemaLoaded(ILogger logger, string fileType, string schemaVersion) =>
        _logSchemaLoaded(logger, fileType, schemaVersion, null);

    private static void LogSchemaLoadError(ILogger logger, string path, string detail) =>
        _logSchemaLoadError(logger, path, detail, null);

    private static void LogProviderReady(ILogger logger, int count, string keys) =>
        _logProviderReady(logger, count, keys, null);
}

