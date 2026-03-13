namespace Labeling.Application.Models;

public record ProductLabelData(
    string DocNo,
    string PageText,
    string ProductName,
    string PartNo,
    decimal Quantity,
    string PoNumber,
    string PoItem,
    string? DueDate,
    string? Description,
    string? RunNo,
    string? Store,
    string QrPayload,
    string? Remarks
);

