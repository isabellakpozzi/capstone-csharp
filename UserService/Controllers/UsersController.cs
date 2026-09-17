using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Models.Dtos;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IAuthService _authService;

    public UsersController(IAuthService authService)
    {
        _authService = authService;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst("userId")?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Invalid or missing userId claim");

        return userId;
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetUserId();
        var result = await _authService.GetProfileAsync(userId);

        if (!result.Success)
        {
            return NotFound(new ErrorResponse
            {
                Error = result.ErrorCode!,
                Message = result.ErrorMessage!,
                Timestamp = DateTime.UtcNow
            });
        }

        return Ok(result.Data);
    }

    // Internal endpoint 
    [HttpGet("{userId}/validate")]
    public async Task<IActionResult> ValidateUser(Guid userId)
    {
        var result = await _authService.ValidateUserAsync(userId);

        if (!result.Success)
        {
            return result.ErrorCode switch
            {
                "NOT_FOUND" => NotFound(new ErrorResponse
                {
                    Error = result.ErrorCode,
                    Message = result.ErrorMessage!,
                    Timestamp = DateTime.UtcNow
                }),
                _ => BadRequest(new ErrorResponse
                {
                    Error = result.ErrorCode!,
                    Message = result.ErrorMessage!,
                    Timestamp = DateTime.UtcNow
                })
            };
        }

        return Ok(result.Data);
    }
}