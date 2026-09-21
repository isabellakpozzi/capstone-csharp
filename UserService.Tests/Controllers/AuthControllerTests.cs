using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserService.Controllers;
using UserService.Models.Dtos;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Register_WhenSuccessful_Returns201WithData()
    {
        var authService = new Mock<IAuthService>();
        var expectedResponse = new RegisterResponse { UserId = Guid.NewGuid(), Email = "a@b.com" };
        authService
            .Setup(a => a.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(AuthResult<RegisterResponse>.Ok(expectedResponse));

        var sut = new AuthController(authService.Object);

        var result = await sut.Register(new RegisterRequest { Email = "a@b.com" });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
        objectResult.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task Register_WhenEmailExists_Returns400WithErrorResponse()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(a => a.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(AuthResult<RegisterResponse>.Fail("VALIDATION_ERROR", "Email already exists"));

        var sut = new AuthController(authService.Object);

        var result = await sut.Register(new RegisterRequest());

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var error = badRequest.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Error.Should().Be("VALIDATION_ERROR");
        error.Message.Should().Be("Email already exists");
    }

    [Fact]
    public async Task Login_WhenSuccessful_Returns200WithToken()
    {
        var authService = new Mock<IAuthService>();
        var expectedResponse = new LoginResponse { AccessToken = "token123" };
        authService
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(AuthResult<LoginResponse>.Ok(expectedResponse));

        var sut = new AuthController(authService.Object);

        var result = await sut.Login(new LoginRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expectedResponse);
    }

    [Fact]
    public async Task Login_WhenCredentialsInvalid_Returns401()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(a => a.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(AuthResult<LoginResponse>.Fail("AUTHENTICATION_FAILED", "Invalid email or password"));

        var sut = new AuthController(authService.Object);

        var result = await sut.Login(new LoginRequest());

        var unauthorized = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var error = unauthorized.Value.Should().BeOfType<ErrorResponse>().Subject;
        error.Error.Should().Be("AUTHENTICATION_FAILED");
    }
}