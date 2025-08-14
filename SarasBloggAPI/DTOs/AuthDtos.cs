namespace SarasBloggAPI.DTOs;

public record LoginRequest(string UserNameOrEmail, string Password, bool RememberMe);
public record LoginResponse(string AccessToken, DateTime AccessTokenExpiresUtc,
                            string RefreshToken, DateTime RefreshTokenExpiresUtc);

public record MeResponse(string Id, string UserName, string? Email, IEnumerable<string> Roles);
