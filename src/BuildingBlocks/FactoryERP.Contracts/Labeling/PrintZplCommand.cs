namespace FactoryERP.Contracts.Labeling;

public record PrintZplCommand(
    Guid JobId,
    string TenantId, /* Or CompanyId based on context */
    Guid PrinterId,
    string Zpl,
    int Copies,
    string RequestedBy,
    Guid CorrelationId,
    DateTime TimestampUtc
);
