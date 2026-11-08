using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

/// <summary>View and update the profile of the logged-in user. Both roles can use these endpoints.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db) => _db = db;

    /// <summary>Get the current user's profile.</summary>
    /// <response code="200">Profile returned.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        return user is null ? NotFound() : Ok(Mapper.ToDto(user));
    }

    /// <summary>Update the current user's profile (name, phone, profile image).</summary>
    /// <response code="200">Profile updated.</response>
    /// <response code="400">Details are invalid.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateProfileDto dto)
    {
        var user = await _db.Users.FindAsync(User.GetUserId());
        if (user is null) return NotFound();

        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.Phone = dto.Phone;
        user.ProfileImageUrl = dto.ProfileImageUrl;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(Mapper.ToDto(user));
    }
}
