using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReservationService.Services;
using System.Net;
using Xunit;

namespace ReservationService.Tests.Services;

public class CatalogServiceClientTests
{
    private static CatalogServiceClient CreateSut(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5002") };
        return new CatalogServiceClient(httpClient, Mock.Of<ILogger<CatalogServiceClient>>());
    }

    [Fact]
    public async Task GetBookAsync_WhenBookExists_ReturnsBookInfo()
    {
        var responseJson = """
        {
            "bookId": "11111111-1111-1111-1111-111111111111",
            "title": "Clean Code",
            "author": "Robert C. Martin",
            "availableCopies": 3
        }
        """;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, responseJson);
        var sut = CreateSut(handler);

        var book = await sut.GetBookAsync(Guid.NewGuid());

        book.Should().NotBeNull();
        book!.Title.Should().Be("Clean Code");
        book.AvailableCopies.Should().Be(3);
    }

    [Fact]
    public async Task GetBookAsync_WhenBookNotFound_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound);
        var sut = CreateSut(handler);

        var book = await sut.GetBookAsync(Guid.NewGuid());

        book.Should().BeNull();
    }

    [Fact]
    public async Task GetBookAsync_WhenCatalogServiceIsUnreachable_ReturnsNullGracefully()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var sut = CreateSut(handler);

        var book = await sut.GetBookAsync(Guid.NewGuid());

        book.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAvailabilityAsync_WhenSuccessful_ReturnsTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");
        var sut = CreateSut(handler);

        var result = await sut.UpdateAvailabilityAsync(Guid.NewGuid(), -1);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAvailabilityAsync_WhenCatalogServiceRejects_ReturnsFalse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest);
        var sut = CreateSut(handler);

        var result = await sut.UpdateAvailabilityAsync(Guid.NewGuid(), 1);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAvailabilityAsync_WhenCatalogServiceIsUnreachable_ReturnsFalseGracefully()
    {
        var handler = new FakeHttpMessageHandler(new HttpRequestException("Connection refused"));
        var sut = CreateSut(handler);

        var result = await sut.UpdateAvailabilityAsync(Guid.NewGuid(), 1);

        result.Should().BeFalse();
    }
}