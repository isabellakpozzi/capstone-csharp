using FluentAssertions;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Services;

public class PasswordValidatorTests
{
    private readonly PasswordValidator _sut = new();

    [Theory]
    [InlineData("Valid1!")]      // 7 chars — one under minimum
    public void Validate_TooShort_ReturnsInvalid(string password)
    {
        var (isValid, error) = _sut.Validate(password);

        isValid.Should().BeFalse();
        error.Should().Contain("at least 8 characters");
    }

    [Fact]
    public void Validate_MissingUppercase_ReturnsInvalid()
    {
        var (isValid, error) = _sut.Validate("lowercase1!");

        isValid.Should().BeFalse();
        error.Should().Contain("uppercase");
    }

    [Fact]
    public void Validate_MissingLowercase_ReturnsInvalid()
    {
        var (isValid, error) = _sut.Validate("UPPERCASE1!");

        isValid.Should().BeFalse();
        error.Should().Contain("lowercase");
    }

    [Fact]
    public void Validate_MissingNumber_ReturnsInvalid()
    {
        var (isValid, error) = _sut.Validate("NoNumbers!");

        isValid.Should().BeFalse();
        error.Should().Contain("number");
    }

    [Fact]
    public void Validate_MissingSpecialCharacter_ReturnsInvalid()
    {
        var (isValid, error) = _sut.Validate("NoSpecial1");

        isValid.Should().BeFalse();
        error.Should().Contain("special character");
    }

    [Theory]
    [InlineData("ValidPass123!")]
    [InlineData("An0ther$ecure1")]
    [InlineData("C0mplex#Pass")]
    public void Validate_MeetsAllRequirements_ReturnsValid(string password)
    {
        var (isValid, error) = _sut.Validate(password);

        isValid.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void Validate_EmptyString_ReturnsInvalid()
    {
        var (isValid, error) = _sut.Validate("");

        isValid.Should().BeFalse();
    }
}