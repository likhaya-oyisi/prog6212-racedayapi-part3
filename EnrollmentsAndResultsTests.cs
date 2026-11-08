using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RaceDay.Tests;

/// <summary>Participant enrollment flow and organiser result capture.</summary>
public class EnrollmentsAndResultsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public EnrollmentsAndResultsTests(ApiFactory factory) => _factory = factory;

    private static async Task<int> CreateEventWithCategoryAsync(HttpClient organiser)
    {
        var created = await organiser.PostAsJsonAsync("/api/events", new
        {
            name = $"Flow Event {Guid.NewGuid():N}", description = "flow", eventDate = "2026-12-01",
            location = "Durban", distanceKm = 40m, eventType = "Cycle", bannerImageUrl = (string?)null
        });
        created.EnsureSuccessStatusCode();
        var eventId = JsonDocument.Parse(await created.Content.ReadAsStringAsync())
            .RootElement.GetProperty("eventId").GetInt32();

        var category = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Open 40K", categoryType = "Distance", minAge = (int?)18, maxAge = (int?)null,
            distanceKm = (decimal?)40m, maxParticipants = 400
        });
        category.EnsureSuccessStatusCode();
        return eventId;
    }

    private static async Task<(int enrollmentId, int categoryId)> EnrollAsync(HttpClient participant, int eventId)
    {
        var categories = await participant.GetFromJsonAsync<JsonElement[]>($"/api/events/{eventId}/categories");
        var categoryId = categories![0].GetProperty("categoryId").GetInt32();

        var enrolled = await participant.PostAsJsonAsync("/api/enrollments", new { eventId, categoryId });
        enrolled.EnsureSuccessStatusCode();
        var enrollmentId = JsonDocument.Parse(await enrolled.Content.ReadAsStringAsync())
            .RootElement.GetProperty("enrollmentId").GetInt32();
        return (enrollmentId, categoryId);
    }

    [Fact]
    public async Task Participant_CanEnroll_AndEnrollmentIsRecorded_AndDuplicateIsRejected()
    {
        var organiser = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(organiser, "Organiser");
        var eventId = await CreateEventWithCategoryAsync(organiser);

        var participant = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(participant, "Participant");

        var (enrollmentId, categoryId) = await EnrollAsync(participant, eventId);
        Assert.True(enrollmentId > 0);

        // The organiser can see the enrollment for their event.
        var list = await organiser.GetFromJsonAsync<JsonElement[]>("/api/enrollments");
        Assert.Contains(list!, e => e.GetProperty("enrollmentId").GetInt32() == enrollmentId);

        // Enrolling a second time must be rejected.
        var duplicate = await participant.PostAsJsonAsync("/api/enrollments", new { eventId, categoryId });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnroll_ParticipantCannotCaptureResults()
    {
        var organiser = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(organiser, "Organiser");
        var eventId = await CreateEventWithCategoryAsync(organiser);

        var participant = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(participant, "Participant");
        var (enrollmentId, _) = await EnrollAsync(participant, eventId);

        // Organisers cannot enroll (Participant-only functionality).
        var enroll = await organiser.PostAsJsonAsync("/api/enrollments", new { eventId, categoryId = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, enroll.StatusCode);

        // Participants cannot capture results (Organiser-only functionality).
        var capture = await participant.PostAsJsonAsync("/api/results", new
        {
            enrollmentId, finishTime = "01:38:20", overallPosition = 31, categoryPosition = 8, notes = "x"
        });
        Assert.Equal(HttpStatusCode.Forbidden, capture.StatusCode);
    }

    [Fact]
    public async Task Organiser_CapturesResult_ParticipantSeesOwnResult_DuplicateRejected()
    {
        var organiser = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(organiser, "Organiser");
        var eventId = await CreateEventWithCategoryAsync(organiser);

        var participant = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(participant, "Participant");
        var (enrollmentId, _) = await EnrollAsync(participant, eventId);

        var capture = await organiser.PostAsJsonAsync("/api/results", new
        {
            enrollmentId, finishTime = "00:47:35", overallPosition = 47, categoryPosition = 12,
            notes = "Completed Cape Town Sunrise 10K."
        });
        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        // The participant can view their own results.
        var mine = await participant.GetFromJsonAsync<JsonElement[]>("/api/results/mine");
        Assert.Contains(mine!, r =>
            r.GetProperty("enrollmentId").GetInt32() == enrollmentId &&
            r.GetProperty("finishTime").GetString() == "00:47:35");

        // A second result for the same enrollment is rejected.
        var duplicate = await organiser.PostAsJsonAsync("/api/results", new
        {
            enrollmentId, finishTime = "00:50:00", overallPosition = 50, categoryPosition = (int?)null, notes = (string?)null
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_AsParticipant_UpdatesOwnDetails()
    {
        var participant = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(participant, "Participant");

        var update = await participant.PutAsJsonAsync("/api/users/me", new
        {
            firstName = "Updated", lastName = "Name", phone = "0831112222",
            profileImageUrl = "https://example.com/me.png"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var doc = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
        Assert.Equal("Updated", doc.RootElement.GetProperty("firstName").GetString());
    }
}
