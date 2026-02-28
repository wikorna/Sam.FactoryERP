namespace FactoryERP.Contracts.Labeling;

public record PrintZplResult(
    Guid JobId,
    string Status,
    string Error,
    DateTime PrintedAtUtc
);
