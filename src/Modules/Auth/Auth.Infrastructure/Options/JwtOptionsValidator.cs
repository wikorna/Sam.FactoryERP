using Microsoft.Extensions.Options;

namespace Auth.Infrastructure.Options;

/// <summary>Validates JWT options at startup to fail fast on misconfiguration.</summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Issuer))
            return ValidateOptionsResult.Fail("Jwt:Issuer must be provided.");

        if (string.IsNullOrWhiteSpace(options.Audience))
            return ValidateOptionsResult.Fail("Jwt:Audience must be provided.");

        if (options.AccessTokenLifetimeMinutes <= 0 || options.AccessTokenLifetimeMinutes > 60)
            return ValidateOptionsResult.Fail("Jwt:AccessTokenLifetimeMinutes must be between 1 and 60.");

        if (options.RefreshTokenLifetimeDays <= 0 || options.RefreshTokenLifetimeDays > 90)
            return ValidateOptionsResult.Fail("Jwt:RefreshTokenLifetimeDays must be between 1 and 90.");

        if (string.IsNullOrWhiteSpace(options.KeyDirectory))
            return ValidateOptionsResult.Fail("Jwt:KeyDirectory must be provided.");

        return ValidateOptionsResult.Success;
    }
}
