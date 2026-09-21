using FluentAssertions;
using ReservationService.Models.Dtos;
using ReservationService.Validators;
using Xunit;

namespace ReservationService.Tests.Validators;

public class CreateReservationRequestValidatorTests
{
    private readonly CreateReservationRequestValidator _sut = new();

    [Fact]
    public void Validate_WithValidBookId_Passes()
    {
        var result = _sut.Validate(new CreateReservationRequest { BookId = Guid.NewGuid() });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyBookId_Fails()
    {
        var result = _sut.Validate(new CreateReservationRequest { BookId = Guid.Empty });
        result.IsValid.Should().BeFalse();
    }
}