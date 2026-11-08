using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------------- Authentication / Users ----------------
public record RegisterDto(string FirstName, string LastName, string Email, string Password, string Role, string? Phone);
public record LoginDto(string Email, string Password);
public record UserDto(int UserId, string FirstName, string LastName, string Email, string Role, string? Phone, string? ProfileImageUrl);
public record UpdateProfileDto(string FirstName, string LastName, string? Phone, string? ProfileImageUrl);

// ---------------- Events ----------------
public record EventDto(int EventId, int OrganiserId, string OrganiserName, string Name, string Description,
    DateOnly EventDate, string Location, decimal DistanceKm, string EventType, string? BannerImageUrl, string Status);
public record CreateEventDto(string Name, string Description, DateOnly EventDate, string Location,
    decimal DistanceKm, string EventType, string? BannerImageUrl);
public record UpdateEventDto(string Name, string Description, DateOnly EventDate, string Location,
    decimal DistanceKm, string EventType, string? BannerImageUrl, string? Status);

// ---------------- Categories ----------------
public record CategoryDto(int CategoryId, int EventId, string Name, string CategoryType,
    int? MinAge, int? MaxAge, decimal? DistanceKm, int? MaxParticipants);
public record CreateCategoryDto(string Name, string CategoryType, int? MinAge, int? MaxAge,
    decimal? DistanceKm, int? MaxParticipants);
public record UpdateCategoryDto(string Name, string CategoryType, int? MinAge, int? MaxAge,
    decimal? DistanceKm, int? MaxParticipants);

// ---------------- Enrollments ----------------
public record CreateEnrollmentDto(int EventId, int CategoryId);
public record EnrollmentDto(int EnrollmentId, int ParticipantId, string? ParticipantName, int EventId,
    string? EventName, int CategoryId, string? CategoryName, string Status, DateTime EnrolledAt);

// ---------------- Results ----------------
public record CreateResultDto(int EnrollmentId, string FinishTime, int OverallPosition, int? CategoryPosition, string? Notes);
public record ResultDto(int ResultId, int EnrollmentId, string FinishTime, int OverallPosition,
    int? CategoryPosition, string? Notes, DateTime CapturedAt, string? EventName, string? ParticipantName);

/// <summary>Maps entity objects to the DTOs returned by the API.</summary>
public static class Mapper
{
    public static UserDto ToDto(User u) =>
        new(u.UserId, u.FirstName, u.LastName, u.Email, u.Role, u.Phone, u.ProfileImageUrl);

    public static EventDto ToDto(Event e) =>
        new(e.EventId, e.OrganiserId, $"{e.Organiser.FirstName} {e.Organiser.LastName}", e.Name, e.Description,
            e.EventDate, e.Location, e.DistanceKm, e.EventType, e.BannerImageUrl, e.Status);

    public static CategoryDto ToDto(Category c) =>
        new(c.CategoryId, c.EventId, c.Name, c.CategoryType, c.MinAge, c.MaxAge, c.DistanceKm, c.MaxParticipants);

    public static EnrollmentDto ToDto(Enrollment e) =>
        new(e.EnrollmentId, e.ParticipantId, e.Participant is null ? null : $"{e.Participant.FirstName} {e.Participant.LastName}",
            e.EventId, e.Event?.Name, e.CategoryId, e.Category?.Name, e.Status, e.EnrolledAt);

    public static ResultDto ToDto(Result r) =>
        new(r.ResultId, r.EnrollmentId, r.FinishTime.ToString("HH:mm:ss"), r.OverallPosition,
            r.CategoryPosition, r.Notes, r.CapturedAt,
            r.Enrollment?.Event?.Name,
            r.Enrollment?.Participant is null ? null : $"{r.Enrollment.Participant.FirstName} {r.Enrollment.Participant.LastName}");
}
