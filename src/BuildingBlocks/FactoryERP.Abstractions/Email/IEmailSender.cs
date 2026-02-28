namespace FactoryERP.Abstractions.Email;

/*public interface IEmailSenderV2
{
    Task<EmailSendResult> SendAsync(EmailSendRequest request, CancellationToken ct = default);
}*/

// Keep the old one for backward compatibility if any old code uses it until fully migrated
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailSendRequest request, CancellationToken ct = default);
}

public sealed record EmailSendRequest(
    IReadOnlyCollection<EmailAddress> To,
    string Subject,
    string HtmlBody,
    IReadOnlyCollection<EmailAddress>? Cc = null,
    IReadOnlyCollection<EmailAddress>? Bcc = null,
    string? PlainTextBody = null,
    string? ReplyTo = null,
    IReadOnlyCollection<EmailAttachment>? Attachments = null,
    Guid? CorrelationId = null);

public sealed record EmailAddress(
    string Email,
    string? DisplayName = null);

public sealed record EmailAttachment(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record EmailSendResult(
    bool Success,
    string? ProviderMessageId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
