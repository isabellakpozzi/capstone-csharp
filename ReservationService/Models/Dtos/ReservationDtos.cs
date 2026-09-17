namespace ReservationService.Models.Dtos;

public class CreateReservationRequest
{
    public Guid BookId { get; set; }
}

public class ReservationResponse
{
    public Guid ReservationId { get; set; }
    public Guid BookId { get; set; }
    public Guid UserId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ActiveReservationItem
{
    public Guid ReservationId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int? DaysUntilDue { get; set; }
}

public class ActiveReservationsResponse
{
    public List<ActiveReservationItem> Reservations { get; set; } = new();
    public int TotalActive { get; set; }
}

public class CheckoutRequest
{
    public string? Notes { get; set; }
}

public class CheckoutResponse
{
    public Guid ReservationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CheckedOutAt { get; set; }
    public DateTime DueDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ReturnRequest
{
    public string Condition { get; set; } = string.Empty; // GOOD, FAIR, POOR, DAMAGED
    public string? Notes { get; set; }
}

public class ReturnResponse
{
    public Guid ReservationId { get; set; }
    public DateTime ReturnedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int LateDays { get; set; }
    public decimal LateFee { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class HistoryItem
{
    public Guid ReservationId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public DateTime ReservedAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool WasLate { get; set; }
}

public class PaginatedHistoryResponse
{
    public List<HistoryItem> Content { get; set; } = new();
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalElements { get; set; }
    public int TotalPages { get; set; }
    public bool Last { get; set; }
}

public class ReservationStatisticsResponse
{
    public Guid UserId { get; set; }
    public int ActiveReservations { get; set; }
    public int BorrowingHistory { get; set; }
}