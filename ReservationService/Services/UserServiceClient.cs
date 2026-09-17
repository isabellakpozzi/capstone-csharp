using System.Net;
using System.Net.Http.Json;

namespace ReservationService.Services;

public class UserServiceClient : IUserServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserServiceClient> _logger;

    public UserServiceClient(HttpClient httpClient, ILogger<UserServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool Success, UserValidationResult? User, string? ErrorCode, string? ErrorMessage)> ValidateUserAsync(Guid userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/users/{userId}/validate");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return (false, null, "NOT_FOUND", "User not found");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("User Service validation failed for {UserId}: {StatusCode} {Body}",
                    userId, response.StatusCode, body);
                return (false, null, "VALIDATION_ERROR", "User validation failed");
            }

            var user = await response.Content.ReadFromJsonAsync<UserValidationResult>();
            return (true, user, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach User Service for user {UserId}", userId);
            return (false, null, "SERVICE_UNAVAILABLE", "User Service is unavailable");
        }
    }
}