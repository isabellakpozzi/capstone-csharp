using Microsoft.AspNetCore.Mvc;
using UserService.Models.Dtos;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);

        if (!result.Success)
        {
            return BadRequest(new ErrorResponse
            {
                Error = result.ErrorCode!,
                Message = result.ErrorMessage!,
                Timestamp = DateTime.UtcNow
            });
        }

        return StatusCode(201, result.Data);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (!result.Success)
        {
            return Unauthorized(new ErrorResponse
            {
                Error = result.ErrorCode!,
                Message = result.ErrorMessage!,
                Timestamp = DateTime.UtcNow
            });
        }

        return Ok(result.Data);
    }
}