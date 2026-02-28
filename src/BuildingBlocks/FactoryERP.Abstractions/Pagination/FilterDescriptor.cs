namespace FactoryERP.Abstractions.Pagination;

/// <summary>
/// Filter operator matching SAP Fiori filter bar operators.
/// Used in dynamic filter expressions with allowlist protection.
/// </summary>
public enum FilterOperator
{
    /// <summary>Equals</summary>
    Eq,
    /// <summary>Not equals</summary>
    Ne,
    /// <summary>Less than</summary>
    Lt,
    /// <summary>Less than or equal</summary>
    Le,
    /// <summary>Greater than</summary>
    Gt,
    /// <summary>Greater than or equal</summary>
    Ge,
    /// <summary>String contains</summary>
    Contains,
    /// <summary>String starts with</summary>
    StartsWith,
    /// <summary>Value is in a set</summary>
    In
}

/// <summary>A single filter condition from the Fiori FilterBar.</summary>
public sealed record FilterDescriptor(string Field, FilterOperator Operator, string Value);

/// <summary>Sort direction.</summary>
public enum SortDirection { Asc, Desc }

/// <summary>A single sort expression.</summary>
public sealed record SortDescriptor(string Field, SortDirection Direction = SortDirection.Asc);
