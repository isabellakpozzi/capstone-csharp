using System.Net.Http.Json;

namespace UserService.Services;

public class ReservationServiceClient : IReservationServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReservationServiceClient> _logger;

    public ReservationServiceClient(HttpClient httpClient, ILogger<ReservationServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReservationStatsResponse?> GetStatisticsAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/reservations/statistics/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Reservation Service returned {StatusCode} for statistics of user {UserId}",
                    response.StatusCode, userId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ReservationStatsResponse>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reach Reservation Service for user {UserId} statistics", userId);
            return null;
        }
    }
}