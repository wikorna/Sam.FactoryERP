using FluentValidation;

namespace EDI.Application.Features.ParseEdiFile;

public sealed class ParseEdiFileCommandValidator : AbstractValidator<ParseEdiFileCommand>
{
    public ParseEdiFileCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty();
    }
}
