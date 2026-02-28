using FluentValidation;

namespace FactoryERP.Modules.Notification.Application.Emails.SendEmail;

public class SendEmailCommandValidator : AbstractValidator<SendEmailCommand>
{
    public SendEmailCommandValidator()
    {
        RuleFor(x => x.To).NotEmpty();
        RuleForEach(x => x.To).EmailAddress();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(255);
        RuleFor(x => x.HtmlBody).NotEmpty();
    }
}
