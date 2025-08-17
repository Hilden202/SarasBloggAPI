using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
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

        var user = new ApplicationUser
        {
            UserName = dto.UserName,
            Email = dto.Email,
            EmailConfirmed = false
        };

        var create = await _userManager.CreateAsync(user, dto.Password);
        if (!create.Succeeded)
        {
            var msg = string.Join("; ", create.Errors.Select(e => $"{e.Code}: {e.Description}"));
            return BadRequest(new BasicResultDto { Succeeded = false, Message = msg });
        }

        if (!await _userManager.IsInRoleAsync(user, "User"))
        {
            await _userManager.AddToRoleAsync(user, "User");
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
                var subject = "Bekräfta din e-post till SarasBlogg";
                var html = $@"<p>Hej!</p>
                      <p>Bekräfta din e-post genom att klicka på länken nedan:</p>
                      <p><a href=""{confirmUrl}"">Bekräfta min e-post</a></p>
                      <p>Hälsningar,<br/>SarasBlogg</p>";

                await _emailSender.SendAsync(user.Email!, subject, html);
                _logger.LogInformation("Register: email queued to {Email}", user.Email);
            }
            else
            {
                // Dev → visa alltid direktlänk i svaret
                expose = true;
                _logger.LogInformation("Register: dev mode, exposing confirm link");
            }
        }
        catch (Exception ex)
        {
            // Kasta inte – exponera länken så vi kan testa ändå
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

        // Om du vill kräva email-konfirmation:
        if (!await _userManager.IsEmailConfirmedAsync(user)) return Unauthorized("Email not confirmed.");

        var access = await _tokenService.CreateAccessTokenAsync(user);
        var accessExp = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:AccessTokenMinutes"] ?? "60"));

        var (refresh, refreshExp) = _tokenService.CreateRefreshToken();
        // TODO: spara refresh-token i DB för rotation/revokering (senare steg)

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
    [HttpGet("me")]
    [ProducesResponseType(typeof(MeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MeResponse>> Me()
    {
        var name = User?.Identity?.Name;
        if (string.IsNullOrEmpty(name))
            return Unauthorized();

        var user = await _userManager.FindByNameAsync(name);
        if (user is null) return Unauthorized();

        var roles = await _userManager.GetRolesAsync(user);
        return new MeResponse(user.Id, user.UserName ?? "", user.Email, roles);
    }

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

        // Decode Base64 URL-encoded code
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
            subject: "Bekräfta din e-post till SarasBlogg",
            htmlBody: $@"<p>Hej {user.UserName},</p>
                 <p>Bekräfta din e-post genom att klicka här:</p>
                 <p><a href=""{confirmUrl}"">Bekräfta e-post</a></p>");

        var expose = _cfg.GetValue("Auth:ExposeConfirmLinkInResponse", true);
        return Ok(new BasicResultDto
        {
            Succeeded = true,
            Message = expose ? "Confirmation link generated (dev)." : "If the email exists, a confirmation link was sent.",
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
        // Samma svar oavsett om e-post finns/är bekräftad (inte läcka info)
        const string neutralMsg = "If the email exists, a reset link was sent.";

        if (dto is null || string.IsNullOrWhiteSpace(dto.Email))
            return Ok(new BasicResultDto { Succeeded = true, Message = neutralMsg });

        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null || !await _userManager.IsEmailConfirmedAsync(user))
            return Ok(new BasicResultDto { Succeeded = true, Message = neutralMsg });

        // 1) Token → Base64Url
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tokenEncoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        // 2) Frontend-bas (ingen trailing slash krävs)
        var frontendBase = _cfg["Frontend:BaseUrl"] ?? "https://sarasblogg.onrender.com";

        // 3) Bygg länk säkert med QueryHelpers
        var resetPath = "/Identity/Account/ResetPassword";
        var resetUrl = QueryHelpers.AddQueryString(
            $"{frontendBase.TrimEnd('/')}{resetPath}",
            new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["token"] = tokenEncoded
            });

        // 4) Skicka mejlet
        var subject = "Återställ lösenord";
        var html = $@"
        <p>Hej {System.Net.WebUtility.HtmlEncode(user.UserName)},</p>
        <p>Klicka på länken nedan för att välja ett nytt lösenord:</p>
        <p><a href=""{resetUrl}"">Återställ lösenord</a></p>
        <p>Om du inte begärt detta kan du ignorera mejlet.</p>
        <p>Hälsningar,<br/>SarasBlogg</p>";

        await _emailSender.SendAsync(user.Email!, subject, html);

        // 5) Dev-läge: exponera länken i svaret (använder befintligt fält)
        var expose = _cfg.GetValue("Auth:ExposeConfirmLinkInResponse", true);

        return Ok(new BasicResultDto
        {
            Succeeded = true,
            Message = expose ? "Reset link generated (dev)." : neutralMsg,
            ConfirmEmailUrl = expose ? resetUrl : null // återanvänder fältet för dev-visning
        });
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

        return Ok(new BasicResultDto { Succeeded = true, Message = "Password reset successfully" });
    }

}
