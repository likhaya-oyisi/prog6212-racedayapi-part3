using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace RaceDay.Tests;

/// <summary>Event + category management and role-based access.</summary>
public class EventManagementTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public EventManagementTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetEvents_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient(); // no cookies
        var response = await client.GetAsync("/api/events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsParticipant_ReturnsForbidden()
    {
        var client = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(client, "Participant");

        var response = await client.PostAsJsonAsync("/api/events", NewEvent());
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_AsOrganiser_ReturnsCreated()
    {
        var client = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(client, "Organiser");

        var response = await client.PostAsJsonAsync("/api/events", NewEvent());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Open", created.RootElement.GetProperty("status").GetString());
        Assert.Equal("Test City Marathon", created.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task CreateEvent_InvalidType_ReturnsBadRequest()
    {
        var client = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(client, "Organiser");

        var response = await client.PostAsJsonAsync("/api/events", new
        {
            name = "Bad Type", description = "x", eventDate = "2026-11-15",
            location = "Cape Town", distanceKm = 10m, eventType = "Swim", bannerImageUrl = (string?)null
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateEvent_AsOrganiser_Works_ButOtherOrganiserIsForbidden()
    {
        var owner = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(owner, "Organiser");
        var created = await owner.PostAsJsonAsync("/api/events", NewEvent());
        var eventId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("eventId").GetInt32();

        var update = await owner.PutAsJsonAsync($"/api/events/{eventId}", new
        {
            name = "Updated Name", description = "updated", eventDate = "2026-11-20",
            location = "Durban", distanceKm = 21m, eventType = "Run", bannerImageUrl = (string?)null, status = "Closed"
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        // A different organiser must not be able to touch someone else's event.
        var other = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(other, "Organiser");
        var forbidden = await other.DeleteAsync($"/api/events/{eventId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task CreateCategory_AsOrganiser_AndViewAsParticipant()
    {
        var organiser = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(organiser, "Organiser");
        var created = await organiser.PostAsJsonAsync("/api/events", NewEvent());
        var eventId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("eventId").GetInt32();

        var category = await organiser.PostAsJsonAsync($"/api/events/{eventId}/categories", new
        {
            name = "Senior 10K", categoryType = "Age", minAge = 40, maxAge = (int?)null,
            distanceKm = (decimal?)10m, maxParticipants = 200
        });
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);

        // Participants can view available categories.
        var participant = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(participant, "Participant");
        var view = await participant.GetAsync($"/api/events/{eventId}/categories");
        Assert.Equal(HttpStatusCode.OK, view.StatusCode);
    }

    private static object NewEvent() => new
    {
        name = "Test City Marathon", description = "A test run.", eventDate = "2026-11-15",
        location = "Cape Town", distanceKm = 10m, eventType = "Run", bannerImageUrl = (string?)null
    };
}
