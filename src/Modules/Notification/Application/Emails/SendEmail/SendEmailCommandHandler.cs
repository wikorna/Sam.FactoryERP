using FactoryERP.Abstractions.Email;
using MediatR;

namespace FactoryERP.Modules.Notification.Application.Emails.SendEmail;

public sealed class SendEmailCommandHandler(IEmailSenderV2 emailSender) 
    : IRequestHandler<SendEmailCommand, EmailSendResult>
{
    public Task<EmailSendResult> Handle(SendEmailCommand request, CancellationToken ct)
    {
        var mailRequest = new EmailSendRequest(
            To: request.To.Select(e => new EmailAddress(e)).ToList(),
            Subject: request.Subject,
            HtmlBody: request.HtmlBody,
            CorrelationId: request.CorrelationId
        );

        return emailSender.SendAsync(mailRequest, ct);
    }
}
