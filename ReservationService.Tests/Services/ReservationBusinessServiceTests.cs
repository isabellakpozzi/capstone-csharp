using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Logging;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;

namespace ReservationService.Tests.Services;

public class ReservationBusinessServiceTests
{
    private static ReservationServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationServiceContext(options);
    }

    private static ReservationBusinessService CreateSut(
    ReservationServiceContext context,
    Mock<IUserServiceClient>? userClient = null,
    Mock<ICatalogServiceClient>? catalogClient = null,
    Mock<IWaitlistBusinessService>? waitlistService = null)
    {
        var waitlistProvided = waitlistService is not null;

        userClient ??= new Mock<IUserServiceClient>();
        catalogClient ??= new Mock<ICatalogServiceClient>();
        waitlistService ??= new Mock<IWaitlistBusinessService>();

        if (!waitlistProvided)
        {
            waitlistService
                .Setup(w => w.TryCascadeToNextEligibleAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);
        }

        return new ReservationBusinessService(
            context,
            userClient.Object,
            catalogClient.Object,
            waitlistService.Object,
            Mock.Of<ILogger<ReservationBusinessService>>());
    }

    private static BookInfo SampleBook(Guid bookId, int availableCopies = 3) => new()
    {
        BookId = bookId,
        Title = "Clean Code",
        Author = "Robert C. Martin",
        AvailableCopies = availableCopies
    };

    [Fact]
    public async Task CreateReservationAsync_WhenBookAvailable_CreatesReservationAndDecrementsAvailability()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId)).ReturnsAsync(SampleBook(bookId));
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, -1)).ReturnsAsync(true);

        var sut = CreateSut(context, catalogClient: catalogClient);

        var (success, data, errorCode, _, _) = await sut.CreateReservationAsync(userId, bookId);

        success.Should().BeTrue();
        data!.Status.Should().Be("RESERVED");
        data.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(bookId, -1), Times.Once);

        var saved = await context.Reservations.FirstAsync();
        saved.Status.Should().Be(ReservationStatus.Reserved);
    }

    [Fact]
    public async Task CreateReservationAsync_WhenUserAtFiveActiveReservations_ReturnsLimitExceeded()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();

        for (int i = 0; i < 5; i++)
        {
            context.Reservations.Add(new Reservation
            {
                UserId = userId,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.Reserved,
                ReservedAt = DateTime.UtcNow,
                BookTitle = "Some Book",
                BookAuthor = "Some Author"
            });
        }
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, data, errorCode, _, currentCount) =
            await sut.CreateReservationAsync(userId, Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("RESERVATION_LIMIT_EXCEEDED");
        currentCount.Should().Be(5);
    }

    [Fact]
    public async Task CreateReservationAsync_CountsBothReservedAndCheckedOutTowardLimit()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();

        context.Reservations.AddRange(
            Enumerable.Range(0, 3).Select(_ => new Reservation
            {
                UserId = userId,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.Reserved,
                ReservedAt = DateTime.UtcNow,
                BookTitle = "A",
                BookAuthor = "B"
            }));
        context.Reservations.AddRange(
            Enumerable.Range(0, 2).Select(_ => new Reservation
            {
                UserId = userId,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.CheckedOut,
                ReservedAt = DateTime.UtcNow,
                BookTitle = "A",
                BookAuthor = "B"
            }));
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, _, errorCode, _, currentCount) =
            await sut.CreateReservationAsync(userId, Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("RESERVATION_LIMIT_EXCEEDED");
        currentCount.Should().Be(5); // 3 Reserved + 2 CheckedOut = 5
    }

    [Fact]
    public async Task CreateReservationAsync_WhenBookHasNoAvailableCopies_ReturnsBookUnavailable()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId)).ReturnsAsync(SampleBook(bookId, availableCopies: 0));

        var sut = CreateSut(context, catalogClient: catalogClient);

        var (success, _, errorCode, _, _) = await sut.CreateReservationAsync(Guid.NewGuid(), bookId);

        success.Should().BeFalse();
        errorCode.Should().Be("BOOK_UNAVAILABLE");
    }

    [Fact]
    public async Task CreateReservationAsync_WhenBookDoesNotExist_ReturnsNotFound()
    {
        var context = CreateContext();
        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(It.IsAny<Guid>())).ReturnsAsync((BookInfo?)null);

        var sut = CreateSut(context, catalogClient: catalogClient);

        var (success, _, errorCode, _, _) = await sut.CreateReservationAsync(Guid.NewGuid(), Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("NOT_FOUND");
    }


    [Fact]
    public async Task CheckoutAsync_WhenReservationIsReserved_SetsCheckedOutStatusAndDueDate()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, data, _, _) = await sut.CheckoutAsync(reservation.ReservationId, "Good condition");

        success.Should().BeTrue();
        data!.Status.Should().Be("CHECKED_OUT");
        data.DueDate.Should().BeCloseTo(DateTime.UtcNow.AddDays(14), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task CheckoutAsync_WhenReservationIsNotReserved_ReturnsInvalidStatus()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, _, errorCode, _) = await sut.CheckoutAsync(reservation.ReservationId, null);

        success.Should().BeFalse();
        errorCode.Should().Be("INVALID_STATUS");
    }

    [Fact]
    public async Task CheckoutAsync_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        var context = CreateContext();
        var sut = CreateSut(context);

        var (success, _, errorCode, _) = await sut.CheckoutAsync(Guid.NewGuid(), null);

        success.Should().BeFalse();
        errorCode.Should().Be("NOT_FOUND");
    }


    [Fact]
    public async Task ReturnAsync_OnTime_HasZeroLateFee()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = bookId,
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-5),
            CheckedOutAt = DateTime.UtcNow.AddDays(-5),
            DueDate = DateTime.UtcNow.AddDays(9),
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var sut = CreateSut(context, catalogClient: catalogClient);

        var (success, data, _, _) = await sut.ReturnAsync(reservation.ReservationId, "Good", null);

        success.Should().BeTrue();
        data!.LateDays.Should().Be(0);
        data.LateFee.Should().Be(0.00m);
    }

    [Fact]
    public async Task ReturnAsync_ThreeDaysLate_ChargesThreeDollars()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = bookId,
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-17),
            CheckedOutAt = DateTime.UtcNow.AddDays(-17),
            DueDate = DateTime.UtcNow.AddDays(-2).AddHours(-12),
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var sut = CreateSut(context, catalogClient: catalogClient);

        var (success, data, _, _) = await sut.ReturnAsync(reservation.ReservationId, "Fair", null);

        success.Should().BeTrue();
        data!.LateDays.Should().Be(3);
        data.LateFee.Should().Be(3.00m);
    }

    [Fact]
    public async Task ReturnAsync_WhenNotCheckedOut_ReturnsInvalidStatus()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, _, errorCode, _) = await sut.ReturnAsync(reservation.ReservationId, "Good", null);

        success.Should().BeFalse();
        errorCode.Should().Be("INVALID_STATUS");
    }

    [Fact]
    public async Task ReturnAsync_WithInvalidConditionValue_ReturnsValidationError()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow,
            CheckedOutAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, _, errorCode, _) = await sut.ReturnAsync(reservation.ReservationId, "Excellent", null);

        success.Should().BeFalse();
        errorCode.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task ReturnAsync_WhenWaitlistCascadeSucceeds_DoesNotIncrementAvailability()
    {
        // This is the critical integration test: confirms return correctly SKIPS
        // the availability increment when a waitlist entry claims the copy instead.
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = bookId,
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-14),
            CheckedOutAt = DateTime.UtcNow.AddDays(-14),
            DueDate = DateTime.UtcNow.AddDays(1),
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var waitlistService = new Mock<IWaitlistBusinessService>();
        waitlistService
            .Setup(w => w.TryCascadeToNextEligibleAsync(bookId, "A", "B"))
            .ReturnsAsync(true); // someone claimed it

        var catalogClient = new Mock<ICatalogServiceClient>();

        var sut = CreateSut(context, catalogClient: catalogClient, waitlistService: waitlistService);

        var (success, _, _, _) = await sut.ReturnAsync(reservation.ReservationId, "Good", null);

        success.Should().BeTrue();
        catalogClient.Verify(c => c.UpdateAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ReturnAsync_WhenNoWaitlistCascade_IncrementsAvailability()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            UserId = Guid.NewGuid(),
            BookId = bookId,
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-14),
            CheckedOutAt = DateTime.UtcNow.AddDays(-14),
            DueDate = DateTime.UtcNow.AddDays(1),
            BookTitle = "A",
            BookAuthor = "B"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var waitlistService = new Mock<IWaitlistBusinessService>();
        waitlistService
            .Setup(w => w.TryCascadeToNextEligibleAsync(bookId, "A", "B"))
            .ReturnsAsync(false); // no one waiting

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var sut = CreateSut(context, catalogClient: catalogClient, waitlistService: waitlistService);

        await sut.ReturnAsync(reservation.ReservationId, "Good", null);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(bookId, 1), Times.Once);
    }

    // ---------- GetHistoryAsync ----------

    [Fact]
    public async Task GetHistoryAsync_MarksReturnedAfterDueDateAsWasLate()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();

        context.Reservations.Add(new Reservation
        {
            UserId = userId,
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.Returned,
            ReservedAt = DateTime.UtcNow.AddDays(-20),
            DueDate = DateTime.UtcNow.AddDays(-10),
            ReturnedAt = DateTime.UtcNow.AddDays(-5), // 5 days after due date
            BookTitle = "A",
            BookAuthor = "B"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.GetHistoryAsync(userId, 0, 20);

        result.Content.Single().WasLate.Should().BeTrue();
    }

    [Fact]
    public async Task GetHistoryAsync_WithNegativePageAndOversizedSize_ClampsToValidRange()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        context.Reservations.Add(new Reservation
        {
            UserId = userId,
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.Returned,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "A",
            BookAuthor = "B"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.GetHistoryAsync(userId, page: -5, size: 999999);

        result.Page.Should().Be(0);
        result.Size.Should().Be(100);
    }
}