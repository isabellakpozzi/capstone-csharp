using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public class WaitlistExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaitlistExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public WaitlistExpiryBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<WaitlistExpiryBackgroundService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        // configurable for easy testing without touching codebase
        var intervalSetting = configuration["WaitlistExpiry:IntervalMinutes"];
        var minutes = int.TryParse(intervalSetting, out var parsed) ? parsed : 60;
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Waitlist expiry background service started. Running every {Interval}.", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredEntriesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing waitlist expiry");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessExpiredEntriesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
        var catalogClient = scope.ServiceProvider.GetRequiredService<ICatalogServiceClient>();
        var waitlistService = scope.ServiceProvider.GetRequiredService<IWaitlistBusinessService>();

        var now = DateTime.UtcNow;

        var expiredEntries = await context.WaitlistEntries
            .Where(w => w.Status == WaitlistStatus.Notified && w.ClaimDeadline < now)
            .ToListAsync(stoppingToken);

        if (expiredEntries.Count == 0)
        {
            _logger.LogInformation("Waitlist expiry check: no expired claims found.");
            return;
        }

        _logger.LogInformation("Waitlist expiry check: found {Count} expired claim(s).", expiredEntries.Count);

        foreach (var entry in expiredEntries)
        {
            entry.Status = WaitlistStatus.Expired;
            await context.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "Expired waitlist entry {WaitlistId} for user {UserId}, book {BookId} (claim deadline {Deadline} passed)",
                entry.WaitlistId, entry.UserId, entry.BookId, entry.ClaimDeadline);

            var handedOff = await waitlistService.TryCascadeToNextEligibleAsync(
                entry.BookId, entry.BookTitle, entry.BookAuthor);

            if (handedOff)
            {
                _logger.LogInformation(
                    "Cascaded book {BookId} to next eligible waitlist entry after expiry of {WaitlistId}",
                    entry.BookId, entry.WaitlistId);
            }
            else
            {
                await catalogClient.UpdateAvailabilityAsync(entry.BookId, +1);
                _logger.LogInformation(
                    "No eligible waitlist entry for book {BookId}; released copy back to general availability",
                    entry.BookId);
            }
        }
    }
}