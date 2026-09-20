using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UserService.Data;
using UserService.Models;
using UserService.Models.Dtos;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Services;

public class AuthServiceTests
{
    private static UserServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UserServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()) // unique DB per test = full isolation
            .Options;
        return new UserServiceContext(options);
    }

    private static AuthService CreateSut(
        UserServiceContext context,
        Mock<IPasswordHasher>? passwordHasher = null,
        Mock<IPasswordValidator>? passwordValidator = null,
        Mock<IJwtTokenService>? jwtTokenService = null,
        Mock<IReservationServiceClient>? reservationClient = null)
    {
        var hasherProvided = passwordHasher is not null;
        var validatorProvided = passwordValidator is not null;
        var jwtProvided = jwtTokenService is not null;

        passwordHasher ??= new Mock<IPasswordHasher>();
        passwordValidator ??= new Mock<IPasswordValidator>();
        jwtTokenService ??= new Mock<IJwtTokenService>();
        reservationClient ??= new Mock<IReservationServiceClient>();

        if (!validatorProvided)
            passwordValidator.Setup(v => v.Validate(It.IsAny<string>())).Returns((true, null));

        if (!hasherProvided)
            passwordHasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed-password");

        if (!jwtProvided)
            jwtTokenService.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns("fake-jwt-token");

        return new AuthService(
            context,
            passwordHasher.Object,
            passwordValidator.Object,
            jwtTokenService.Object,
            reservationClient.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_CreatesUserWithPatronRoleAndActiveStatus()
    {
        var context = CreateContext();
        var sut = CreateSut(context);

        var request = new RegisterRequest
        {
            Email = "newuser@example.com",
            Password = "ValidPass123!",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "+1-555-0100"
        };

        var result = await sut.RegisterAsync(request);

        result.Success.Should().BeTrue();
        result.Data!.Role.Should().Be("PATRON");
        result.Data.MembershipStatus.Should().Be("ACTIVE");

        var savedUser = await context.Users.FirstAsync(u => u.Email == request.Email);
        savedUser.Role.Should().Be(Role.Patron);
        savedUser.MembershipStatus.Should().Be(MembershipStatus.Active);
        savedUser.MemberSince.Should().NotBeNull();
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ReturnsValidationError()
    {
        var context = CreateContext();
        context.Users.Add(new User
        {
            Email = "existing@example.com",
            PasswordHash = "hash",
            FirstName = "A",
            LastName = "B",
            PhoneNumber = "1234567890"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var request = new RegisterRequest
        {
            Email = "existing@example.com", // same email, different case shouldn't matter either
            Password = "ValidPass123!",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "+1-555-0100"
        };

        var result = await sut.RegisterAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().Contain("already exists");
    }

    [Fact]
    public async Task RegisterAsync_WithWeakPassword_ReturnsValidationErrorFromValidator()
    {
        var context = CreateContext();
        var passwordValidator = new Mock<IPasswordValidator>();
        passwordValidator
            .Setup(v => v.Validate("weak"))
            .Returns((false, "Password must be at least 8 characters long"));

        var sut = CreateSut(context, passwordValidator: passwordValidator);

        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "weak",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "+1-555-0100"
        };

        var result = await sut.RegisterAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().Be("Password must be at least 8 characters long");
    }

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokenAndUserSummary()
    {
        var context = CreateContext();
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(h => h.Verify("correct-password", "stored-hash")).Returns(true);

        context.Users.Add(new User
        {
            Email = "user@example.com",
            PasswordHash = "stored-hash",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "1234567890",
            Role = Role.Patron
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context, passwordHasher: passwordHasher);

        var result = await sut.LoginAsync(new LoginRequest
        {
            Email = "user@example.com",
            Password = "correct-password"
        });

        result.Success.Should().BeTrue();
        result.Data!.AccessToken.Should().Be("fake-jwt-token");
        result.Data.TokenType.Should().Be("Bearer");
        result.Data.ExpiresIn.Should().Be(86400);
        result.Data.User.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsAuthenticationFailed()
    {
        var context = CreateContext();
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        context.Users.Add(new User
        {
            Email = "user@example.com",
            PasswordHash = "stored-hash",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "1234567890"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context, passwordHasher: passwordHasher);

        var result = await sut.LoginAsync(new LoginRequest
        {
            Email = "user@example.com",
            Password = "wrong-password"
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTHENTICATION_FAILED");
    }

    [Fact]
    public async Task LoginAsync_WithNonexistentEmail_ReturnsAuthenticationFailed()
    {
        var context = CreateContext();
        var sut = CreateSut(context);

        var result = await sut.LoginAsync(new LoginRequest
        {
            Email = "doesnotexist@example.com",
            Password = "anything"
        });

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AUTHENTICATION_FAILED");
    }

    [Fact]
    public async Task GetProfileAsync_WhenReservationServiceIsUnavailable_ReturnsZeroedStatsGracefully()
    {
        var context = CreateContext();
        var user = new User
        {
            Email = "user@example.com",
            PasswordHash = "hash",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "1234567890",
            MemberSince = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var reservationClient = new Mock<IReservationServiceClient>();
        reservationClient
            .Setup(c => c.GetStatisticsAsync(user.UserId))
            .ReturnsAsync((ReservationStatsResponse?)null); // simulates service being down

        var sut = CreateSut(context, reservationClient: reservationClient);

        var result = await sut.GetProfileAsync(user.UserId);

        result.Success.Should().BeTrue();
        result.Data!.ActiveReservations.Should().Be(0);
        result.Data.BorrowingHistory.Should().Be(0);
    }

    [Fact]
    public async Task GetProfileAsync_WithNonexistentUser_ReturnsNotFound()
    {
        var context = CreateContext();
        var sut = CreateSut(context);

        var result = await sut.GetProfileAsync(Guid.NewGuid());

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task ValidateUserAsync_WithSuspendedUser_ReturnsValidationError()
    {
        var context = CreateContext();
        var user = new User
        {
            Email = "suspended@example.com",
            PasswordHash = "hash",
            FirstName = "Jane",
            LastName = "Doe",
            PhoneNumber = "1234567890",
            MembershipStatus = MembershipStatus.Suspended
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.ValidateUserAsync(user.UserId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("VALIDATION_ERROR");
        result.ErrorMessage.Should().Contain("suspended");
    }
}