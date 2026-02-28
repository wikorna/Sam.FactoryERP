using FluentValidation;

namespace Labeling.Application.Features.PrintJobs;

public sealed class CreatePrintJobValidator : AbstractValidator<CreatePrintJobCommand>
{
    /// <summary>Maximum ZPL payload size: 1 MB.</summary>
    private const int MaxPayloadBytes = 1_048_576;

    public CreatePrintJobValidator()
    {
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .MaximumLength(256)
            .WithMessage("IdempotencyKey is required and must be ≤ 256 characters.");

        RuleFor(x => x.PrinterId)
            .NotEmpty()
            .WithMessage("PrinterId is required.");

        RuleFor(x => x.ZplContent)
            .NotEmpty()
            .Must(zpl => System.Text.Encoding.UTF8.GetByteCount(zpl) <= MaxPayloadBytes)
            .WithMessage($"ZplContent must not exceed {MaxPayloadBytes:N0} bytes.");

        RuleFor(x => x.Copies)
            .InclusiveBetween(1, 100)
            .WithMessage("Copies must be between 1 and 100.");

        RuleFor(x => x.RequestedBy)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("RequestedBy is required.");
    }
}

