using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using UserService.Models;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Services;

public class JwtTokenServiceTests
{
    private static IConfiguration BuildConfig(string? secret = "test-secret-key-at-least-32-characters-long")
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = secret,
            ["Jwt:Issuer"] = "TestIssuer",
            ["Jwt:Audience"] = "TestAudience",
            ["Jwt:ExpiresInSeconds"] = "86400"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    private static User CreateTestUser() => new()
    {
        UserId = Guid.NewGuid(),
        Email = "test@example.com",
        Role = Role.Librarian,
        FirstName = "Test",
        LastName = "User",
        PhoneNumber = "1234567890",
        PasswordHash = "hash"
    };

    [Fact]
    public void GenerateToken_IncludesUserIdEmailAndRoleClaims()
    {
        var config = BuildConfig();
        var sut = new JwtTokenService(config);
        var user = CreateTestUser();

        var token = sut.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Claims.Should().Contain(c => c.Type == "userId" && c.Value == user.UserId.ToString());
        jwt.Claims.Should().Contain(c => c.Value == user.Email);
        jwt.Claims.Should().Contain(c => c.Value == "Librarian");
    }

    [Fact]
    public void GenerateToken_SetsExpirationTo24HoursFromNow()
    {
        var config = BuildConfig();
        var sut = new JwtTokenService(config);
        var user = CreateTestUser();

        var beforeGeneration = DateTime.UtcNow;
        var token = sut.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // small tolerance for test execution time
        jwt.ValidTo.Should().BeCloseTo(beforeGeneration.AddHours(24), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateToken_SetsCorrectIssuerAndAudience()
    {
        var config = BuildConfig();
        var sut = new JwtTokenService(config);
        var user = CreateTestUser();

        var token = sut.GenerateToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        jwt.Issuer.Should().Be("TestIssuer");
        jwt.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public void GenerateToken_TwoCallsForSameUser_ProduceDifferentTokens()
    {
        
        var config = BuildConfig();
        var sut = new JwtTokenService(config);
        var user = CreateTestUser();

        var token1 = sut.GenerateToken(user);
        var token2 = sut.GenerateToken(user);

        token1.Should().NotBe(token2);
    }

    [Fact]
    public void Constructor_WithMissingSecret_ThrowsOnGenerateToken()
    {
        var config = BuildConfig(secret: null);
        var sut = new JwtTokenService(config);
        var user = CreateTestUser();

        var act = () => sut.GenerateToken(user);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Jwt:Secret*");
    }
}