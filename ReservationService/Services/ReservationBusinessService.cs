using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Models.Dtos;

namespace ReservationService.Services;

public class ReservationBusinessService : IReservationBusinessService
{
    private const int MaxActiveReservations = 5;
    private const int ReservationExpiryDays = 7;
    private const int CheckoutPeriodDays = 14;
    private const decimal LateFeePerDay = 1.00m;

    private readonly ReservationServiceContext _context;
    private readonly IUserServiceClient _userServiceClient;
    private readonly ICatalogServiceClient _catalogServiceClient;

    private readonly IWaitlistBusinessService _waitlistBusinessService;
    private readonly ILogger<ReservationBusinessService> _logger;

    public ReservationBusinessService(
        ReservationServiceContext context,
        IUserServiceClient userServiceClient,
        ICatalogServiceClient catalogServiceClient,
        IWaitlistBusinessService waitlistBusinessService,
        ILogger<ReservationBusinessService> logger)
    {
        _context = context;
        _userServiceClient = userServiceClient;
        _catalogServiceClient = catalogServiceClient;
        _waitlistBusinessService = waitlistBusinessService;
        _logger = logger;
    }

    public async Task<(bool Success, ReservationResponse? Data, string? ErrorCode, string? ErrorMessage, int? CurrentCount)>
        CreateReservationAsync(Guid userId, Guid bookId)
    {
        // 1. Check user's current active reservation count directly from our own DB 
        var activeCount = await _context.Reservations.CountAsync(r =>
            r.UserId == userId &&
            (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

        if (activeCount >= MaxActiveReservations)
            return (false, null, "RESERVATION_LIMIT_EXCEEDED",
                "You have reached the maximum of 5 active reservations", activeCount);

        // 2. Check book exists and has availability via Catalog Service
        var book = await _catalogServiceClient.GetBookAsync(bookId);
        if (book is null)
            return (false, null, "NOT_FOUND", "Book not found", null);

        if (book.AvailableCopies <= 0)
            return (false, null, "BOOK_UNAVAILABLE", "No copies available for reservation", null);

        // 3. Decrement availability BEFORE creating our own record, so we never
        //    show a reservation for a book we failed to actually claim a copy of
        var decremented = await _catalogServiceClient.UpdateAvailabilityAsync(bookId, -1);
        if (!decremented)
            return (false, null, "SERVICE_UNAVAILABLE", "Could not update book availability", null);

        var now = DateTime.UtcNow;
        var reservation = new Reservation
        {
            BookId = bookId,
            UserId = userId,
            Status = ReservationStatus.Reserved,
            ReservedAt = now,
            ExpiresAt = now.AddDays(ReservationExpiryDays),
            BookTitle = book.Title,
            BookAuthor = book.Author
        };

        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();

        return (true, new ReservationResponse
        {
            ReservationId = reservation.ReservationId,
            BookId = reservation.BookId,
            UserId = reservation.UserId,
            BookTitle = reservation.BookTitle,
            Status = "RESERVED",
            ReservedAt = reservation.ReservedAt,
            ExpiresAt = reservation.ExpiresAt,
            Message = "Book reserved successfully. Please pick up within 7 days."
        }, null, null, null);
    }

    public async Task<ActiveReservationsResponse> GetActiveReservationsAsync(Guid userId)
    {
        var now = DateTime.UtcNow;

        var reservations = await _context.Reservations
            .Where(r => r.UserId == userId &&
                (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut))
            .OrderBy(r => r.ReservedAt)
            .ToListAsync();

        var items = reservations.Select(r => new ActiveReservationItem
        {
            ReservationId = r.ReservationId,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            Status = r.Status.ToString().ToUpper(),
            ReservedAt = r.ReservedAt,
            ExpiresAt = r.ExpiresAt,
            DaysUntilExpiry = r.Status == ReservationStatus.Reserved && r.ExpiresAt.HasValue
                ? Math.Max(0, (int)Math.Ceiling((r.ExpiresAt.Value - now).TotalDays))
                : null,
            CheckedOutAt = r.CheckedOutAt,
            DueDate = r.DueDate,
            DaysUntilDue = r.Status == ReservationStatus.CheckedOut && r.DueDate.HasValue
                ? Math.Max(0, (int)Math.Ceiling((r.DueDate.Value - now).TotalDays))
                : null
        }).ToList();

        return new ActiveReservationsResponse
        {
            Reservations = items,
            TotalActive = items.Count
        };
    }

    public async Task<(bool Success, CheckoutResponse? Data, string? ErrorCode, string? ErrorMessage)>
        CheckoutAsync(Guid reservationId, string? notes)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation is null)
            return (false, null, "NOT_FOUND", "Reservation not found");

        if (reservation.Status != ReservationStatus.Reserved)
            return (false, null, "INVALID_STATUS", "Can only checkout reservations with RESERVED status");

        var now = DateTime.UtcNow;
        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = now;
        reservation.DueDate = now.AddDays(CheckoutPeriodDays);
        reservation.Notes = notes;

        await _context.SaveChangesAsync();

        return (true, new CheckoutResponse
        {
            ReservationId = reservation.ReservationId,
            Status = "CHECKED_OUT",
            CheckedOutAt = reservation.CheckedOutAt.Value,
            DueDate = reservation.DueDate.Value,
            Message = $"Book checked out successfully. Due date: {reservation.DueDate.Value:MMMM d, yyyy}"
        }, null, null);
    }

    public async Task<(bool Success, ReturnResponse? Data, string? ErrorCode, string? ErrorMessage)>
        ReturnAsync(Guid reservationId, string condition, string? notes)
    {
        var reservation = await _context.Reservations.FindAsync(reservationId);
        if (reservation is null)
            return (false, null, "NOT_FOUND", "Reservation not found");

        if (reservation.Status != ReservationStatus.CheckedOut)
            return (false, null, "INVALID_STATUS", "Can only return books with CHECKED_OUT status");

        if (!Enum.TryParse<BookCondition>(condition, ignoreCase: true, out var parsedCondition))
            return (false, null, "VALIDATION_ERROR", "Invalid condition value");

        var now = DateTime.UtcNow;
        var dueDate = reservation.DueDate!.Value;

        int lateDays = 0;
        decimal lateFee = 0.00m;
        if (now > dueDate)
        {
            lateDays = (int)Math.Ceiling((now - dueDate).TotalDays);
            lateFee = lateDays * LateFeePerDay;
        }

        reservation.Status = ReservationStatus.Returned;
        reservation.ReturnedAt = now;
        reservation.Condition = parsedCondition;
        reservation.Notes = notes;
        reservation.LateDays = lateDays;
        reservation.LateFee = lateFee;

        await _context.SaveChangesAsync();

        var handedToWaitlist = await _waitlistBusinessService.TryCascadeToNextEligibleAsync(
            reservation.BookId, reservation.BookTitle, reservation.BookAuthor);

        if (!handedToWaitlist)
        {
            await _catalogServiceClient.UpdateAvailabilityAsync(reservation.BookId, +1);
        }

        return (true, new ReturnResponse
        {
            ReservationId = reservation.ReservationId,
            ReturnedAt = reservation.ReturnedAt.Value,
            DueDate = dueDate,
            LateDays = lateDays,
            LateFee = lateFee,
            Message = lateFee > 0
                ? $"Book returned. Late fee of ${lateFee:F2} applied to account."
                : "Book returned successfully"
        }, null, null);
    }

    public async Task<PaginatedHistoryResponse> GetHistoryAsync(Guid userId, int page, int size)
    {
        var query = _context.Reservations
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReturnedAt ?? r.ReservedAt);

        var totalElements = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalElements / (double)size);

        var pageItems = await query
            .Skip(page * size)
            .Take(size)
            .ToListAsync();

        var content = pageItems.Select(r => new HistoryItem
        {
            ReservationId = r.ReservationId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            ReservedAt = r.ReservedAt,
            CheckedOutAt = r.CheckedOutAt,
            ReturnedAt = r.ReturnedAt,
            DueDate = r.DueDate,
            Status = r.Status.ToString().ToUpper(),
            WasLate = r.ReturnedAt.HasValue && r.DueDate.HasValue && r.ReturnedAt.Value > r.DueDate.Value
        }).ToList();

        return new PaginatedHistoryResponse
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        };
    }

    public async Task<ReservationStatisticsResponse> GetStatisticsAsync(Guid userId)
    {
        var activeReservations = await _context.Reservations.CountAsync(r =>
            r.UserId == userId &&
            (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

        var borrowingHistory = await _context.Reservations.CountAsync(r =>
            r.UserId == userId && r.Status == ReservationStatus.Returned);

        return new ReservationStatisticsResponse
        {
            UserId = userId,
            ActiveReservations = activeReservations,
            BorrowingHistory = borrowingHistory
        };
    }
}