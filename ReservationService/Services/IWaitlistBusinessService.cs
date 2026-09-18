using ReservationService.Models.Dtos;

namespace ReservationService.Services;

public interface IWaitlistBusinessService
{
    Task<(bool Success, WaitlistResponse? Data, string? ErrorCode, string? ErrorMessage)>
        JoinWaitlistAsync(Guid userId, Guid bookId);

    Task<MyWaitlistResponse> GetMyWaitlistAsync(Guid userId);

    Task<(bool Success, CancelWaitlistResponse? Data, string? ErrorCode, string? ErrorMessage)>
        LeaveWaitlistAsync(Guid userId, Guid waitlistId);

    Task<bool> TryCascadeToNextEligibleAsync(Guid bookId, string bookTitle, string bookAuthor);
}