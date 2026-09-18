using FluentValidation;
using ReservationService.Models;
using ReservationService.Models.Dtos;

namespace ReservationService.Validators;

public class ReturnRequestValidator : AbstractValidator<ReturnRequest>
{
    private static readonly string[] ValidConditions =
        Enum.GetNames(typeof(BookCondition));

    public ReturnRequestValidator()
    {
        RuleFor(x => x.Condition)
            .NotEmpty().WithMessage("Condition is required")
            .Must(c => ValidConditions.Contains(c, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Condition must be one of: {string.Join(", ", ValidConditions)}");

        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must be 1000 characters or fewer");
    }
}