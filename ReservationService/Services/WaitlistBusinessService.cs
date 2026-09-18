using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Models.Dtos;

namespace ReservationService.Services;

public class WaitlistBusinessService : IWaitlistBusinessService
{
    private const int MaxActiveReservations = 5;
    private const int ClaimWindowHours = 48;

    private readonly ReservationServiceContext _context;
    private readonly ICatalogServiceClient _catalogServiceClient;
    private readonly ILogger<WaitlistBusinessService> _logger;

    public WaitlistBusinessService(
        ReservationServiceContext context,
        ICatalogServiceClient catalogServiceClient,
        ILogger<WaitlistBusinessService> logger)
    {
        _context = context;
        _catalogServiceClient = catalogServiceClient;
        _logger = logger;
    }

    public async Task<(bool Success, WaitlistResponse? Data, string? ErrorCode, string? ErrorMessage)>
        JoinWaitlistAsync(Guid userId, Guid bookId)
    {
        var book = await _catalogServiceClient.GetBookAsync(bookId);
        if (book is null)
            return (false, null, "NOT_FOUND", "Book not found");

        if (book.AvailableCopies > 0)
            return (false, null, "BOOK_AVAILABLE",
                "This book currently has available copies - reserve it directly instead of joining the waitlist");

        var alreadyWaiting = await _context.WaitlistEntries.AnyAsync(w =>
            w.UserId == userId && w.BookId == bookId && w.Status == WaitlistStatus.Waiting);

        if (alreadyWaiting)
            return (false, null, "ALREADY_WAITLISTED", "You are already on the waitlist for this book");

        var now = DateTime.UtcNow;
        var entry = new Waitlist
        {
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now,
            BookTitle = book.Title,
            BookAuthor = book.Author
        };

        _context.WaitlistEntries.Add(entry);
        await _context.SaveChangesAsync();

        var position = await ComputePositionAsync(bookId, entry.JoinedAt);

        return (true, new WaitlistResponse
        {
            WaitlistId = entry.WaitlistId,
            BookId = entry.BookId,
            BookTitle = entry.BookTitle,
            Status = "WAITING",
            JoinedAt = entry.JoinedAt,
            Position = position
        }, null, null);
    }

    public async Task<MyWaitlistResponse> GetMyWaitlistAsync(Guid userId)
    {
        var entries = await _context.WaitlistEntries
            .Where(w => w.UserId == userId &&
                (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .OrderBy(w => w.JoinedAt)
            .ToListAsync();

        var items = new List<WaitlistEntryItem>();

        foreach (var entry in entries)
        {
            var item = new WaitlistEntryItem
            {
                WaitlistId = entry.WaitlistId,
                BookId = entry.BookId,
                BookTitle = entry.BookTitle,
                BookAuthor = entry.BookAuthor,
                Status = entry.Status.ToString().ToUpper(),
                JoinedAt = entry.JoinedAt
            };

            if (entry.Status == WaitlistStatus.Waiting)
            {
                item.Position = await ComputePositionAsync(entry.BookId, entry.JoinedAt);
            }
            else // notified
            {
                item.NotifiedAt = entry.NotifiedAt;
                item.ClaimDeadline = entry.ClaimDeadline;
            }

            items.Add(item);
        }

        return new MyWaitlistResponse { Entries = items };
    }

    public async Task<(bool Success, CancelWaitlistResponse? Data, string? ErrorCode, string? ErrorMessage)>
        LeaveWaitlistAsync(Guid userId, Guid waitlistId)
    {
        var entry = await _context.WaitlistEntries
            .FirstOrDefaultAsync(w => w.WaitlistId == waitlistId && w.UserId == userId);

        if (entry is null)
            return (false, null, "NOT_FOUND", "Waitlist entry not found");

        var wasNotified = entry.Status == WaitlistStatus.Notified;
        entry.Status = WaitlistStatus.Cancelled;
        await _context.SaveChangesAsync();

        if (wasNotified)
        {
            var handedOff = await TryCascadeToNextEligibleAsync(entry.BookId, entry.BookTitle, entry.BookAuthor);
            if (!handedOff)
            {
                await _catalogServiceClient.UpdateAvailabilityAsync(entry.BookId, +1);
            }
        }

        return (true, new CancelWaitlistResponse
        {
            WaitlistId = entry.WaitlistId,
            Status = "CANCELLED",
            Message = "You have been removed from the waitlist"
        }, null, null);
    }

    public async Task<bool> TryCascadeToNextEligibleAsync(Guid bookId, string bookTitle, string bookAuthor)
    {
        while (true)
        {
            var next = await _context.WaitlistEntries
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (next is null)
                return false; // queue exhausted release copy back to general availability

            var activeCount = await _context.Reservations.CountAsync(r =>
                r.UserId == next.UserId &&
                (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

            if (activeCount >= MaxActiveReservations)
            {
                // skip since this patron is over the limit right now
                // expire entry and move onto the next
                next.Status = WaitlistStatus.Expired;
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Waitlist entry {WaitlistId} for user {UserId} expired (over reservation limit) while cascading book {BookId}",
                    next.WaitlistId, next.UserId, bookId);
                continue;
            }

            // eligible so notify
            var now = DateTime.UtcNow;

            var reservation = new Reservation
            {
                BookId = bookId,
                UserId = next.UserId,
                Status = ReservationStatus.Reserved,
                ReservedAt = now,
                ExpiresAt = now.AddDays(7),
                BookTitle = bookTitle,
                BookAuthor = bookAuthor
            };
            _context.Reservations.Add(reservation);

            next.Status = WaitlistStatus.Notified;
            next.NotifiedAt = now;
            next.ClaimDeadline = now.AddHours(ClaimWindowHours);
            next.ResultingReservationId = reservation.ReservationId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Waitlist entry {WaitlistId} for user {UserId} notified for book {BookId}; claim deadline {Deadline}",
                next.WaitlistId, next.UserId, bookId, next.ClaimDeadline);

            return true; // copy handed off do not increment availableCopies
        }
    }

    private async Task<int> ComputePositionAsync(Guid bookId, DateTime joinedAt)
    {
        var aheadCount = await _context.WaitlistEntries.CountAsync(w =>
            w.BookId == bookId &&
            w.Status == WaitlistStatus.Waiting &&
            w.JoinedAt < joinedAt);

        return aheadCount + 1;
    }
}