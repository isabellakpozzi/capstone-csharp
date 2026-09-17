namespace ReservationService.Models.Dtos;

public class JoinWaitlistRequest
{
    public Guid BookId { get; set; }
}

public class WaitlistResponse
{
    public Guid WaitlistId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int? Position { get; set; }
}

public class WaitlistEntryItem
{
    public Guid WaitlistId { get; set; }
    public Guid BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string BookAuthor { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int? Position { get; set; }          // WAITING entries only
    public DateTime? NotifiedAt { get; set; }    // NOTIFIED entries only
    public DateTime? ClaimDeadline { get; set; } // NOTIFIED entries only
}

public class MyWaitlistResponse
{
    public List<WaitlistEntryItem> Entries { get; set; } = new();
}

public class CancelWaitlistResponse
{
    public Guid WaitlistId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}