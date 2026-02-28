using FluentValidation;
using Inventory.Application.Commands;

namespace Inventory.Application.Validators;

/// <summary>Validates CreateItemCommand input.</summary>
public sealed class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.ItemNumber)
            .NotEmpty().WithErrorCode("ITEM_NUM_REQUIRED")
            .MaximumLength(40).WithErrorCode("ITEM_NUM_TOO_LONG");

        RuleFor(x => x.Description)
            .NotEmpty().WithErrorCode("DESC_REQUIRED")
            .MaximumLength(200).WithErrorCode("DESC_TOO_LONG");

        RuleFor(x => x.BaseUom)
            .NotEmpty().WithErrorCode("UOM_REQUIRED")
            .MaximumLength(10).WithErrorCode("UOM_TOO_LONG");

        RuleFor(x => x.MaterialGroup)
            .MaximumLength(40).WithErrorCode("MGRP_TOO_LONG")
            .When(x => x.MaterialGroup is not null);
    }
}

/// <summary>Validates UpdateItemCommand input.</summary>
public sealed class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithErrorCode("ID_REQUIRED");

        RuleFor(x => x.Description)
            .NotEmpty().WithErrorCode("DESC_REQUIRED")
            .MaximumLength(200).WithErrorCode("DESC_TOO_LONG");

        RuleFor(x => x.BaseUom)
            .NotEmpty().WithErrorCode("UOM_REQUIRED")
            .MaximumLength(10).WithErrorCode("UOM_TOO_LONG");

        RuleFor(x => x.RowVersion)
            .NotEmpty().WithErrorCode("ROWVERSION_REQUIRED")
            .WithMessage("RowVersion is required for optimistic concurrency.");
    }
}

/// <summary>Validates DeactivateItemCommand input.</summary>
public sealed class DeactivateItemCommandValidator : AbstractValidator<DeactivateItemCommand>
{
    public DeactivateItemCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithErrorCode("ID_REQUIRED");
    }
}
