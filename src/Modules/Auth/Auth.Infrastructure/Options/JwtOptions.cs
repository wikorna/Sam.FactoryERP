namespace Auth.Infrastructure.Options;

/// <summary>Strongly-typed JWT configuration bound from "Jwt" section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "https://factoryerp.local";
    public string Audience { get; set; } = "factoryerp-api";
    public int AccessTokenLifetimeMinutes { get; set; } = 15;
    public int RefreshTokenLifetimeDays { get; set; } = 7;
    public int ClockSkewSeconds { get; set; } = 30;

    /// <summary>Directory containing RSA PEM key files (e.g., "Keys/").</summary>
    public string KeyDirectory { get; set; } = "Keys";
}
