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

public class WaitlistControllerTests
{
    private static WaitlistController CreateSutWithUser(IWaitlistBusinessService waitlistService, Guid userId)
    {
        var controller = new WaitlistController(waitlistService);

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
    public async Task JoinWaitlist_WhenSuccessful_Returns201()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IWaitlistBusinessService>();
        var expected = new WaitlistResponse { Status = "WAITING", Position = 1 };
        service
            .Setup(s => s.JoinWaitlistAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((true, expected, null, null));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.JoinWaitlist(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task JoinWaitlist_WhenBookAvailable_Returns400()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IWaitlistBusinessService>();
        service
            .Setup(s => s.JoinWaitlistAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((false, null, "BOOK_AVAILABLE", "Reserve directly instead"));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.JoinWaitlist(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task JoinWaitlist_WhenAlreadyWaitlisted_Returns400()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IWaitlistBusinessService>();
        service
            .Setup(s => s.JoinWaitlistAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((false, null, "ALREADY_WAITLISTED", "Already on this waitlist"));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.JoinWaitlist(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetMyWaitlist_ReturnsEntriesForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IWaitlistBusinessService>();
        var expected = new MyWaitlistResponse { Entries = new List<WaitlistEntryItem>() };
        service.Setup(s => s.GetMyWaitlistAsync(userId)).ReturnsAsync(expected);

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.GetMyWaitlist();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task LeaveWaitlist_WhenSuccessful_Returns200()
    {
        var userId = Guid.NewGuid();
        var waitlistId = Guid.NewGuid();
        var service = new Mock<IWaitlistBusinessService>();
        var expected = new CancelWaitlistResponse { Status = "CANCELLED" };
        service
            .Setup(s => s.LeaveWaitlistAsync(userId, waitlistId))
            .ReturnsAsync((true, expected, null, null));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.LeaveWaitlist(waitlistId);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
    }

    [Fact]
    public async Task LeaveWaitlist_WhenNotFound_Returns404()
    {
        var userId = Guid.NewGuid();
        var service = new Mock<IWaitlistBusinessService>();
        service
            .Setup(s => s.LeaveWaitlistAsync(userId, It.IsAny<Guid>()))
            .ReturnsAsync((false, null, "NOT_FOUND", "Waitlist entry not found"));

        var sut = CreateSutWithUser(service.Object, userId);

        var result = await sut.LeaveWaitlist(Guid.NewGuid());

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}