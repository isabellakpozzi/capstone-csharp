using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Models.Dtos;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationBusinessService _reservationService;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(
        IReservationBusinessService reservationService,
        ILogger<ReservationsController> logger)
    {
        _reservationService = reservationService;
        _logger = logger;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid or missing userId claim");

        return userId;
    }

    private bool IsLibrarian()
    {
        return User.IsInRole("Librarian");
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request)
    {
        var userId = GetUserId();

        var (success, data, errorCode, errorMessage, currentCount) =
            await _reservationService.CreateReservationAsync(userId, request.BookId);

        if (!success)
        {
            var timestamp = DateTime.UtcNow;

            return errorCode switch
            {
                "RESERVATION_LIMIT_EXCEEDED" => BadRequest(new
                {
                    error = errorCode,
                    message = errorMessage,
                    currentReservations = currentCount
                }),
                "BOOK_UNAVAILABLE" => BadRequest(new
                {
                    error = errorCode,
                    message = errorMessage,
                    availableCopies = 0
                }),
                "NOT_FOUND" => NotFound(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                _ => StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = errorMessage ?? "An unexpected error occurred", Timestamp = timestamp })
            };
        }

        return CreatedAtAction(nameof(GetActiveReservations), null, data);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetActiveReservations()
    {
        var userId = GetUserId();
        var result = await _reservationService.GetActiveReservationsAsync(userId);
        return Ok(result);
    }

    [HttpPost("{reservationId}/checkout")]
    [Authorize(Roles = "Librarian")]
    public async Task<IActionResult> Checkout(Guid reservationId, [FromBody] CheckoutRequest request)
    {
        var (success, data, errorCode, errorMessage) =
            await _reservationService.CheckoutAsync(reservationId, request.Notes);

        if (!success)
        {
            var timestamp = DateTime.UtcNow;
            return errorCode switch
            {
                "NOT_FOUND" => NotFound(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                "INVALID_STATUS" => BadRequest(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                _ => StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = errorMessage ?? "An unexpected error occurred", Timestamp = timestamp })
            };
        }

        return Ok(data);
    }

    [HttpPost("{reservationId}/return")]
    [Authorize(Roles = "Librarian")]
    public async Task<IActionResult> Return(Guid reservationId, [FromBody] ReturnRequest request)
    {
        var (success, data, errorCode, errorMessage) =
            await _reservationService.ReturnAsync(reservationId, request.Condition, request.Notes);

        if (!success)
        {
            var timestamp = DateTime.UtcNow;
            return errorCode switch
            {
                "NOT_FOUND" => NotFound(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                "INVALID_STATUS" => BadRequest(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                "VALIDATION_ERROR" => BadRequest(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                _ => StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = errorMessage ?? "An unexpected error occurred", Timestamp = timestamp })
            };
        }

        return Ok(data);
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        var userId = GetUserId();
        var result = await _reservationService.GetHistoryAsync(userId, page, size);
        return Ok(result);
    }

    // Internal endpoint 
    [HttpGet("statistics/{userId}")]
    public async Task<IActionResult> GetStatistics(Guid userId)
    {
        var result = await _reservationService.GetStatisticsAsync(userId);
        return Ok(result);
    }
}