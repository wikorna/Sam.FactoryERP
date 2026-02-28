namespace FactoryERP.Abstractions.Cqrs;

/// <summary>Machine-readable error with code for UI mapping.</summary>
public sealed record AppError(string Code, string Message)
{
    // ── Common error codes ──
    public static readonly AppError None = new(string.Empty, string.Empty);
    public static readonly AppError NullValue = new("Error.NullValue", "A required value was null.");

    public static AppError Validation(string message) => new("Validation", message);
    public static AppError NotFound(string entity, object id) => new("NotFound", $"{entity} with id '{id}' was not found.");
    public static AppError Conflict(string message) => new("Conflict", message);
    public static AppError Unauthorized(string message = "Unauthorized") => new("Unauthorized", message);
    public static AppError Forbidden(string message = "Forbidden") => new("Forbidden", message);
}
