using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ReservationService.Controllers;
using ReservationService.Models.Dtos;
using ReservationService.Services;
using Xunit;
using Microsoft.Extensions.Logging;


namespace ReservationService.Tests.Controllers;

public class ReservationsControllerTests
{
    private static ReservationsController CreateSutWithUser(
        IReservationBusinessService reservationService, Guid userId, bool isLibrarian = false)
    {
        var controller = new ReservationsController(reservationService, Mock.Of<ILogger<ReservationsController>>());

        var claims = new List<Claim> { new("userId", userId.ToString()) };
        if (isLibrarian)
            claims.Add(new Claim(ClaimTypes.Role, "Librarian"));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return controller;
    }

    // ---------- CreateReservation ----------

    [Fact]
    public async Task CreateReservation_WhenSuccessful_Returns201()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IReservationBusinessService>();
        var expected = new ReservationResponse { ReservationId = Guid.NewGuid(), Status = "RESERVED" };
        service
            .Setup(s => s.CreateReservationAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((true, expected, null, null, (int?)null));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.CreateReservation(new CreateReservationRequest { BookId = Guid.NewGuid() });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateReservation_WhenLimitExceeded_Returns400WithCurrentCount()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IReservationBusinessService>();
        service
            .Setup(s => s.CreateReservationAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((false, null, "RESERVATION_LIMIT_EXCEEDED", "Limit reached", (int?)5));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.CreateReservation(new CreateReservationRequest { BookId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateReservation_WhenBookUnavailable_Returns400()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IReservationBusinessService>();
        service
            .Setup(s => s.CreateReservationAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((false, null, "BOOK_UNAVAILABLE", "No copies available", (int?)null));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.CreateReservation(new CreateReservationRequest { BookId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- GetActiveReservations ----------

    [Fact]
    public async Task GetActiveReservations_ReturnsDataForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IReservationBusinessService>();
        var expected = new ActiveReservationsResponse { TotalActive = 2 };
        service.Setup(s => s.GetActiveReservationsAsync(userId)).ReturnsAsync(expected);

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.GetActiveReservations();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    // ---------- Checkout ----------

    [Fact]
    public async Task Checkout_WhenSuccessful_Returns200()
    {
        var service = new Mock<IReservationBusinessService>();
        var expected = new CheckoutResponse { Status = "CHECKED_OUT" };
        service
            .Setup(s => s.CheckoutAsync(It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((true, expected, null, null));

        var sut = CreateSutWithUser(service.Object, Guid.NewGuid(), isLibrarian: true);

        var result = await sut.Checkout(Guid.NewGuid(), new CheckoutRequest());

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Checkout_WhenReservationNotFound_Returns404()
    {
        var service = new Mock<IReservationBusinessService>();
        service
            .Setup(s => s.CheckoutAsync(It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((false, null, "NOT_FOUND", "Reservation not found"));

        var sut = CreateSutWithUser(service.Object, Guid.NewGuid(), isLibrarian: true);

        var result = await sut.Checkout(Guid.NewGuid(), new CheckoutRequest());

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Checkout_WhenAlreadyCheckedOut_Returns400InvalidStatus()
    {
        var service = new Mock<IReservationBusinessService>();
        service
            .Setup(s => s.CheckoutAsync(It.IsAny<Guid>(), It.IsAny<string?>()))
            .ReturnsAsync((false, null, "INVALID_STATUS", "Can only checkout RESERVED"));

        var sut = CreateSutWithUser(service.Object, Guid.NewGuid(), isLibrarian: true);

        var result = await sut.Checkout(Guid.NewGuid(), new CheckoutRequest());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- Return ----------

    [Fact]
    public async Task Return_WhenSuccessful_Returns200WithLateFeeDetails()
    {
        var service = new Mock<IReservationBusinessService>();
        var expected = new ReturnResponse { LateDays = 2, LateFee = 2.00m };
        service
            .Setup(s => s.ReturnAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((true, expected, null, null));

        var sut = CreateSutWithUser(service.Object, Guid.NewGuid(), isLibrarian: true);

        var result = await sut.Return(Guid.NewGuid(), new ReturnRequest { Condition = "GOOD" });

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Return_WithInvalidCondition_Returns400ValidationError()
    {
        var service = new Mock<IReservationBusinessService>();
        service
            .Setup(s => s.ReturnAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>()))
            .ReturnsAsync((false, null, "VALIDATION_ERROR", "Invalid condition value"));

        var sut = CreateSutWithUser(service.Object, Guid.NewGuid(), isLibrarian: true);

        var result = await sut.Return(Guid.NewGuid(), new ReturnRequest { Condition = "EXCELLENT" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- History ----------

    [Fact]
    public async Task GetHistory_ReturnsDataForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IReservationBusinessService>();
        var expected = new PaginatedHistoryResponse { TotalElements = 10 };
        service.Setup(s => s.GetHistoryAsync(userId, 0, 20)).ReturnsAsync(expected);

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.GetHistory(0, 20);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    // ---------- Statistics (internal endpoint) ----------

    [Fact]
    public async Task GetStatistics_ReturnsDataForRequestedUser()
    {
        var targetUserId = Guid.NewGuid();
        var service = new Mock<IReservationBusinessService>();
        var expected = new ReservationStatisticsResponse { UserId = targetUserId, ActiveReservations = 3 };
        service.Setup(s => s.GetStatisticsAsync(targetUserId)).ReturnsAsync(expected);

        var sut = new ReservationsController(service.Object, Mock.Of<ILogger<ReservationsController>>());

        var result = await sut.GetStatistics(targetUserId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }
}