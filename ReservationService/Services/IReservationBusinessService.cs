using ReservationService.Models.Dtos;

namespace ReservationService.Services;

public interface IReservationBusinessService
{
    Task<(bool Success, ReservationResponse? Data, string? ErrorCode, string? ErrorMessage, int? CurrentCount)>
        CreateReservationAsync(Guid userId, Guid bookId);

    Task<ActiveReservationsResponse> GetActiveReservationsAsync(Guid userId);

    Task<(bool Success, CheckoutResponse? Data, string? ErrorCode, string? ErrorMessage)>
        CheckoutAsync(Guid reservationId, string? notes);

    Task<(bool Success, ReturnResponse? Data, string? ErrorCode, string? ErrorMessage)>
        ReturnAsync(Guid reservationId, string condition, string? notes);

    Task<PaginatedHistoryResponse> GetHistoryAsync(Guid userId, int page, int size);

    Task<ReservationStatisticsResponse> GetStatisticsAsync(Guid userId);
}