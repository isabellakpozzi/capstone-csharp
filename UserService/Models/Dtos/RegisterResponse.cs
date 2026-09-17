namespace UserService.Models.Dtos;

public class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;           // "PATRON"
    public string MembershipStatus { get; set; } = string.Empty; // "ACTIVE"
    public DateTime CreatedAt { get; set; }
    public string Message { get; set; } = "Registration successful";
}