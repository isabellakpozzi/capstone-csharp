using System.ComponentModel.DataAnnotations;

namespace ReservationService.Models;

public class Waitlist
{
    [Key]
    public Guid WaitlistId { get; set; } = Guid.NewGuid();

    public Guid BookId { get; set; }
    public Guid UserId { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;

    public DateTime JoinedAt { get; set; }
    public DateTime? NotifiedAt { get; set; }
    public DateTime? ClaimDeadline { get; set; }

    public Guid? ResultingReservationId { get; set; }

    [MaxLength(255)]
    public string BookTitle { get; set; } = string.Empty;

    [MaxLength(255)]
    public string BookAuthor { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}