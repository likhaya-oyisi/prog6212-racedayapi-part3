using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Age and distance categories for events. Both roles can view categories.
/// Only Organisers can define, update and delete categories for their own events.
/// </summary>
[ApiController]
[Authorize]
[Route("api")]
public class CategoriesController : ControllerBase
{
    private static readonly string[] ValidTypes = { "Age", "Distance" };
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>List the categories available for an event. Both roles can view.</summary>
    /// <response code="200">List of categories.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("events/{eventId:int}/categories")]
    public async Task<IActionResult> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = "Event not found." });
        var categories = await _db.Categories
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryId)
            .ToListAsync();
        return Ok(categories.Select(Mapper.ToDto));
    }

    /// <summary>Define a category (Age or Distance) for one of your own events. Organiser only.</summary>
    /// <param name="dto">Category name, type (Age/Distance), optional age range, distance and max participants.</param>
    /// <response code="201">Category created.</response>
    /// <response code="400">Invalid category type or age range.</response>
    /// <response code="403">Caller is a Participant or does not own the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> CreateCategory(int eventId, CreateCategoryDto dto)
    {
        var e = await _db.Events.FindAsync(eventId);
        if (e is null) return NotFound(new { message = "Event not found." });
        if (e.OrganiserId != User.GetUserId()) return Forbid();

        var category = new Models.Category
        {
            EventId = eventId,
            Name = dto.Name.Trim(),
            CategoryType = dto.CategoryType,
            MinAge = dto.MinAge,
            MaxAge = dto.MaxAge,
            DistanceKm = dto.DistanceKm,
            MaxParticipants = dto.MaxParticipants
        };
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategories), new { eventId }, Mapper.ToDto(category));
    }

    /// <summary>Update a category. Organiser only, own events only.</summary>
    /// <response code="200">Category updated.</response>
    /// <response code="403">Caller is a Participant or does not own the event.</response>
    /// <response code="404">Category not found.</response>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> UpdateCategory(int id, UpdateCategoryDto dto)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound(new { message = "Category not found." });
        if (category.Event.OrganiserId != User.GetUserId()) return Forbid();

        category.Name = dto.Name.Trim();
        category.CategoryType = dto.CategoryType;
        category.MinAge = dto.MinAge;
        category.MaxAge = dto.MaxAge;
        category.DistanceKm = dto.DistanceKm;
        category.MaxParticipants = dto.MaxParticipants;
        category.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(Mapper.ToDto(category));
    }

    /// <summary>Delete a category. Organiser only, own events only, no enrollments allowed.</summary>
    /// <response code="204">Category deleted.</response>
    /// <response code="403">Caller is a Participant or does not own the event.</response>
    /// <response code="404">Category not found.</response>
    /// <response code="409">The category has enrollments and cannot be deleted.</response>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Enrollments).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound(new { message = "Category not found." });
        if (category.Event.OrganiserId != User.GetUserId()) return Forbid();
        if (category.Enrollments.Count > 0)
            return Conflict(new { message = "This category has enrollments and cannot be deleted." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
