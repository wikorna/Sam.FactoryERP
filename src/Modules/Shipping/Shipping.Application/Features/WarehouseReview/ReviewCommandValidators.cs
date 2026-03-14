using FluentValidation;

namespace Shipping.Application.Features.WarehouseReview;

/// <summary>Validates the approve command.</summary>
public sealed class ApproveShipmentBatchCommandValidator : AbstractValidator<ApproveShipmentBatchCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public ApproveShipmentBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.ReviewerUserId).NotEmpty();
        RuleFor(x => x.Comment).MaximumLength(2000).When(x => x.Comment is not null);
    }
}

/// <summary>Validates the reject command.</summary>
public sealed class RejectShipmentBatchCommandValidator : AbstractValidator<RejectShipmentBatchCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public RejectShipmentBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.ReviewerUserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
    }
}

/// <summary>Validates the partial-approve command.</summary>
public sealed class PartiallyApproveShipmentBatchCommandValidator
    : AbstractValidator<PartiallyApproveShipmentBatchCommand>
{
    /// <summary>Initializes validation rules.</summary>
    public PartiallyApproveShipmentBatchCommandValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.ReviewerUserId).NotEmpty();
        RuleFor(x => x.Comment).MaximumLength(2000).When(x => x.Comment is not null);

        RuleFor(x => x.ItemDecisions)
            .NotEmpty()
            .WithMessage("Item decisions are required for partial approval.");

        RuleForEach(x => x.ItemDecisions).ChildRules(item =>
        {
            item.RuleFor(d => d.ItemId).NotEmpty();
            item.RuleFor(d => d.ExclusionReason)
                .MaximumLength(2000)
                .When(d => d.ExclusionReason is not null);
        });
    }
}

