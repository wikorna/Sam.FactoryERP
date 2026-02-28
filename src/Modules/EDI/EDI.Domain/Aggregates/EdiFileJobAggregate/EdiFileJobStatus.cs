namespace EDI.Domain.Aggregates.EdiFileJobAggregate;

public enum EdiFileJobStatus
{
    Received = 1,
    Parsing = 2,
    Parsed = 3,
    Validating = 8,
    Validated = 9,
    Applying = 4,
    Applied = 5,
    Failed = 6,
    Archived = 7
}
