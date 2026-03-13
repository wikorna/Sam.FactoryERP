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
public sealed partial class JsonEdiSchemaProvider : IEdiSchemaProvider
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
        Message = "EDI schema loaded: {FileType} v{SchemaVersion}")]
    private static partial void LogSchemaLoaded(ILogger l, string fileType, string schemaVersion);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "EDI schema load error: {Path} — {Detail}")]
    private static partial void LogSchemaLoadError(ILogger l, string path, string detail);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI Schema Provider ready: {Count} schemas loaded. Keys: {Keys}")]
    private static partial void LogProviderReady(ILogger l, int count, string keys);
}

