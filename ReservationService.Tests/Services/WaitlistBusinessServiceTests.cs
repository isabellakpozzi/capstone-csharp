using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;
using Xunit;
using Microsoft.Extensions.Logging;

namespace ReservationService.Tests.Services;

public class WaitlistBusinessServiceTests
{
    private static ReservationServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationServiceContext(options);
    }

    private static WaitlistBusinessService CreateSut(
        ReservationServiceContext context,
        Mock<ICatalogServiceClient>? catalogClient = null)
    {
        catalogClient ??= new Mock<ICatalogServiceClient>();
        return new WaitlistBusinessService(
            context,
            catalogClient.Object,
            Mock.Of<ILogger<WaitlistBusinessService>>());
    }

    private static async Task<int> ActiveReservationCountFor(ReservationServiceContext context, Guid userId) =>
        await context.Reservations.CountAsync(r =>
            r.UserId == userId &&
            (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

    // ---------- TryCascadeToNextEligibleAsync (the core cascade loop) ----------

    [Fact]
    public async Task TryCascade_WithEmptyQueue_ReturnsFalse()
    {
        var context = CreateContext();
        var sut = CreateSut(context);

        var result = await sut.TryCascadeToNextEligibleAsync(Guid.NewGuid(), "Title", "Author");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryCascade_WithOneEligibleWaitingEntry_NotifiesThemAndCreatesReservation()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            BookTitle = "Title",
            BookAuthor = "Author"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.TryCascadeToNextEligibleAsync(bookId, "Title", "Author");

        result.Should().BeTrue();

        var entry = await context.WaitlistEntries.FirstAsync();
        entry.Status.Should().Be(WaitlistStatus.Notified);
        entry.NotifiedAt.Should().NotBeNull();
        entry.ClaimDeadline.Should().BeCloseTo(DateTime.UtcNow.AddHours(48), TimeSpan.FromMinutes(1));
        entry.ResultingReservationId.Should().NotBeNull();

        var reservation = await context.Reservations.SingleAsync();
        reservation.UserId.Should().Be(userId);
        reservation.BookId.Should().Be(bookId);
        reservation.Status.Should().Be(ReservationStatus.Reserved);
        reservation.ReservationId.Should().Be(entry.ResultingReservationId!.Value);
    }

    [Fact]
    public async Task TryCascade_SkipsIneligibleEntriesAndNotifiesNextEligibleOne()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        var ineligibleUser = Guid.NewGuid();
        var eligibleUser = Guid.NewGuid();

        // ineligibleUser already has 5 active reservations
        context.Reservations.AddRange(Enumerable.Range(0, 5).Select(_ => new Reservation
        {
            UserId = ineligibleUser,
            BookId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "X",
            BookAuthor = "Y"
        }));

        // ineligibleUser joined the waitlist first (longest-waiting)
        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = ineligibleUser,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-2),
            BookTitle = "Title",
            BookAuthor = "Author"
        });

        // eligibleUser joined second, has no active reservations
        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = eligibleUser,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            BookTitle = "Title",
            BookAuthor = "Author"
        });

        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.TryCascadeToNextEligibleAsync(bookId, "Title", "Author");

        result.Should().BeTrue();

        var ineligibleEntry = await context.WaitlistEntries.FirstAsync(w => w.UserId == ineligibleUser);
        ineligibleEntry.Status.Should().Be(WaitlistStatus.Expired);

        var eligibleEntry = await context.WaitlistEntries.FirstAsync(w => w.UserId == eligibleUser);
        eligibleEntry.Status.Should().Be(WaitlistStatus.Notified);

        var reservation = await context.Reservations.SingleAsync(r => r.BookId == bookId);
        reservation.UserId.Should().Be(eligibleUser);
    }

    [Fact]
    public async Task TryCascade_WhenEveryoneInQueueIsIneligible_ExpiresAllAndReturnsFalse()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        foreach (var user in new[] { user1, user2 })
        {
            context.Reservations.AddRange(Enumerable.Range(0, 5).Select(_ => new Reservation
            {
                UserId = user,
                BookId = Guid.NewGuid(),
                Status = ReservationStatus.Reserved,
                ReservedAt = DateTime.UtcNow,
                BookTitle = "X",
                BookAuthor = "Y"
            }));
        }

        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = user1,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-2),
            BookTitle = "Title",
            BookAuthor = "Author"
        });
        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = user2,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            BookTitle = "Title",
            BookAuthor = "Author"
        });

        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.TryCascadeToNextEligibleAsync(bookId, "Title", "Author");

        result.Should().BeFalse();

        var entries = await context.WaitlistEntries.Where(w => w.BookId == bookId).ToListAsync();
        entries.Should().OnlyContain(w => w.Status == WaitlistStatus.Expired);

        (await context.Reservations.CountAsync(r => r.BookId == bookId)).Should().Be(0);
    }

    [Fact]
    public async Task TryCascade_NotifiesLongestWaitingEntryFirst()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        var laterJoiner = Guid.NewGuid();
        var earlierJoiner = Guid.NewGuid();

        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = laterJoiner,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddHours(-1),
            BookTitle = "T",
            BookAuthor = "A"
        });
        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = earlierJoiner,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-3),
            BookTitle = "T",
            BookAuthor = "A"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        await sut.TryCascadeToNextEligibleAsync(bookId, "T", "A");

        var reservation = await context.Reservations.SingleAsync();
        reservation.UserId.Should().Be(earlierJoiner);
    }

    [Fact]
    public async Task TryCascade_OnlyConsidersEntriesForTheSpecifiedBook()
    {
        var context = CreateContext();
        var targetBook = Guid.NewGuid();
        var otherBook = Guid.NewGuid();

        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = otherBook,
            UserId = Guid.NewGuid(),
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = "Other",
            BookAuthor = "Other"
        });
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var result = await sut.TryCascadeToNextEligibleAsync(targetBook, "Target", "Author");

        result.Should().BeFalse(); // no entries for targetBook, only for otherBook
    }

    // ---------- JoinWaitlistAsync ----------

    [Fact]
    public async Task JoinWaitlistAsync_WhenBookHasAvailableCopies_ReturnsBookAvailable()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookInfo { BookId = bookId, Title = "T", Author = "A", AvailableCopies = 2 });

        var sut = CreateSut(context, catalogClient);

        var (success, _, errorCode, _) = await sut.JoinWaitlistAsync(Guid.NewGuid(), bookId);

        success.Should().BeFalse();
        errorCode.Should().Be("BOOK_AVAILABLE");
    }

    [Fact]
    public async Task JoinWaitlistAsync_WhenAlreadyWaiting_ReturnsAlreadyWaitlisted()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = "T",
            BookAuthor = "A"
        });
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookInfo { BookId = bookId, Title = "T", Author = "A", AvailableCopies = 0 });

        var sut = CreateSut(context, catalogClient);

        var (success, _, errorCode, _) = await sut.JoinWaitlistAsync(userId, bookId);

        success.Should().BeFalse();
        errorCode.Should().Be("ALREADY_WAITLISTED");
    }

    [Fact]
    public async Task JoinWaitlistAsync_WhenEligible_ReturnsCorrectQueuePosition()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        // Two people already waiting ahead of the new joiner
        context.WaitlistEntries.AddRange(
            new Waitlist { BookId = bookId, UserId = Guid.NewGuid(), Status = WaitlistStatus.Waiting, JoinedAt = DateTime.UtcNow.AddDays(-2), BookTitle = "T", BookAuthor = "A" },
            new Waitlist { BookId = bookId, UserId = Guid.NewGuid(), Status = WaitlistStatus.Waiting, JoinedAt = DateTime.UtcNow.AddDays(-1), BookTitle = "T", BookAuthor = "A" });
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookInfo { BookId = bookId, Title = "T", Author = "A", AvailableCopies = 0 });

        var sut = CreateSut(context, catalogClient);

        var (success, data, _, _) = await sut.JoinWaitlistAsync(Guid.NewGuid(), bookId);

        success.Should().BeTrue();
        data!.Position.Should().Be(3);
    }

    // ---------- LeaveWaitlistAsync ----------

    [Fact]
    public async Task LeaveWaitlistAsync_WhenEntryNotFound_ReturnsNotFound()
    {
        var context = CreateContext();
        var sut = CreateSut(context);

        var (success, _, errorCode, _) = await sut.LeaveWaitlistAsync(Guid.NewGuid(), Guid.NewGuid());

        success.Should().BeFalse();
        errorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task LeaveWaitlistAsync_WhenEntryBelongsToDifferentUser_ReturnsNotFound()
    {
        var context = CreateContext();
        var entry = new Waitlist
        {
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.WaitlistEntries.Add(entry);
        await context.SaveChangesAsync();

        var sut = CreateSut(context);

        var (success, _, errorCode, _) = await sut.LeaveWaitlistAsync(Guid.NewGuid(), entry.WaitlistId);

        success.Should().BeFalse();
        errorCode.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task LeaveWaitlistAsync_WhenWaiting_CancelsWithoutTriggeringCascade()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var entry = new Waitlist
        {
            BookId = Guid.NewGuid(),
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow,
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.WaitlistEntries.Add(entry);
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        var sut = CreateSut(context, catalogClient);

        var (success, data, _, _) = await sut.LeaveWaitlistAsync(userId, entry.WaitlistId);

        success.Should().BeTrue();
        data!.Status.Should().Be("CANCELLED");

        var updated = await context.WaitlistEntries.FindAsync(entry.WaitlistId);
        updated!.Status.Should().Be(WaitlistStatus.Cancelled);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task LeaveWaitlistAsync_WhenNotified_CascadesToNextEligibleEntry()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var notifiedUser = Guid.NewGuid();
        var nextInLineUser = Guid.NewGuid();

        var notifiedEntry = new Waitlist
        {
            BookId = bookId,
            UserId = notifiedUser,
            Status = WaitlistStatus.Notified,
            JoinedAt = DateTime.UtcNow.AddDays(-2),
            NotifiedAt = DateTime.UtcNow.AddHours(-1),
            ClaimDeadline = DateTime.UtcNow.AddHours(47),
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.WaitlistEntries.Add(notifiedEntry);

        context.WaitlistEntries.Add(new Waitlist
        {
            BookId = bookId,
            UserId = nextInLineUser,
            Status = WaitlistStatus.Waiting,
            JoinedAt = DateTime.UtcNow.AddDays(-1),
            BookTitle = "T",
            BookAuthor = "A"
        });
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        var sut = CreateSut(context, catalogClient);

        var (success, _, _, _) = await sut.LeaveWaitlistAsync(notifiedUser, notifiedEntry.WaitlistId);

        success.Should().BeTrue();

        var nextEntry = await context.WaitlistEntries.FirstAsync(w => w.UserId == nextInLineUser);
        nextEntry.Status.Should().Be(WaitlistStatus.Notified);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task LeaveWaitlistAsync_WhenNotifiedAndQueueIsEmpty_ReleasesAvailabilityBackToGeneralPool()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var notifiedUser = Guid.NewGuid();

        var notifiedEntry = new Waitlist
        {
            BookId = bookId,
            UserId = notifiedUser,
            Status = WaitlistStatus.Notified,
            JoinedAt = DateTime.UtcNow.AddDays(-2),
            NotifiedAt = DateTime.UtcNow.AddHours(-1),
            ClaimDeadline = DateTime.UtcNow.AddHours(47),
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.WaitlistEntries.Add(notifiedEntry);
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var sut = CreateSut(context, catalogClient);

        await sut.LeaveWaitlistAsync(notifiedUser, notifiedEntry.WaitlistId);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(bookId, 1), Times.Once);
    }
}