using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace FactoryERP.Infrastructure.Email.Options;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        // When email is disabled, skip all validation — no SMTP config required.
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        var failures = new List<string>();

        if (!string.Equals(options.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add($"Email: Provider must be 'Smtp'.");
        }

        if (string.IsNullOrWhiteSpace(options.FromEmail))
        {
            failures.Add("Email:FromEmail is required.");
        }
        else if (!IsValidEmail(options.FromEmail))
        {
            failures.Add("Email:FromEmail is not a valid email address.");
        }

        if (options.Smtp is null)
        {
            failures.Add("Email:Smtp section is required.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(options.Smtp.Host))
                failures.Add("Email:Smtp:Host is required.");

            if (options.Smtp.Port is < 1 or > 65535)
                failures.Add("Email:Smtp:Port must be between 1 and 65535.");

            if (options.Smtp.TimeoutSeconds is < 1 or > 300)
                failures.Add("Email:Smtp:TimeoutSeconds must be between 1 and 300.");

            // TLS mode rules
            if (options.Smtp.UseStartTls && options.Smtp.UseSslOnConnect)
                failures.Add("Email:Smtp:UseStartTls and UseSslOnConnect cannot both be true.");

            // Auth rule: username -> password required
            if (!string.IsNullOrWhiteSpace(options.Smtp.Username) && string.IsNullOrWhiteSpace(options.Smtp.Password))
                failures.Add("Email:Smtp:Password is required when Username is provided.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
