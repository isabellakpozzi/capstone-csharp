using FluentAssertions;
using ReservationService.Models.Dtos;
using ReservationService.Validators;
using Xunit;

namespace ReservationService.Tests.Validators;

public class CheckoutRequestValidatorTests
{
    private readonly CheckoutRequestValidator _sut = new();

    [Fact]
    public void Validate_WithNullNotes_Passes()
    {
        var result = _sut.Validate(new CheckoutRequest { Notes = null });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNotesUnderLimit_Passes()
    {
        var result = _sut.Validate(new CheckoutRequest { Notes = "Book condition: good" });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNotesOverLimit_Fails()
    {
        var result = _sut.Validate(new CheckoutRequest { Notes = new string('x', 1001) });
        result.IsValid.Should().BeFalse();
    }
}