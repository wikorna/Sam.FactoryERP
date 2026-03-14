using FluentValidation;

namespace Shipping.Application.Features.UploadBatch;

/// <summary>Validates the upload command before the handler executes.</summary>
public sealed class UploadShipmentBatchCommandValidator : AbstractValidator<UploadShipmentBatchCommand>
{
    /// <summary>Max file size: 10 MB.</summary>
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    /// <summary>Initializes validation rules.</summary>
    public UploadShipmentBatchCommandValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty()
            .MaximumLength(255)
            .Must(name => name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only CSV files are accepted.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0)
            .WithMessage("File must not be empty.")
            .LessThanOrEqualTo(MaxFileSizeBytes)
            .WithMessage($"File must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");

        RuleFor(x => x.FileStream)
            .NotNull()
            .WithMessage("File stream is required.");

        RuleFor(x => x.PoReference)
            .MaximumLength(500)
            .When(x => x.PoReference is not null);
    }
}

