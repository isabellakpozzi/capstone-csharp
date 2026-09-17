using System.Text.RegularExpressions;

namespace UserService.Services;

public class PasswordValidator : IPasswordValidator
{
    public (bool IsValid, string? ErrorMessage) Validate(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return (false, "Password must be at least 8 characters long");

        if (!Regex.IsMatch(password, "[A-Z]"))
            return (false, "Password must contain at least one uppercase letter");

        if (!Regex.IsMatch(password, "[a-z]"))
            return (false, "Password must contain at least one lowercase letter");

        if (!Regex.IsMatch(password, "[0-9]"))
            return (false, "Password must contain at least one number");

        if (!Regex.IsMatch(password, @"[!@#$%^&*()_+\-=\[\]{}|;:,.<>?]"))
            return (false, "Password must contain at least one special character");

        return (true, null);
    }
}