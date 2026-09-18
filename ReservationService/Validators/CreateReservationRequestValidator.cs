using FluentValidation;
using ReservationService.Models.Dtos;

namespace ReservationService.Validators;

public class CreateReservationRequestValidator : AbstractValidator<CreateReservationRequest>
{
    public CreateReservationRequestValidator()
    {
        RuleFor(x => x.BookId)
            .NotEmpty().WithMessage("bookId is required");
    }
}