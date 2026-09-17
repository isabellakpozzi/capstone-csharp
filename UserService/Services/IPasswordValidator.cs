namespace UserService.Services;

public interface IPasswordValidator
{
    (bool IsValid, string? ErrorMessage) Validate(string password);
}