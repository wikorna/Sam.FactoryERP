using FluentValidation;

namespace EDI.Application.Features.UploadEdiBatch;

public sealed class UploadEdiBatchCommandValidator : AbstractValidator<UploadEdiBatchCommand>
{
    public UploadEdiBatchCommandValidator()
    {
        RuleFor(x => x.PartnerCode)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Files)
            .NotEmpty()
            .WithMessage("At least one file is required.")
            .Must(files => files.Count <= 20)
            .WithMessage("Maximum 20 files per batch.");

        RuleForEach(x => x.Files).ChildRules(file =>
        {
            file.RuleFor(f => f.FileName)
                .NotEmpty()
                .MaximumLength(255);

            file.RuleFor(f => f.SizeBytes)
                .GreaterThan(0)
                .WithMessage("File must not be empty.");
        });
    }
}

