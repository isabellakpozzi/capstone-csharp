using UserService.Models;
using UserService.Models.Dtos;

namespace UserService.Services;

public class AuthResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public static AuthResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static AuthResult<T> Fail(string code, string message) =>
        new() { Success = false, ErrorCode = code, ErrorMessage = message };
}

public interface IAuthService
{
    Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request);
    Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request);
    Task<AuthResult<ProfileResponse>> GetProfileAsync(Guid userId);
    Task<AuthResult<UserValidationResponse>> ValidateUserAsync(Guid userId);
}