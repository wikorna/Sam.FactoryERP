namespace EDI.Application.DTOs;

public record PurchaseOrderDto(
    PurchaseOrderHeaderDto Header,
    List<PurchaseOrderDetailDto> Details
);

public record PurchaseOrderHeaderDto(
    DateOnly? TransmissionDate,
    TimeOnly? TransmissionTime,
    string? PoFileName,
    int? RecordCount,
    string? SupplierCode,
    string? SupplierName,
    string? ContactName
);

public record PurchaseOrderDetailDto(
    string? PoStatus,
    string? PoNumber,
    string? PoItem,
    string? ItemNo,
    string? Description,
    string? BoiName,
    decimal? DueQty,
    string? Um,
    DateOnly? DueDate,
    decimal? UnitPrice,
    decimal? Amount,
    string? Currency,
    string? RawLine
);
