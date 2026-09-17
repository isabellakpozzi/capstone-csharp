namespace UserService.Services;

public class ReservationStatsResponse
{
    public Guid UserId { get; set; }
    public int ActiveReservations { get; set; }
    public int BorrowingHistory { get; set; }
}

public interface IReservationServiceClient
{
    Task<ReservationStatsResponse?> GetStatisticsAsync(Guid userId);
}