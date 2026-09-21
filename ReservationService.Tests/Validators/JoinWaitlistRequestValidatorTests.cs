using FluentAssertions;
using ReservationService.Models.Dtos;
using ReservationService.Validators;
using Xunit;

namespace ReservationService.Tests.Validators;

public class JoinWaitlistRequestValidatorTests
{
    private readonly JoinWaitlistRequestValidator _sut = new();

    [Fact]
    public void Validate_WithValidBookId_Passes()
    {
        var result = _sut.Validate(new JoinWaitlistRequest { BookId = Guid.NewGuid() });
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyBookId_Fails()
    {
        var result = _sut.Validate(new JoinWaitlistRequest { BookId = Guid.Empty });
        result.IsValid.Should().BeFalse();
    }
}