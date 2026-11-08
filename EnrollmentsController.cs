using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Event enrollments. Participants enter events by selecting a category.
/// Organisers can view all enrollments for their own events; participants can
/// view (and cancel) their own enrollments.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class EnrollmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrollmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enter an event by selecting a category (Participant only).</summary>
    /// <param name="dto">The event and the category within that event.</param>
    /// <returns>The recorded enrollment linking participant, event and category.</returns>
    /// <response code="201">Enrollment recorded.</response>
    /// <response code="400">Category does not belong to the event.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Caller is an Organiser.</response>
    /// <response code="404">Event or category not found.</response>
    /// <response code="409">Already enrolled in this event.</response>
    [HttpPost]
    [Authorize(Roles = "Participant")]
    public async Task<IActionResult> Enroll(CreateEnrollmentDto dto)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == dto.EventId))
            return NotFound(new { message = "Event not found." });

        var categoryExists = await _db.Categories
            .AnyAsync(c => c.CategoryId == dto.CategoryId && c.EventId == dto.EventId);
        if (!categoryExists)
            return BadRequest(new { message = "Category does not belong to this event." });

        var participantId = User.GetUserId();
        var alreadyEnrolled = await _db.Enrollments
            .AnyAsync(e => e.ParticipantId == participantId && e.EventId == dto.EventId);
        if (alreadyEnrolled)
            return Conflict(new { message = "You are already enrolled in this event." });

        var enrollment = new Models.Enrollment
        {
            ParticipantId = participantId,
            EventId = dto.EventId,
            CategoryId = dto.CategoryId,
            Status = "Confirmed"
        };
        _db.Enrollments.Add(enrollment);
        await _db.SaveChangesAsync();

        var saved = await Load(enrollment.EnrollmentId);
        return CreatedAtAction(nameof(GetById), new { id = enrollment.EnrollmentId }, Mapper.ToDto(saved!));
    }

    /// <summary>
    /// List enrollments. Organisers see all enrollments for their own events;
    /// participants see only their own.
    /// </summary>
    /// <response code="200">List of enrollments.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet]
    public async Task<IActionResult> GetEnrollments()
    {
        var userId = User.GetUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);

        var query = _db.Enrollments
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Participant)
            .AsQueryable();

        query = role == "Organiser"
            ? query.Where(e => e.Event.OrganiserId == userId)
            : query.Where(e => e.ParticipantId == userId);

        var enrollments = await query.OrderByDescending(e => e.EnrolledAt).ToListAsync();
        return Ok(enrollments.Select(Mapper.ToDto));
    }

    /// <summary>Get a single enrollment. Only the enrolled participant or the event's organiser.</summary>
    /// <response code="200">The enrollment.</response>
    /// <response code="403">Caller may not view this enrollment.</response>
    /// <response code="404">Enrollment not found.</response>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var enrollment = await Load(id);
        if (enrollment is null) return NotFound(new { message = "Enrollment not found." });
        if (!CanAccess(enrollment)) return Forbid();
        return Ok(Mapper.ToDto(enrollment));
    }

    /// <summary>Cancel your own enrollment (Participant only).</summary>
    /// <response code="200">Enrollment cancelled.</response>
    /// <response code="403">Caller is an Organiser or does not own the enrollment.</response>
    /// <response code="404">Enrollment not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Participant")]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrollment = await _db.Enrollments.Include(e => e.Event).FirstOrDefaultAsync(e => e.EnrollmentId == id);
        if (enrollment is null) return NotFound(new { message = "Enrollment not found." });
        if (enrollment.ParticipantId != User.GetUserId()) return Forbid();

        enrollment.Status = "Cancelled";
        enrollment.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Ok(Mapper.ToDto(enrollment));
    }

    private async Task<Models.Enrollment?> Load(int id) =>
        await _db.Enrollments
            .Include(e => e.Event)
            .Include(e => e.Category)
            .Include(e => e.Participant)
            .FirstOrDefaultAsync(e => e.EnrollmentId == id);

    private bool CanAccess(Models.Enrollment enrollment)
    {
        var userId = User.GetUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);
        return role == "Organiser"
            ? enrollment.Event.OrganiserId == userId
            : enrollment.ParticipantId == userId;
    }
}
