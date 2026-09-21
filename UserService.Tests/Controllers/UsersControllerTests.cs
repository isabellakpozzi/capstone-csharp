using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserService.Controllers;
using UserService.Models.Dtos;
using UserService.Services;
using Xunit;

namespace UserService.Tests.Controllers;

public class UsersControllerTests
{
    private static UsersController CreateSutWithUser(IAuthService authService, Guid userId)
    {
        var controller = new UsersController(authService);

        var claims = new List<Claim> { new("userId", userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    [Fact]
    public async Task GetProfile_WithValidToken_ReturnsProfileData()
    {
        var userId = Guid.NewGuid();
        var authService = new Mock<IAuthService>();
        var expectedProfile = new ProfileResponse { UserId = userId, Email = "a@b.com" };
        authService
            .Setup(a => a.GetProfileAsync(userId))
            .ReturnsAsync(AuthResult<ProfileResponse>.Ok(expectedProfile));

        var sut = CreateSutWithUser(authService.Object, userId);

        var result = await sut.GetProfile();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expectedProfile);
    }

    [Fact]
    public async Task GetProfile_WhenUserNotFound_Returns404()
    {
        var userId = Guid.NewGuid();
        var authService = new Mock<IAuthService>();
        authService
            .Setup(a => a.GetProfileAsync(userId))
            .ReturnsAsync(AuthResult<ProfileResponse>.Fail("NOT_FOUND", "User not found"));

        var sut = CreateSutWithUser(authService.Object, userId);

        var result = await sut.GetProfile();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetProfile_WithNoUserIdClaim_ThrowsUnauthorizedAccessException()
    {
        var authService = new Mock<IAuthService>();
        var controller = new UsersController(authService.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        var act = async () => await controller.GetProfile();

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ValidateUser_WhenUserSuspended_Returns400()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(a => a.ValidateUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AuthResult<UserValidationResponse>.Fail("VALIDATION_ERROR", "User is suspended"));

        var sut = new UsersController(authService.Object);

        var result = await sut.ValidateUser(Guid.NewGuid());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ValidateUser_WhenUserNotFound_Returns404()
    {
        var authService = new Mock<IAuthService>();
        authService
            .Setup(a => a.ValidateUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AuthResult<UserValidationResponse>.Fail("NOT_FOUND", "User not found"));

        var sut = new UsersController(authService.Object);

        var result = await sut.ValidateUser(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ValidateUser_WhenValid_Returns200WithData()
    {
        var authService = new Mock<IAuthService>();
        var expected = new UserValidationResponse { UserId = Guid.NewGuid() };
        authService
            .Setup(a => a.ValidateUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync(AuthResult<UserValidationResponse>.Ok(expected));

        var sut = new UsersController(authService.Object);

        var result = await sut.ValidateUser(Guid.NewGuid());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }
}