using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Models;
using UserService.Models.Dtos;

namespace UserService.Services;

public class AuthService : IAuthService
{
    private readonly UserServiceContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IReservationServiceClient _reservationServiceClient;

    public AuthService(
        UserServiceContext context,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        IJwtTokenService jwtTokenService,
        IReservationServiceClient reservationServiceClient)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _jwtTokenService = jwtTokenService;
        _reservationServiceClient = reservationServiceClient;
    }

    public async Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request)
    {
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (emailExists)
            return AuthResult<RegisterResponse>.Fail("VALIDATION_ERROR", "Email already exists");

        var (isValid, errorMessage) = _passwordValidator.Validate(request.Password);
        if (!isValid)
            return AuthResult<RegisterResponse>.Fail("VALIDATION_ERROR", errorMessage!);

        var user = new User
        {
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            Role = Role.Patron,
            MembershipStatus = MembershipStatus.Active,
            MemberSince = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return AuthResult<RegisterResponse>.Ok(new RegisterResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString().ToUpper(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpper(),
            CreatedAt = user.CreatedAt,
            Message = "Registration successful"
        });
    }

    public async Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            return AuthResult<LoginResponse>.Fail("AUTHENTICATION_FAILED", "Invalid email or password");

        var token = _jwtTokenService.GenerateToken(user);

        return AuthResult<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 86400,
            User = new UserSummary
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString().ToUpper()
            }
        });
    }

    public async Task<AuthResult<ProfileResponse>> GetProfileAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            return AuthResult<ProfileResponse>.Fail("NOT_FOUND", "User not found");

        var stats = await _reservationServiceClient.GetStatisticsAsync(userId);

        return AuthResult<ProfileResponse>.Ok(new ProfileResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString().ToUpper(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpper(),
            MemberSince = user.MemberSince,
            ActiveReservations = stats?.ActiveReservations ?? 0,
            BorrowingHistory = stats?.BorrowingHistory ?? 0
        });
    }

    public async Task<AuthResult<UserValidationResponse>> ValidateUserAsync(Guid userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user is null)
            return AuthResult<UserValidationResponse>.Fail("NOT_FOUND", "User not found");

        if (user.MembershipStatus != MembershipStatus.Active)
            return AuthResult<UserValidationResponse>.Fail("VALIDATION_ERROR", "User is suspended");

        var stats = await _reservationServiceClient.GetStatisticsAsync(userId);

        return AuthResult<UserValidationResponse>.Ok(new UserValidationResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString().ToUpper(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpper(),
            ActiveReservationsCount = stats?.ActiveReservations ?? 0
        });
    }
}