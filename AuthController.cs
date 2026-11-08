using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Handles registration, login, logout and the current session. On success a
/// session cookie ("raceday.session") is issued containing the user id, email
/// and role. The password is hashed with PBKDF2-SHA256 before it is stored.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public AuthController(RaceDayDbContext db) => _db = db;

    /// <summary>Register a new account as an Organiser or Participant.</summary>
    /// <param name="dto">Personal details, password and role ("Organiser" or "Participant").</param>
    /// <returns>The created user profile and sets the session cookie.</returns>
    /// <response code="201">Account created and session established.</response>
    /// <response code="400">Role is invalid or details are missing.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (dto.Role is not ("Organiser" or "Participant"))
        {
            ModelState.AddModelError("Role", "Role must be 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 6)
        {
            ModelState.AddModelError("Password", "Password must be at least 6 characters.");
            return ValidationProblem(ModelState);
        }

        var email = dto.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "An account with this email already exists." });

        var user = new User
        {
            FirstName = dto.FirstName.Trim(),
            LastName = dto.LastName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Phone = dto.Phone,
            Role = dto.Role
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        await SignIn(user);
        return CreatedAtAction(nameof(Me), new { }, Mapper.ToDto(user));
    }

    /// <summary>Log in with an email and password.</summary>
    /// <param name="dto">Registered email and password.</param>
    /// <returns>The user profile and sets the session cookie.</returns>
    /// <response code="200">Logged in; session cookie issued.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == email);

        if (user is null || !PasswordHasher.Verify(dto.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        await SignIn(user);
        return Ok(Mapper.ToDto(user));
    }

    /// <summary>Log out the current user and end the session.</summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Get the profile of the currently logged-in user.</summary>
    /// <response code="200">Current user's profile.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        var user = await _db.Users.FindAsync(userId);
        return user is null ? NotFound() : Ok(Mapper.ToDto(user));
    }

    private async Task SignIn(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }
}
