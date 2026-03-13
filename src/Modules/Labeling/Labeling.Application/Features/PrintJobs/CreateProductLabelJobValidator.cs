using FluentValidation;

namespace Labeling.Application.Features.PrintJobs;

public sealed class CreateProductLabelJobValidator : AbstractValidator<CreateProductLabelJobCommand>
{
    public CreateProductLabelJobValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(256);
        RuleFor(x => x.PrinterId).NotEmpty();
        RuleFor(x => x.Copies).InclusiveBetween(1, 100);
        RuleFor(x => x.RequestedBy).NotEmpty();

        RuleFor(x => x.LabelData).NotNull();
        RuleFor(x => x.LabelData.DocNo).NotEmpty();
        RuleFor(x => x.LabelData.ProductName).NotEmpty();
        RuleFor(x => x.LabelData.PartNo).NotEmpty();
        RuleFor(x => x.LabelData.QrPayload).NotEmpty();
    }
}

