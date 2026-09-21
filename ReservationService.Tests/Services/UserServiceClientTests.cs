using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReservationService.Services;
using System.Net;
using Xunit;

namespace ReservationService.Tests.Services;

public class UserServiceClientTests
{
    private static UserServiceClient CreateSut(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5001") };
        return new UserServiceClient(httpClient, Mock.Of<ILogger<UserServiceClient>>());
    }

    [Fact]
    public async Task ValidateUserAsync_WhenUserServiceReturns200_ReturnsSuccessWithUserData()
    {
        var responseJson = """
        {
            "userId": "11111111-1111-1111-1111-111111111111",
            "email": "test@example.com",
            "firstName": "Jane",
            "lastName": "Doe",
            "role": "PATRON",
            "membershipStatus": "ACTIVE",
            "activeReservationsCount": 2
        }
        """;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var sut = CreateSut(handler);

        var (success, user, errorCode, errorMessage) = await sut.ValidateUserAsync(Guid.NewGuid());

        success.Should().BeTrue();
        user!.Email.Should().Be("test@example.com");
        user.ActiveReservationsCount.Should().Be(2);
    }

    [Fact]
    public async Task ValidateUserAsync_WhenUserServiceReturns404_ReturnsNotFound()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound);
        var sut = CreateSut(handler);

        var (success, user, errorCode, _) = await sut.ValidateUserAsync(Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("NOT_FOUND");
        user.Should().BeNull();
    }

    [Fact]
    public async Task ValidateUserAsync_WhenUserServiceIsUnreachable_ReturnsServiceUnavailableGracefully()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var sut = CreateSut(handler);

        var (success, user, errorCode, _) = await sut.ValidateUserAsync(Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("SERVICE_UNAVAILABLE");
    }

    [Fact]
    public async Task ValidateUserAsync_WhenUserServiceReturnsUnexpectedError_ReturnsValidationError()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var sut = CreateSut(handler);

        var (success, _, errorCode, _) = await sut.ValidateUserAsync(Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("VALIDATION_ERROR");
    }
}