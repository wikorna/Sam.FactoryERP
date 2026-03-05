using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using EDI.Application.Abstractions;
using EDI.Domain.Enums;
using EDI.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Detection;

/// <summary>
/// Loads and caches EDI schemas from JSON files at startup.
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

    // FrozenDictionary is immutable and faster for read-heavy workloads.
    private readonly FrozenDictionary<string, EdiSchema> _schemas;

    public IReadOnlyCollection<string> RegisteredKeys => _schemas.Keys;
    public int Count => _schemas.Count;

    public EdiSchemaRegistry(ILogger<EdiSchemaRegistry> logger, string? schemaDirectory = null)
    {
        var dir = schemaDirectory ?? Path.Combine(AppContext.BaseDirectory, "Schemas");

        var schemas = new Dictionary<string, EdiSchema>(StringComparer.OrdinalIgnoreCase);

        foreach (var (fileType, fileName) in SchemaFiles)
        {
            var path = Path.Combine(dir, fileName);
            if (!File.Exists(path))
            {
                LogSchemaMissing(logger, path);
                continue;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var dto = JsonSerializer.Deserialize<EdiSchemaDto>(stream, JsonOptions);
                if (dto is null)
                {
                    LogSchemaEmpty(logger, path);
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

    private static readonly IReadOnlyList<(EdiFileType, string)> SchemaFiles =
    [
        (EdiFileType.Forecast,      "forecast.v1.json"),
        (EdiFileType.PurchaseOrder, "purchaseorder.v1.json"),
    ];

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

    // ── Deserialization DTO ───────────────────────────────────────────────────

    private sealed class EdiSchemaDto
    {
        public string SchemaKey { get; init; } = string.Empty;
        public string SchemaVersion { get; init; } = "v1";

        [JsonPropertyName("requiredHeaders")]
        public List<string> RequiredHeaders { get; init; } = [];

        [JsonPropertyName("optionalHeaders")]
        public List<string> OptionalHeaders { get; init; } = [];

        [JsonPropertyName("headerAliases")]
        public Dictionary<string, string> HeaderAliases { get; init; } = [];

        public EdiSchema ToSchema(EdiFileType fileType) => new(
            SchemaKey: SchemaKey,
            SchemaVersion: SchemaVersion,
            FileType: fileType,
            RequiredHeaders: RequiredHeaders.AsReadOnly(),
            OptionalHeaders: OptionalHeaders.AsReadOnly(),
            HeaderAliases: HeaderAliases);
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema file not found: {Path}")]
    private static partial void LogSchemaMissing(ILogger l, string path);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema file is empty or invalid JSON: {Path}")]
    private static partial void LogSchemaEmpty(ILogger l, string path);

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

