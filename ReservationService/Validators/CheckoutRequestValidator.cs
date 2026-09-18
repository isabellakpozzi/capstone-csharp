using FluentValidation;
using ReservationService.Models.Dtos;

namespace ReservationService.Validators;

public class CheckoutRequestValidator : AbstractValidator<CheckoutRequest>
{
    public CheckoutRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(1000).WithMessage("Notes must be 1000 characters or fewer");
    }
}