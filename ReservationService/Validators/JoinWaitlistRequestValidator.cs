using FluentValidation;
using ReservationService.Models.Dtos;

namespace ReservationService.Validators;

public class JoinWaitlistRequestValidator : AbstractValidator<JoinWaitlistRequest>
{
    public JoinWaitlistRequestValidator()
    {
        RuleFor(x => x.BookId)
            .NotEmpty().WithMessage("bookId is required");
    }
}