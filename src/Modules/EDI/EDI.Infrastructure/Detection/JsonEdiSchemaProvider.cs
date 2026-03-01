using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;
using EDI.Application.Abstractions;
using EDI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace EDI.Infrastructure.Detection;

/// <summary>
/// Loads EDI schemas from embedded JSON files.
/// Schemas are parsed once and cached in-process (immutable after startup).
/// </summary>
public sealed partial class JsonEdiSchemaProvider : IEdiSchemaProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
    };

    // Immutable after first access — assigned during construction.
    private readonly FrozenDictionary<EdiFileType, EdiSchema> _schemas;

    /// <summary>
    /// Creates a provider that loads schemas from the given base directory.
    /// Pass <c>null</c> to use the assembly's directory (default for embedded resources pattern).
    /// </summary>
    public JsonEdiSchemaProvider(
        ILogger<JsonEdiSchemaProvider> logger,
        string? schemaDirectory = null)
    {
        var dir = schemaDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Schemas");

        var schemas = new Dictionary<EdiFileType, EdiSchema>();

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
    }

    private static readonly IReadOnlyList<(EdiFileType, string)> SchemaFiles =
    [
        (EdiFileType.Forecast,      "forecast.v1.json"),
        (EdiFileType.PurchaseOrder, "purchaseorder.v1.json"),
    ];

    /// <inheritdoc/>
    public Task<EdiSchema?> GetSchemaAsync(EdiFileType fileType, CancellationToken ct)
    {
        _schemas.TryGetValue(fileType, out var schema);
        return Task.FromResult(schema);
    }

    // ── Deserialization DTO ───────────────────────────────────────────────────

    private sealed class EdiSchemaDto
    {
        public string SchemaKey     { get; init; } = string.Empty;
        public string SchemaVersion { get; init; } = "v1";

        [JsonPropertyName("requiredHeaders")]
        public List<string> RequiredHeaders { get; init; } = [];

        [JsonPropertyName("optionalHeaders")]
        public List<string> OptionalHeaders { get; init; } = [];

        [JsonPropertyName("headerAliases")]
        public Dictionary<string, string> HeaderAliases { get; init; } = [];

        public EdiSchema ToSchema(EdiFileType fileType) => new(
            SchemaKey:      SchemaKey,
            SchemaVersion:  SchemaVersion,
            FileType:       fileType,
            RequiredHeaders: RequiredHeaders.AsReadOnly(),
            OptionalHeaders: OptionalHeaders.AsReadOnly(),
            HeaderAliases:  HeaderAliases);
    }

    // ── LoggerMessage ─────────────────────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema file not found: {Path}")]
    private static partial void LogSchemaMissing(ILogger l, string path);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "EDI schema file is empty or invalid JSON: {Path}")]
    private static partial void LogSchemaEmpty(ILogger l, string path);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "EDI schema loaded: {FileType} v{SchemaVersion}")]
    private static partial void LogSchemaLoaded(ILogger l, string fileType, string schemaVersion);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "EDI schema load error: {Path} — {Detail}")]
    private static partial void LogSchemaLoadError(ILogger l, string path, string detail);
}

