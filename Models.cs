using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RaceDay.Api.Models;

/// <summary>A system user. Role is either "Organiser" or "Participant".</summary>
public class User
{
    public int UserId { get; set; }

    [MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [MaxLength(120)] public string Email { get; set; } = string.Empty;
    [MaxLength(255)] public string PasswordHash { get; set; } = string.Empty;
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(20)] public string Role { get; set; } = string.Empty; // Organiser | Participant
    [MaxLength(500)] public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Event> Events { get; set; } = new List<Event>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

/// <summary>A race event created by an Organiser.</summary>
public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    [MaxLength(150)] public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    [Column(TypeName = "date")] public DateOnly EventDate { get; set; }
    [MaxLength(150)] public string Location { get; set; } = string.Empty;
    [Column(TypeName = "decimal(6,2)")] public decimal DistanceKm { get; set; }
    [MaxLength(10)] public string EventType { get; set; } = string.Empty; // Run | Walk | Cycle
    [MaxLength(500)] public string? BannerImageUrl { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Open"; // Draft | Open | Closed | Completed | Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Organiser { get; set; } = null!;
    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

/// <summary>An age or distance category belonging to an event.</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(10)] public string CategoryType { get; set; } = string.Empty; // Age | Distance
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    [Column(TypeName = "decimal(6,2)")] public decimal? DistanceKm { get; set; }
    public int? MaxParticipants { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Event Event { get; set; } = null!;
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}

/// <summary>Links a Participant to an Event and the Category they entered.</summary>
public class Enrollment
{
    public int EnrollmentId { get; set; }
    public int ParticipantId { get; set; }
    public int EventId { get; set; }
    public int CategoryId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Pending"; // Pending | Confirmed | Cancelled | Completed
    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User Participant { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}

/// <summary>The captured finish time and positions for a completed enrollment.</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrollmentId { get; set; }
    [Column(TypeName = "time")] public TimeOnly FinishTime { get; set; }
    public int OverallPosition { get; set; }
    public int? CategoryPosition { get; set; }
    [MaxLength(255)] public string? Notes { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Enrollment Enrollment { get; set; } = null!;
}
