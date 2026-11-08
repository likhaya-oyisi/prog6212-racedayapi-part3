using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Helpers;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Race results. Organisers capture finish times and positions for
/// participants after an event; participants can view their own results.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Capture a finish time and positions for an enrollment (Organiser only, own events).</summary>
    /// <param name="dto">Enrollment id, finish time (HH:mm:ss), overall position, optional category position and notes.</param>
    /// <response code="201">Result captured; the enrollment is marked Completed.</response>
    /// <response code="400">Invalid finish time or position.</response>
    /// <response code="403">Caller is a Participant, or does not own the event.</response>
    /// <response code="404">Enrollment not found.</response>
    /// <response code="409">A result already exists for this enrollment.</response>
    [HttpPost]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> CaptureResult(CreateResultDto dto)
    {
        if (!TimeOnly.TryParse(dto.FinishTime, out var finishTime))
            return BadRequest(new { message = "FinishTime must be in HH:mm:ss format." });
        if (dto.OverallPosition <= 0)
            return BadRequest(new { message = "OverallPosition must be greater than 0." });
        if (dto.CategoryPosition is <= 0)
            return BadRequest(new { message = "CategoryPosition must be greater than 0 when supplied." });

        var enrollment = await _db.Enrollments
            .Include(e => e.Event)
            .FirstOrDefaultAsync(e => e.EnrollmentId == dto.EnrollmentId);
        if (enrollment is null) return NotFound(new { message = "Enrollment not found." });
        if (enrollment.Event.OrganiserId != User.GetUserId())
            return Forbid();

        var exists = await _db.Results.AnyAsync(r => r.EnrollmentId == dto.EnrollmentId);
        if (exists) return Conflict(new { message = "A result has already been captured for this enrollment." });

        var result = new Models.Result
        {
            EnrollmentId = dto.EnrollmentId,
            FinishTime = finishTime,
            OverallPosition = dto.OverallPosition,
            CategoryPosition = dto.CategoryPosition,
            Notes = dto.Notes
        };
        enrollment.Status = "Completed";
        enrollment.UpdatedAt = DateTime.UtcNow;
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var saved = await Load(result.ResultId);
        return CreatedAtAction(nameof(GetById), new { id = result.ResultId }, Mapper.ToDto(saved!));
    }

    /// <summary>View your own results (Participant only).</summary>
    /// <response code="200">List of the participant's results.</response>
    /// <response code="403">Caller is an Organiser.</response>
    [HttpGet("mine")]
    [Authorize(Roles = "Participant")]
    public async Task<IActionResult> GetMyResults()
    {
        var participantId = User.GetUserId();
        var results = await _db.Results
            .Include(r => r.Enrollment).ThenInclude(e => e.Event)
            .Include(r => r.Enrollment).ThenInclude(e => e.Participant)
            .Where(r => r.Enrollment.ParticipantId == participantId)
            .OrderBy(r => r.CapturedAt)
            .ToListAsync();
        return Ok(results.Select(Mapper.ToDto));
    }

    /// <summary>List results for your own events (Organiser only), optionally filtered by event.</summary>
    /// <response code="200">List of results.</response>
    /// <response code="403">Caller is a Participant.</response>
    [HttpGet]
    [Authorize(Roles = "Organiser")]
    public async Task<IActionResult> GetResults([FromQuery] int? eventId)
    {
        var organiserId = User.GetUserId();
        var query = _db.Results
            .Include(r => r.Enrollment).ThenInclude(e => e.Event)
            .Include(r => r.Enrollment).ThenInclude(e => e.Participant)
            .Where(r => r.Enrollment.Event.OrganiserId == organiserId)
            .AsQueryable();
        if (eventId.HasValue) query = query.Where(r => r.Enrollment.EventId == eventId.Value);

        var results = await query.OrderBy(r => r.Enrollment.EventId).ThenBy(r => r.OverallPosition).ToListAsync();
        return Ok(results.Select(Mapper.ToDto));
    }

    /// <summary>Get a single result. The participant it belongs to, or the event's organiser.</summary>
    /// <response code="200">The result.</response>
    /// <response code="403">Caller may not view this result.</response>
    /// <response code="404">Result not found.</response>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await Load(id);
        if (result is null) return NotFound(new { message = "Result not found." });

        var userId = User.GetUserId();
        var allowed = result.Enrollment.ParticipantId == userId || result.Enrollment.Event.OrganiserId == userId;
        if (!allowed) return Forbid();
        return Ok(Mapper.ToDto(result));
    }

    private async Task<Models.Result?> Load(int id) =>
        await _db.Results
            .Include(r => r.Enrollment).ThenInclude(e => e.Event)
            .Include(r => r.Enrollment).ThenInclude(e => e.Participant)
            .FirstOrDefaultAsync(r => r.ResultId == id);
}
