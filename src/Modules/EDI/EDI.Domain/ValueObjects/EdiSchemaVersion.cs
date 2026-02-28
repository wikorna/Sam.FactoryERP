namespace EDI.Domain.ValueObjects;

public sealed record EdiSchemaVersion
{
    public static readonly EdiSchemaVersion V1 = Create("1.0");
    public static readonly EdiSchemaVersion V2 = Create("2.0");

    public string Value { get; }

    private EdiSchemaVersion(string value)
    {
        Value = value;
    }

    public override string ToString() => Value;

    public static EdiSchemaVersion Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("SchemaVersion cannot be empty.", nameof(value));
        }

        string v = value.Trim();

        if (v.Length > 32)
        {
            throw new ArgumentException("SchemaVersion too long.", nameof(value));
        }

        if (v[0] < '0' || v[0] > '9')
        {
            throw new ArgumentException($"Invalid SchemaVersion: '{value}'.", nameof(value));
        }

        foreach (char ch in v)
        {
            bool ok = (ch >= '0' && ch <= '9') || ch == '.';
            if (!ok)
            {
                throw new ArgumentException($"Invalid SchemaVersion: '{value}'.", nameof(value));
            }
        }

        return new EdiSchemaVersion(v);
    }
}
