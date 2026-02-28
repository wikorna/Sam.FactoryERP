namespace Auth.Domain.Enums;

/// <summary>
/// Application-level roles as string constants for Identity compatibility.
/// </summary>
public static class AuthRole
{
    public const string Operator = "Operator";
    public const string Supervisor = "Supervisor";
    public const string Manager = "Manager";
    public const string Admin = "Admin";
    public const string Auditor = "Auditor";
}
