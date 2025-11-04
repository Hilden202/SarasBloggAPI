using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;
using System.Text;

namespace SarasBloggAPI.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _cfg;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<AuthController> _logger;

    public AuthController(SignInManager<ApplicationUser> signInManager,
                          UserManager<ApplicationUser> userManager,
                          TokenService tokenService,
                          IConfiguration cfg,
                          IEmailSender emailSender,
                          ILogger<AuthController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tokenService = tokenService;
        _cfg = cfg;
        _emailSender = emailSender;
        _logger = logger;
    }

    // ---------- REGISTER ----------
    [AllowAnonymous]
    [HttpPost("register")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BasicResultDto>> Register([FromBody] RegisterRequestDto dto)
    {
        if (dto is null)
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Invalid payload" });

        if (string.IsNullOrWhiteSpace(dto.UserName))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Username is required" });

        if (string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Email is required" });

        if (string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Password is required" });

        if (dto.BirthYear is < 1900 or > 2100)
            dto.BirthYear = null;

        var user = new ApplicationUser
        {
            UserName = dto.UserName,
            Email = dto.Email,
            EmailConfirmed = false,
            Name = dto.Name,
            BirthYear = dto.BirthYear
        };

        user.NotifyOnNewPost = dto.SubscribeNewPosts;

        // Unik e-post?
        var normEmail = _userManager.NormalizeEmail(dto.Email);
        var emailInUse = await _userManager.Users
            .AnyAsync(u => u.NormalizedEmail == normEmail);
        if (emailInUse)
            return Conflict(new BasicResultDto
            {
                Succeeded = false,
                Message = "Kunde inte skapa konto. Prova en annan e-post eller logga in."
            });

        // Unikt användarnamn?
        var normName = _userManager.NormalizeName(dto.UserName);
        var userNameInUse = await _userManager.Users
            .AnyAsync(u => u.NormalizedUserName == normName);
        if (userNameInUse)
            return Conflict(new BasicResultDto
            {
                Succeeded = false,
                Message = "Användarnamnet är upptaget."
            });


        var create = await _userManager.CreateAsync(user, dto.Password);
        if (!create.Succeeded)
        {
            var msg = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        if (!await _userManager.IsInRoleAsync(user, "user"))
        {
            await _userManager.AddToRoleAsync(user, "user");
        }

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var codeBytes = Encoding.UTF8.GetBytes(code);
        var codeEncoded = WebEncoders.Base64UrlEncode(codeBytes);

        var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
        var confirmUrl = $"{frontendBase}/Identity/Account/ConfirmEmail?userId={user.Id}&code={codeEncoded}";

        var expose = _cfg.GetValue("Auth:ExposeConfirmLinkInResponse", false);
        var mode = _cfg["Email:Mode"] ?? "Dev";

        try
        {
            if (mode.Equals("Prod", StringComparison.OrdinalIgnoreCase))
            {
                var subject = "Bekräfta din e-post till Med Hjärtat som Kompass";
                var html = $@"<p>Hej!</p>
                      <p>Bekräfta din e-post genom att klicka på länken nedan:</p>
                      <p><a href=""{confirmUrl}"">Bekräfta min e-post</a></p>
                      <p>Hälsningar,<br/>Med Hjärtat som Kompass</p>";

                await _emailSender.SendAsync(user.Email!, subject, html);
                _logger.LogInformation("Register: email queued to {Email}", user.Email);
            }
            else
            {
                expose = true;
                _logger.LogInformation("Register: dev mode, exposing confirm link");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Register: email send failed to {Email}", user.Email);
            expose = true;
        }

        return Ok(new BasicResultDto
        {
            Succeeded = true,
            Message = expose ? "User created (dev/test mode)" : "User created. Check your email.",
            ConfirmEmailUrl = expose ? confirmUrl : null
        });
    }

    // ---------- LOGIN ----------
    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByNameAsync(req.UserNameOrEmail)
                   ?? await _userManager.FindByEmailAsync(req.UserNameOrEmail);

        if (user is null)
            return Unauthorized("Invalid credentials.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized("Invalid credentials.");

        if (!await _userManager.IsEmailConfirmedAsync(user))
            return Unauthorized("Email not confirmed.");

        var access = await _tokenService.CreateAccessTokenAsync(user);
        var accessExp = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:AccessTokenMinutes"] ?? "60"));
        var (refresh, refreshExp) = _tokenService.CreateRefreshToken();

        return new LoginResponse(access, accessExp, refresh, refreshExp);
    }

    // ---------- LOGOUT ----------
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Ok(new { message = "Logged out" });
    }

    // ---------- ME ----------
    [Authorize]
    // gamla route (behåll funktionell, men göm i Swagger)
    [HttpGet("me")]
    [ApiExplorerSettings(IgnoreApi = true)]
    // ny “snygg” alias-route som visas i Swagger
    [HttpGet("~/api/users/me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var userName = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
            return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        var phone = await _userManager.GetPhoneNumberAsync(user);

        return new MeResponse(
            Id: user.Id,
            UserName: user.UserName ?? "",
            Email: user.Email,
            Name: user.Name,
            BirthYear: user.BirthYear,
            PhoneNumber: phone,
            EmailConfirmed: user.EmailConfirmed,
            Roles: roles,
            NotifyOnNewPost: user.NotifyOnNewPost
        );

    }


    // ---------- CONFIRM EMAIL ----------
    [AllowAnonymous]
    [HttpPost("confirm-email")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BasicResultDto>> ConfirmEmail([FromBody] ConfirmEmailRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.UserId) || string.IsNullOrWhiteSpace(dto.Code))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "UserId and Code are required" });

        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null)
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Invalid user" });

        var decodedBytes = WebEncoders.Base64UrlDecode(dto.Code);
        var decodedCode = Encoding.UTF8.GetString(decodedBytes);

        var result = await _userManager.ConfirmEmailAsync(user, decodedCode);
        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        return Ok(new BasicResultDto { Succeeded = true, Message = "Email confirmed successfully" });
    }

    // ---------- RESEND CONFIRMATION ----------
    [AllowAnonymous]
    [HttpPost("resend-confirmation")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BasicResultDto>> ResendConfirmation([FromBody] EmailDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
            return Ok(new BasicResultDto { Succeeded = true, Message = "If the email exists, a confirmation link was sent." });

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || await _userManager.IsEmailConfirmedAsync(user))
            return Ok(new BasicResultDto { Succeeded = true, Message = "If the email exists, a confirmation link was sent." });

        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var codeEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
        var confirmUrl = $"{frontendBase}/Identity/Account/ConfirmEmail?userId={user.Id}&code={codeEncoded}";

        await _emailSender.SendAsync(
            to: user.Email!,
            subject: "Bekräfta din e-post till Med Hjärtat som Kompass",
            htmlBody: $@"<p>Hej {user.UserName},</p>
                 <p>Bekräfta din e-post genom att klicka här:</p>
                 <p><a href=""{confirmUrl}"">Bekräfta e-post</a></p>");

        var expose = _cfg.GetValue("Auth:ExposeConfirmLinkInResponse", true);
        return Ok(new BasicResultDto
        {
            Succeeded = true,
            Message = expose ? "Bekräftelselänk skapad (dev)." : "Om adressen finns skickades en bekräftelselänk.",
            ConfirmEmailUrl = expose ? confirmUrl : null
        });
    }

    // ---------- FORGOT PASSWORD ----------
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<BasicResultDto>> ForgotPassword([FromBody] EmailDto dto)
    {
        const string neutralMsg = "If the email exists, a reset link was sent.";

        if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
            return Ok(new BasicResultDto { Succeeded = true, Message = neutralMsg });

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
            return Ok(new BasicResultDto { Succeeded = true, Message = neutralMsg });

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
        var resetPath = "/Identity/Account/ResetPassword";
        var resetUrl = QueryHelpers.AddQueryString(
            $"{frontendBase.TrimEnd('/')}{resetPath}",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["token"] = tokenEncoded
            });

        var subject = "Återställ lösenord";
        var html = $@"
        <p>Hej {System.Net.WebUtility.HtmlEncode(user.UserName)},</p>
        <p>Klicka på länken nedan för att välja ett nytt lösenord:</p>
        <p><a href=""{resetUrl}"">Återställ lösenord</a></p>
        <p>Om du inte begärt detta kan du ignorera mejlet.</p>
        <p>Hälsningar,<br/>Med Hjärtat som Kompass</p>";

        var mode = _cfg["Email:Mode"] ?? "Dev";
        var exposeFallbackLink = _cfg.GetValue("Auth:ExposeConfirmLinkInResponse", true);

        if (!mode.Equals("Prod", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("ForgotPassword: dev/test mode, exposing reset link");
            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = "Reset link generated (dev/test).",
                ConfirmEmailUrl = resetUrl
            });
        }

        try
        {
            await _emailSender.SendAsync(user.Email!, subject, html);
            _logger.LogInformation("ForgotPassword: email queued to {Email}", user.Email);

            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = neutralMsg
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ForgotPassword: email send failed to {Email}", user.Email);

            var expose = exposeFallbackLink;
            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = expose ? "Reset link generated (fallback)." : neutralMsg,
                ConfirmEmailUrl = expose ? resetUrl : null
            });
        }
    }

    // ---------- RESET PASSWORD ----------
    [AllowAnonymous]
    [HttpPost("reset-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BasicResultDto>> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.UserId) ||
            string.IsNullOrWhiteSpace(dto.Token) || string.IsNullOrWhiteSpace(dto.NewPassword))
        {
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Invalid payload" });
        }

        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null)
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Invalid user" });

        var tokenDecoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token));
        var result = await _userManager.ResetPasswordAsync(user, tokenDecoded, dto.NewPassword);

        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        // ✔ Rensa ev. låsning/failed count efter lyckad reset (annars kan inlogg nekas ändå)
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);

        return Ok(new BasicResultDto { Succeeded = true, Message = "Password reset successfully" });
    }

    // ---------- CHANGE PASSWORD ----------
    [Authorize]
    // gamla route (behåll funktionell, men göm i Swagger)
    [HttpPost("change-password")]
    [ApiExplorerSettings(IgnoreApi = true)]
    // ny alias-route som visas i Swagger
    [HttpPost("~/api/users/me/change-password")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BasicResultDto>> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Current and new password are required." });

        var userName = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Not authenticated." });

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null)
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "User not found." });

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        // Valfritt men bra: rensa ev. låsning och uppdatera säkerhetsstämplar
        await _userManager.ResetAccessFailedCountAsync(user);
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _signInManager.RefreshSignInAsync(user); // för cookie-scenarier; har ingen nackdel med JWT

        return Ok(new BasicResultDto { Succeeded = true, Message = "Password changed successfully." });
    }

    [Authorize]
    [HttpPost("set-password")]
    [HttpPost("~/api/users/me/set-password")] // alias
    [Consumes("application/json")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BasicResultDto>> SetPassword([FromBody] SetPasswordDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "New password is required." });

        var userName = User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized();

        var user = await _userManager.FindByNameAsync(userName);
        if (user is null) return BadRequest(new BasicResultDto { Succeeded = false, Message = "User not found." });

        var hasPassword = await _userManager.HasPasswordAsync(user);
        if (hasPassword)
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "User already has a password." });

        var result = await _userManager.AddPasswordAsync(user, dto.NewPassword);
        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        await _signInManager.RefreshSignInAsync(user);
        return Ok(new BasicResultDto { Succeeded = true, Message = "Password set successfully." });
    }

    // --- CHANGE EMAIL: START ---
    [Authorize]
    [HttpPost("change-email/start")]
    [HttpPost("~/api/users/me/change-email/start")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BasicResultDto>> ChangeEmailStart([FromBody] ChangeEmailStartDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto?.NewEmail))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "New email is required." });

        var userName = User?.Identity?.Name;
        var user = string.IsNullOrEmpty(userName) ? null : await _userManager.FindByNameAsync(userName);
        if (user is null) return Unauthorized();

        // valfritt: kontrollera om e-posten redan används
        var exists = await _userManager.FindByEmailAsync(dto.NewEmail);
        if (exists is not null && exists.Id != user.Id)
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "Email already in use." });

        var token = await _userManager.GenerateChangeEmailTokenAsync(user, dto.NewEmail);
        var codeEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
        var confirmUrl = $"{frontendBase}/Identity/Account/ConfirmEmailChange?userId={user.Id}&code={codeEncoded}&email={Uri.EscapeDataString(dto.NewEmail)}";

        // Skicka mejl (Prod) eller exponera länk (Dev)
        var expose = _cfg.GetValue("Auth:ExposeConfirmLinkInResponse", true);
        try
        {
            var subject = "Bekräfta byte av e-post";
            var html = $@"<p>Hej {System.Net.WebUtility.HtmlEncode(user.UserName)},</p>
                      <p>Klicka för att bekräfta ny e-post: <a href=""{confirmUrl}"">Bekräfta e-post</a></p>";
            await _emailSender.SendAsync(dto.NewEmail, subject, html);
        }
        catch { /* vid dev kan vi exponera */ }

        return Ok(new BasicResultDto { Succeeded = true, Message = "Confirmation sent.", ConfirmEmailUrl = expose ? confirmUrl : null });
    }

    // --- CHANGE EMAIL: CONFIRM ---
    [AllowAnonymous]
    [HttpPost("change-email/confirm")]
    [HttpPost("~/api/users/change-email/confirm")]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BasicResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BasicResultDto>> ChangeEmailConfirm([FromBody] ChangeEmailConfirmDto dto, [FromQuery] string? newEmail)
    {
        if (string.IsNullOrWhiteSpace(dto?.UserId) || string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(newEmail))
            return BadRequest(new BasicResultDto { Succeeded = false, Message = "UserId, Code and newEmail are required." });

        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null) return BadRequest(new BasicResultDto { Succeeded = false, Message = "Invalid user." });

        var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Code));
        var result = await _userManager.ChangeEmailAsync(user, newEmail, code);
        if (!result.Succeeded)
        {
            var msg = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        // sätt även UserName om du vill spegla e-posten: await _userManager.SetUserNameAsync(user, newEmail);
        return Ok(new BasicResultDto { Succeeded = true, Message = "Email changed." });
    }

    // ---------- SEND RESET LINK (SUPERADMIN) ----------
    [Authorize(Roles = "superadmin")]
    [HttpPost("send-reset-link")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendResetLink([FromBody] EmailDto dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { sent = false, message = "Email required" });

        // Förhindra att systemkontot hanteras
        if (dto.Email.Equals("admin@sarasblogg.se", StringComparison.OrdinalIgnoreCase))
            return Forbid();

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null)
            return NotFound(new { sent = false, message = "User not found" });

        // Skapa återställningslänk
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";
        var resetUrl = $"{frontendBase}/Identity/Account/ResetPassword?userId={user.Id}&token={tokenEncoded}";

        var mode = _cfg["Email:Mode"] ?? "Dev";
        if (!mode.Equals("Prod", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("SendResetLink: dev/test mode, exposing reset link");
            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = "Reset link generated (dev/test mode).",
                ConfirmEmailUrl = resetUrl
            });
        }
        // 📧 Prod-läge → försök skicka via SendGrid
        _logger.LogInformation("SendResetLink: prod mode, attempting email send...");
        try
        {
            var subject = "Återställ lösenord (initierad av administratör)";
            var html = $@"<p>Hej {System.Net.WebUtility.HtmlEncode(user.UserName)},</p>
                  <p>En administratör har initierat en återställning av ditt lösenord.</p>
                  <p>Klicka här för att återställa det:</p>
                  <p><a href=""{resetUrl}"">Återställ lösenord</a></p>
                  <p>Hälsningar,<br/>Med Hjärtat som Kompass</p>";

            await _emailSender.SendAsync(user.Email!, subject, html);
            _logger.LogInformation("SendResetLink: email queued to {Email}", user.Email);

            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = "Reset link sent to user."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendResetLink: email send failed to {Email}", user.Email);
            // Fallback → returnera länken så att admin kan skicka manuellt
            return Ok(new BasicResultDto
            {
                Succeeded = true,
                Message = "Reset link generated (fallback mode).",
                ConfirmEmailUrl = resetUrl
            });
        }
    }

}
