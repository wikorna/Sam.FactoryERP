using FluentValidation;

namespace EDI.Application.Features.DetectEdiFile;

public sealed class DetectEdiFileCommandValidator : AbstractValidator<DetectEdiFileCommand>
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public DetectEdiFileCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .WithMessage("File name is required.")
            .MaximumLength(255);

        RuleFor(x => x.SizeBytes)
            .GreaterThan(0)
            .WithMessage("File must not be empty.")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File must not exceed {MaxFileSizeBytes / 1024 / 1024} MB.");

        RuleFor(x => x.Content)
            .NotNull()
            .WithMessage("File content is required.");
    }
}

