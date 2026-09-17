using System.Net;
using System.Net.Http.Json;

namespace ReservationService.Services;

public class CatalogServiceClient : ICatalogServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CatalogServiceClient> _logger;

    public CatalogServiceClient(HttpClient httpClient, ILogger<CatalogServiceClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BookInfo?> GetBookAsync(Guid bookId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/catalog/books/{bookId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Catalog Service returned {StatusCode} for book {BookId}",
                    response.StatusCode, bookId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<BookInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach Catalog Service for book {BookId}", bookId);
            return null;
        }
    }

    public async Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(
                $"/api/catalog/books/{bookId}/availability",
                new { Delta = delta });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to update availability for book {BookId} by {Delta}: {StatusCode}",
                    bookId, delta, response.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reach Catalog Service to update availability for book {BookId}", bookId);
            return false;
        }
    }
}