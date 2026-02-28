using FactoryERP.Abstractions.Email;
using MediatR;

namespace FactoryERP.Modules.Notification.Application.Emails.SendEmail;

public sealed record SendEmailCommand(
    List<string> To,
    string Subject,
    string HtmlBody,
    Guid? CorrelationId = null) : IRequest<EmailSendResult>;
