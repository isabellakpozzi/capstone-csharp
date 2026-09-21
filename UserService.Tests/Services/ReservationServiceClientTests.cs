using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using UserService.Services;
using System.Net;
using Xunit;

namespace UserService.Tests.Services;

public class ReservationServiceClientTests
{
    private static ReservationServiceClient CreateSut(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5003") };
        return new ReservationServiceClient(httpClient, Mock.Of<ILogger<ReservationServiceClient>>());
    }

    [Fact]
    public async Task GetStatisticsAsync_WhenSuccessful_ReturnsStats()
    {
        var responseJson = """
        {
            "userId": "11111111-1111-1111-1111-111111111111",
            "activeReservations": 3,
            "borrowingHistory": 12
        }
        """;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var sut = CreateSut(handler);

        var stats = await sut.GetStatisticsAsync(Guid.NewGuid());

        stats.Should().NotBeNull();
        stats!.ActiveReservations.Should().Be(3);
        stats.BorrowingHistory.Should().Be(12);
    }

    [Fact]
    public async Task GetStatisticsAsync_WhenReservationServiceReturnsError_ReturnsNullGracefully()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var sut = CreateSut(handler);

        var stats = await sut.GetStatisticsAsync(Guid.NewGuid());

        stats.Should().BeNull();
    }

    [Fact]
    public async Task GetStatisticsAsync_WhenReservationServiceIsUnreachable_ReturnsNullGracefully()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var sut = CreateSut(handler);

        var stats = await sut.GetStatisticsAsync(Guid.NewGuid());

        stats.Should().BeNull();
    }
}