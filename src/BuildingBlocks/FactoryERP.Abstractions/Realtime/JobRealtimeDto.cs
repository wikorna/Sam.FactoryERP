namespace FactoryERP.Abstractions.Realtime;

public sealed record JobProgressDto(
    string JobId,
    int Percent,
    int Processed,
    int Total,
    string Status,
    string Message,
    DateTime TimestampUtc);

public sealed record JobCompletedDto(
    string JobId,
    string ResultId,
    string Message,
    DateTime TimestampUtc);

public sealed record JobFailedDto(
    string JobId,
    string ErrorMessage,
    DateTime TimestampUtc);
