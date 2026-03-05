namespace EDI.Domain.Enums;

public enum EdiStagingStatus
{
    Staged = 1,
    Validating = 2,
    Validated = 3,
    Queued = 4,
    Processing = 5,
    Completed = 6,
    Failed = 7,
    Cancelled = 8
}
