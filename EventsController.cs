using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Race event management. Both Organisers and Participants can view events.
/// Only Organisers can create, update and delete events, and only their own events.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private static readonly string[] ValidTypes = { "Run", "Walk", "Cycle" };
    private static readonly string[] ValidStatuses = { "Draft", "Open", "Closed", "Completed", "Cancelled" };
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>List events. Authenticated users of both roles can view events.</summary>
    /// <param name="includeClosed">Set to true to also return Closed/Completed/Cancelled events.</param>
    /// <response code="200">List of events.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet]
    public async Task<IActionResult> GetEvents([FromQuery] bool includeClosed = false)
    {
        var query = _db.Events.Include(e => e.Organiser).AsQueryable();
        if (!includeClosed)
            query = query.Where(e => e.Status == "Open" || e.Status == "Draft");
        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(Mapper.ToDto));
    }

    /// <summary>Get a single event by id.</summary>
    /// <response code="200">The event.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Event not found.</response>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEvent(int id)
    {
        var e = await _db.Events.Include(e => e.Organiser).FirstOrDefaultAsync(e => e.EventId == id);
        return e is null ? NotFound(new { message = "Event not found." }) : Ok(Mapper.ToDto(e));
    }

    /// <summary>Create a new event (Organiser only).</summary>
    /// <param name="dto">Name, description, date, location, distance, type (Run/Walk/Cycle).</param>
    /// <response code="201">Event created.</response>
    /// <response code="400">Invalid event type or distance.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is a Participant.</response>
    [HttpPost]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> CreateEvent(CreateEventDto dto)
    {
        if (!ValidateEvent(dto.EventType, dto.DistanceKm, out var problem)) return problem!;

        var e = new Models.Event
        {
            OrganiserId = User.GetUserId(),
            Name = dto.Name.Trim(),
            Description = dto.Description.Trim(),
            EventDate = dto.EventDate,
            Location = dto.Location.Trim(),
            DistanceKm = dto.DistanceKm,
            EventType = dto.EventType,
            BannerImageUrl = dto.BannerImageUrl,
            Status = "Open"
        };
        _db.Events.Add(e);
        await _db.SaveChangesAsync();

        e.Organiser = await _db.Users.FindAsync(e.OrganiserId) ?? e.Organiser;
        return CreatedAtAction(nameof(GetEvent), new { id = e.EventId }, Mapper.ToDto(e));
    }

    /// <summary>Update an event (Organiser only, own events only).</summary>
    /// <response code="200">Event updated.</response>
    /// <response code="403">Caller is a Participant or does not own the event.</response>
    /// <response code="404">Event not found.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> UpdateEvent(int id, UpdateEventDto dto)
    {
        var e = await _db.Events.FindAsync(id);
        if (e is null) return NotFound(new { message = "Event not found." });
        if (e.OrganiserId != User.GetUserId()) return Forbid();

        if (!ValidateEvent(dto.EventType, dto.DistanceKm, out var problem)) return problem!;
        if (dto.Status is not null && !ValidStatuses.Contains(dto.Status))
        {
            ModelState.AddModelError("Status", $"Status must be one of: {string.Join(", ", ValidStatuses)}.");
            return ValidationProblem(ModelState);
        }

        e.Name = dto.Name.Trim();
        e.Description = dto.Description.Trim();
        e.EventDate = dto.EventDate;
        e.Location = dto.Location.Trim();
        e.DistanceKm = dto.DistanceKm;
        e.EventType = dto.EventType;
        e.BannerImageUrl = dto.BannerImageUrl;
        if (dto.Status is not null) e.Status = dto.Status;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        e.Organiser = await _db.Users.FindAsync(e.OrganiserId) ?? e.Organiser;
        return Ok(Mapper.ToDto(e));
    }

    /// <summary>Delete an event (Organiser only, own events only, no enrollments allowed).</summary>
    /// <response code="204">Event deleted.</response>
    /// <response code="403">Caller is a Participant or does not own the event.</response>
    /// <response code="404">Event not found.</response>
    /// <response code="409">The event has enrollments and cannot be deleted.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        var e = await _db.Events.Include(ev => ev.Enrollments).FirstOrDefaultAsync(ev => ev.EventId == id);
        if (e is null) return NotFound(new { message = "Event not found." });
        if (e.OrganiserId != User.GetUserId()) return Forbid();
        if (e.Enrollments.Count > 0)
            return Conflict(new { message = "This event has enrollments and cannot be deleted. Cancel it instead." });

        _db.Events.Remove(e);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private bool ValidateEvent(string eventType, decimal distanceKm, out IActionResult? problem)
    {
        problem = null;
        if (!ValidTypes.Contains(eventType))
        {
            ModelState.AddModelError("EventType", "EventType must be 'Run', 'Walk' or 'Cycle'.");
            problem = ValidationProblem(ModelState);
            return false;
        }
        if (distanceKm <= 0)
        {
            ModelState.AddModelError("DistanceKm", "DistanceKm must be greater than 0.");
            problem = ValidationProblem(ModelState);
            return false;
        }
        return true;
    }
}
