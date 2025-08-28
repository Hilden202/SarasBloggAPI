namespace SarasBloggAPI.DTOs
{
    // ---- LOGIN / AUTH ----
    public record LoginRequest(string UserNameOrEmail, string Password, bool RememberMe);

    public record LoginResponse(
        string AccessToken,
        DateTime AccessTokenExpiresUtc,
        string RefreshToken,
        DateTime RefreshTokenExpiresUtc
    );

    public record MeResponse(
        string Id,
        string UserName,
        string? Email,
        IEnumerable<string> Roles
    );

    // ---- REGISTER ----
    public sealed class RegisterRequestDto
    {
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
    }


    public sealed class BasicResultDto
    {
        public bool Succeeded { get; set; }
        public string? Message { get; set; }
        public string? ConfirmEmailUrl { get; set; }
    }
}
public sealed class ConfirmEmailRequestDto
{
    public string UserId { get; set; } = "";
    public string Code { get; set; } = "";
}
public record EmailDto(string Email);
public record ResetPasswordDto(string UserId, string Token, string NewPassword);

public sealed class ChangePasswordDto
{
    public string CurrentPassword { get; set; } = "";
    public string NewPassword { get; set; } = "";
}
