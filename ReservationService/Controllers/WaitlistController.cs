using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReservationService.Models.Dtos;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly IWaitlistBusinessService _waitlistService;

    public WaitlistController(IWaitlistBusinessService waitlistService)
    {
        _waitlistService = waitlistService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid or missing userId claim");

        return userId;
    }

    [HttpPost]
    public async Task<IActionResult> JoinWaitlist([FromBody] JoinWaitlistRequest request)
    {
        var userId = GetUserId();

        var (success, data, errorCode, errorMessage) =
            await _waitlistService.JoinWaitlistAsync(userId, request.BookId);

        if (!success)
        {
            var timestamp = DateTime.UtcNow;
            return errorCode switch
            {
                "NOT_FOUND" => NotFound(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                "BOOK_AVAILABLE" => BadRequest(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                "ALREADY_WAITLISTED" => BadRequest(new ErrorResponse { Error = errorCode, Message = errorMessage!, Timestamp = timestamp }),
                _ => StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = errorMessage ?? "An unexpected error occurred", Timestamp = timestamp })
            };
        }

        return StatusCode(201, data);
    }

    [HttpGet]
    public async Task<IActionResult> GetMyWaitlist()
    {
        var userId = GetUserId();
        var result = await _waitlistService.GetMyWaitlistAsync(userId);
        return Ok(result);
    }

    [HttpDelete("{waitlistId}")]
    public async Task<IActionResult> LeaveWaitlist(Guid waitlistId)
    {
        var userId = GetUserId();

        var (success, data, errorCode, errorMessage) =
            await _waitlistService.LeaveWaitlistAsync(userId, waitlistId);

        if (!success)
        {
            return NotFound(new ErrorResponse
            {
                Error = errorCode!,
                Message = errorMessage!,
                Timestamp = DateTime.UtcNow
            });
        }

        return Ok(data);
    }
}