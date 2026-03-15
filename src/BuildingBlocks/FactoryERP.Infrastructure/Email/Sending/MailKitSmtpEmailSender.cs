using System.Net.Sockets;
using FactoryERP.Abstractions.Email;
using FactoryERP.Infrastructure.Email.Options;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Utils;
using Polly;

namespace FactoryERP.Infrastructure.Email.Sending;

public sealed class MailKitSmtpEmailSender(
    IOptions<EmailOptions> options,
    ILogger<MailKitSmtpEmailSender> logger) : IEmailSender//, IEmailSenderV2
{
    public async Task<EmailSendResult> SendAsync(EmailSendRequest request, CancellationToken ct = default)
    {
        var opt = options.Value;

        // Email sending is disabled — return success no-op (dev/test without SMTP config).
        if (!opt.Enabled)
        {
            LocalLog.SmtpDisabled(logger);
            return new EmailSendResult(true, ProviderMessageId: "disabled");
        }

        var configError = ValidateConfig(opt);
        if (configError is not null)
            return configError;

        var requestError = TryBuildMessage(opt, request, out var message, out var errorResult);
        if (!requestError)
            return errorResult!;

        var toDisplay = FormatRecipients(request.To);

        using var client = new SmtpClient();

        client.CheckCertificateRevocation = true;

        if (opt.Smtp.AllowInvalidCertificate)
        {
            LocalLog.SmtpCertificateValidationBypassed(logger);
            client.ServerCertificateValidationCallback = BypassCertificateValidation;
        }

        client.Timeout = ToTimeoutMilliseconds(opt.Smtp.TimeoutSeconds);

        try
        {
            var socketOptions = ResolveSocketOptions(opt);
            var retryPolicy = CreateRetryPolicy(client);

            var providerMessageId = await retryPolicy.ExecuteAsync(async innerCt =>
            {
                await EnsureSmtpConnected(client, opt, socketOptions, innerCt).ConfigureAwait(false);

                var serverResponse = await client.SendAsync(message, innerCt).ConfigureAwait(false);

                LocalLog.SmtpSendMailTo(logger, toDisplay, serverResponse);
                return serverResponse;
            }, ct);

            LocalLog.SmtpSendMailTo(logger, toDisplay, NormalizeProviderId(providerMessageId));

            return new EmailSendResult(true, ProviderMessageId: NormalizeProviderId(providerMessageId));
        }
        catch (OperationCanceledException)
        {
            LocalLog.SmtpSendCanceled(logger, toDisplay);
            throw;
        }
        catch (Exception ex)
        {
            var (code, msg) = MapException(ex);
            LocalLog.SmtpSendFailed(logger, toDisplay, ex);

            return new EmailSendResult(false, ErrorCode: code, ErrorMessage: msg);
        }
        finally
        {
            await DisconnectSmtpClient(client).ConfigureAwait(false);
        }
    }

    private Polly.Retry.AsyncRetryPolicy<string> CreateRetryPolicy(SmtpClient client)
    {
        return Policy<string>
            .Handle<Exception>(ex => IsTransientSmtp(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetryAsync: async (del, delay, retryCount, _) =>
                {
                    LocalLog.SmtpRetry(
                        logger,
                        retryCount,
                        delay.TotalMilliseconds,
                        del.Exception?.Message ?? "Unknown error",
                        del.Exception ?? new InvalidOperationException("Unknown retry error"));

                    await ResetSmtpConnection(client).ConfigureAwait(false);
                });
    }

    private static async Task EnsureSmtpConnected(
        SmtpClient client,
        EmailOptions opt,
        SecureSocketOptions socketOptions,
        CancellationToken ct)
    {
        if (!client.IsConnected)
        {
            await client.ConnectAsync(opt.Smtp.Host, opt.Smtp.Port, socketOptions, ct).ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(opt.Smtp.Username))
        {
            client.AuthenticationMechanisms.Remove("XOAUTH2");

            if (!client.IsAuthenticated)
            {
                await client.AuthenticateAsync(opt.Smtp.Username, opt.Smtp.Password, ct).ConfigureAwait(false);
            }
        }
    }

    private static async Task ResetSmtpConnection(SmtpClient client)
    {
        if (client.IsConnected)
        {
            try
            {
                await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Ignore reset errors; connection will be re-established on next retry
            }
        }
    }

    private static async Task DisconnectSmtpClient(SmtpClient client)
    {
        if (client.IsConnected)
        {
            try
            {
                await client.DisconnectAsync(true, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Ignore disconnect errors to avoid hiding original exception
            }
        }
    }

    /// <summary>
    /// Certificate validation callback for dev/test environments where self-signed certs are used.
    /// Only wired when <c>Email:Smtp:AllowInvalidCertificate=true</c>.
    /// Accepts self-signed and untrusted-root errors but still rejects name mismatch.
    /// </summary>
    private static bool BypassCertificateValidation(
        object sender,
        System.Security.Cryptography.X509Certificates.X509Certificate? certificate,
        System.Security.Cryptography.X509Certificates.X509Chain? chain,
        System.Net.Security.SslPolicyErrors sslPolicyErrors)
    {
        // No errors — certificate is fully valid.
        if (sslPolicyErrors == System.Net.Security.SslPolicyErrors.None)
            return true;

        // Reject remote certificate name mismatch — even in dev this indicates a wrong host.
        if (sslPolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch))
            return false;

        // Accept chain errors (self-signed, untrusted root) — expected in dev/test.
        if (sslPolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors))
            return true;

        // Accept missing remote certificate only when no certificate is required in dev SMTP.
        if (sslPolicyErrors.HasFlag(System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable))
            return certificate is not null;

        return false;
    }

    private static EmailSendResult? ValidateConfig(EmailOptions opt)
    {
        if (string.IsNullOrWhiteSpace(opt.FromEmail))
            return new EmailSendResult(false, ErrorCode: "CONFIG_FROM_EMPTY", ErrorMessage: "Email:FromEmail is not configured.");

        if (string.IsNullOrWhiteSpace(opt.Smtp.Host))
            return new EmailSendResult(false, ErrorCode: "CONFIG_SMTP_HOST_EMPTY", ErrorMessage: "Email:Smtp:Host is not configured.");

        if (opt.Smtp.Port is < 1 or > 65535)
            return new EmailSendResult(false, ErrorCode: "CONFIG_SMTP_PORT_INVALID", ErrorMessage: "Email:Smtp:Port must be between 1 and 65535.");

        return null;
    }

    private static bool TryBuildMessage(
        EmailOptions opt,
        EmailSendRequest request,
        out MimeMessage message,
        out EmailSendResult? error)
    {
        message = default!;
        error = null;

        if (request.To is null || request.To.Count == 0)
        {
            error = new EmailSendResult(false, ErrorCode: "REQ_TO_EMPTY", ErrorMessage: "Email recipient (To) is required.");
            return false;
        }

        message = new MimeMessage
        {
            Subject = request.Subject,
            Date = DateTimeOffset.UtcNow,
            MessageId = MimeUtils.GenerateMessageId()
        };

        message.From.Add(new MailboxAddress(opt.FromName, opt.FromEmail));

        // To recipients
        foreach (var to in request.To)
        {
            try { message.To.Add(ToMailboxAddress(to)); }
            catch (FormatException fe)
            {
                error = new EmailSendResult(false, ErrorCode: "REQ_TO_INVALID", ErrorMessage: $"Invalid To address '{to.Email}': {fe.Message}");
                return false;
            }
        }

        // Cc recipients
        if (request.Cc is { Count: > 0 })
        {
            foreach (var cc in request.Cc)
            {
                try { message.Cc.Add(ToMailboxAddress(cc)); }
                catch (FormatException fe)
                {
                    error = new EmailSendResult(false, ErrorCode: "REQ_CC_INVALID", ErrorMessage: $"Invalid Cc address '{cc.Email}': {fe.Message}");
                    return false;
                }
            }
        }

        // Bcc recipients
        if (request.Bcc is { Count: > 0 })
        {
            foreach (var bcc in request.Bcc)
            {
                try { message.Bcc.Add(ToMailboxAddress(bcc)); }
                catch (FormatException fe)
                {
                    error = new EmailSendResult(false, ErrorCode: "REQ_BCC_INVALID", ErrorMessage: $"Invalid Bcc address '{bcc.Email}': {fe.Message}");
                    return false;
                }
            }
        }

        // Reply-To
        if (!string.IsNullOrWhiteSpace(request.ReplyTo))
        {
            try { message.ReplyTo.Add(MailboxAddress.Parse(request.ReplyTo)); }
            catch (FormatException fe)
            {
                error = new EmailSendResult(false, ErrorCode: "REQ_REPLY_TO_INVALID", ErrorMessage: $"Invalid ReplyTo address: {fe.Message}");
                return false;
            }
        }

        // Correlation ID header
        if (request.CorrelationId.HasValue)
        {
            var correlationIdValue = request.CorrelationId.Value.ToString();
            if (!string.IsNullOrWhiteSpace(correlationIdValue))
                message.Headers.Add("X-Correlation-Id", correlationIdValue);
        }

        var body = new BodyBuilder
        {
            HtmlBody = request.HtmlBody,
            TextBody = request.PlainTextBody ?? StripHtmlToText(request.HtmlBody)
        };

        // Attachments
        if (request.Attachments?.Count > 0)
        {
            foreach (var attachment in request.Attachments)
            {
                body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        message.Body = body.ToMessageBody();
        return true;
    }

    private static MailboxAddress ToMailboxAddress(EmailAddress addr)
        => new(addr.DisplayName, addr.Email);

    private static string FormatRecipients(IReadOnlyCollection<EmailAddress> recipients)
        => string.Join(", ", recipients.Select(r => r.Email));

    private static int ToTimeoutMilliseconds(int timeoutSeconds)
    {
        // Guardrails: Math.Clamp basically
        if (timeoutSeconds <= 0) timeoutSeconds = 30;
        if (timeoutSeconds > 300) timeoutSeconds = 300;

        return (int)TimeSpan.FromSeconds(timeoutSeconds).TotalMilliseconds;
    }

    private static SecureSocketOptions ResolveSocketOptions(EmailOptions opt)
        => opt.Smtp.UseSslOnConnect
            ? SecureSocketOptions.SslOnConnect
            : opt.Smtp.UseStartTls
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.Auto;

    private static string? NormalizeProviderId(string? serverResponse)
        => string.IsNullOrWhiteSpace(serverResponse) ? null : serverResponse.Trim();

    private static (string Code, string Message) MapException(Exception ex) =>
        ex switch
        {
            MailKit.Security.AuthenticationException => ("SMTP_AUTH_FAILED", ex.Message),

            SslHandshakeException => ("SMTP_TLS_HANDSHAKE_FAILED", ex.Message),

            SmtpCommandException sce => ("SMTP_COMMAND_FAILED",
                $"SMTP command failed (StatusCode={(int)sce.StatusCode}): {sce.Message}"),

            SmtpProtocolException => ("SMTP_PROTOCOL_ERROR", ex.Message),

            SocketException se => ("SMTP_CONNECT_FAILED", se.Message),

            _ => ("SMTP_SEND_FAILED", ex.Message)
        };

    private static string StripHtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        // Simple fallback; if you want perfect conversion, use a real HTML-to-text lib.
        return html
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase);
    }
    private static bool IsTransientSmtp(Exception ex)
    {
        if (ex is OperationCanceledException) return false;

        // common transient network/protocol issues
        if (ex is SocketException) return true;
        if (ex is IOException) return true;
        if (ex is SmtpProtocolException) return true;
        if (ex is ServiceNotConnectedException) return true;

        // SMTP command failure: retry only 4xx
        if (ex is SmtpCommandException sce)
        {
            var code = (int)sce.StatusCode;
            return code is >= 400 and <= 499;
        }

        // auth failure usually permanent
        if (ex is ServiceNotAuthenticatedException) return false;

        return false; // conservative default
    }

    private static class LocalLog
    {
        public static void SmtpDisabled(ILogger logger) => logger.LogDebug("Email sending is disabled (Email:Enabled=false). Skipping send.");

        public static void SmtpCertificateValidationBypassed(ILogger logger) => logger.LogWarning("SMTP certificate validation is bypassed (AllowInvalidCertificate=true). Do NOT use in production.");

        public static void SmtpSendMailTo(ILogger logger, string to, string? providerMessageId) => logger.LogInformation("SMTP email sent to {To}. ProviderMessageId={ProviderMessageId}", to, providerMessageId);

        public static void SmtpSendCanceled(ILogger logger, string to) => logger.LogWarning("SMTP send canceled for {To}", to);

        public static void SmtpSendFailed(ILogger logger, string to, Exception ex) => logger.LogError(ex, "SMTP send failed for {To}", to);

        public static void SmtpRetry(ILogger logger, int retryCount, double delayMs, string errorMessage, Exception exception) => logger.LogWarning(exception, "SMTP retry {RetryCount} after {DelayMs}ms due to {ErrorMessage}", retryCount, delayMs, errorMessage);
    }
}
