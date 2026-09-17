using UserService.Models;

namespace UserService.Services;

public interface IJwtTokenService
{
    string GenerateToken(User user);
}