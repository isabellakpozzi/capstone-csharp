namespace ReservationService.Services;

public class BookInfo
{
    public Guid BookId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public int AvailableCopies { get; set; }
}

public interface ICatalogServiceClient
{
    Task<BookInfo?> GetBookAsync(Guid bookId);
    Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta);
}