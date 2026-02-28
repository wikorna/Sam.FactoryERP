namespace FactoryERP.Infrastructure.Email.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// Set to false to disable email sending entirely (e.g. local dev without SMTP).
    /// When false, validation is skipped and all send operations are no-ops.
    /// </summary>
    public bool Enabled { get; init; } = true;

    public string Provider { get; init; } = "Smtp"; // Smtp | SendGrid | Ses ...
    public string FromName { get; init; } = "Notification";
    public string FromEmail { get; init; } = string.Empty;

    public SmtpOptions Smtp { get; init; } = new();

    public sealed class SmtpOptions
    {
        public string Host { get; init; } = string.Empty;
        public int Port { get; init; } = 587;

        /// <summary>
        /// If true: use STARTTLS upgrade (typically port 587).
        /// </summary>
        public bool UseStartTls { get; init; } = true;

        /// <summary>
        /// If true: use SSL/TLS on connect (typically port 465).
        /// </summary>
        public bool UseSslOnConnect { get; init; }

        public string? Username { get; init; }
        public string? Password { get; init; }

        // Hardening knobs
        public int TimeoutSeconds { get; init; } = 30;

        /// <summary>
        /// Allow invalid TLS cert (DEV only). Keep false in UAT/PROD.
        /// </summary>
        public bool AllowInvalidCertificate { get; init; }
    }
}
