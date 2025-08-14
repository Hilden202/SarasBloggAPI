using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SarasBloggAPI.Data;
using SarasBloggAPI.DTOs;
using SarasBloggAPI.Services;

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

    public AuthController(SignInManager<ApplicationUser> signInManager,
                          UserManager<ApplicationUser> userManager,
                          TokenService tokenService,
                          IConfiguration cfg)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _tokenService = tokenService;
        _cfg = cfg;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        // tillåt både username och email
        var user = await _userManager.FindByNameAsync(req.UserNameOrEmail)
                   ?? await _userManager.FindByEmailAsync(req.UserNameOrEmail);

        if (user is null)
            return Unauthorized("Invalid credentials.");

        var result = await _signInManager.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized("Invalid credentials.");

        // (om du vill kräva email-konfirmation:)
        // if (!await _userManager.IsEmailConfirmedAsync(user)) return Forbid("Email not confirmed.");

        var access = await _tokenService.CreateAccessTokenAsync(user);
        var accessExp = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:AccessTokenMinutes"] ?? "60"));

        var (refresh, refreshExp) = _tokenService.CreateRefreshToken();
        // TODO: spara refresh-token i DB för rotation/revokering (senare steg)

        return new LoginResponse(access, accessExp, refresh, refreshExp);
    }

    // OBS: för ren JWT behövs inte "server-side logout".
    // Men om du kör cookie-auth parallellt under migreringen kan detta vara trevligt.
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync(); // gör inget för JWT, men loggar ut ev. cookies
        return Ok(new { message = "Logged out" });
    }

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
}
