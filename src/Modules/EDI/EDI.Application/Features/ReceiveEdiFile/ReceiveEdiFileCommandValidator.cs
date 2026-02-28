using EDI.Application.Features.ReceiveEdiFile;
using FluentValidation;

namespace Sam.FactoryErp.Edi.Application.Features.ReceiveEdiFile;

public sealed class ReceiveEdiFileCommandValidator : AbstractValidator<ReceiveEdiFileCommand>
{
    public ReceiveEdiFileCommandValidator()
    {
        RuleFor(x => x.PartnerCode)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(255);

        RuleFor(x => x.FullPath)
            .NotEmpty()
            .MaximumLength(1000);
    }
}
