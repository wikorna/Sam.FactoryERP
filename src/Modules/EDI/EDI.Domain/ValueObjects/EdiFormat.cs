namespace EDI.Domain.ValueObjects;

public sealed record EdiFormat(string Value)
{
    public static readonly EdiFormat Csv = new("csv");
    public static readonly EdiFormat Pipe = new("pipe");
    public static readonly EdiFormat FixedWidth = new("fixed-width");

    public override string ToString() => Value;
}
