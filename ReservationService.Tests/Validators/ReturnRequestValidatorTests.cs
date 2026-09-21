using FluentAssertions;
using ReservationService.Models.Dtos;
using ReservationService.Validators;
using Xunit;

namespace ReservationService.Tests.Validators;

public class ReturnRequestValidatorTests
{
    private readonly ReturnRequestValidator _sut = new();

    [Theory]
    [InlineData("GOOD")]
    [InlineData("good")]
    [InlineData("Fair")]
    [InlineData("Poor")]
    [InlineData("Damaged")]
    public void Validate_WithValidCondition_Passes(string condition)
    {
        var result = _sut.Validate(new ReturnRequest { Condition = condition });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInvalidCondition_Fails()
    {
        var result = _sut.Validate(new ReturnRequest { Condition = "Excellent" });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithEmptyCondition_Fails()
    {
        var result = _sut.Validate(new ReturnRequest { Condition = "" });
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithNotesOverLimit_Fails()
    {
        var result = _sut.Validate(new ReturnRequest
        {
            Condition = "GOOD",
            Notes = new string('x', 1001)
        });
        result.IsValid.Should().BeFalse();
    }
}