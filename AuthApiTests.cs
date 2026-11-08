using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace RaceDay.Tests;

/// <summary>User registration, login and session behaviour.</summary>
public class AuthApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AuthApiTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_ValidDetails_ReturnsCreatedAndSetsSessionCookie()
    {
        var client = TestClient.Create(_factory, out var cookies);
        var response = await TestClient.RegisterAsync(client, "Participant");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(cookies.GetAllCookies(), c => c.Name == "raceday.session");
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var client = TestClient.Create(_factory, out _);
        var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Dup", lastName = "User", email = $"dup-{Guid.NewGuid():N}@example.com",
            password = "Passw0rd!", role = "Participant", phone = "0820000000"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Dup", lastName = "User", email = (await first.Content.ReadAsStringAsync()).Contains("example.com")
                ? System.Text.Json.JsonDocument.Parse(await first.Content.ReadAsStringAsync()).RootElement.GetProperty("email").GetString()
                : null,
            password = "Passw0rd!", role = "Participant", phone = "0820000000"
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_ReturnsBadRequest()
    {
        var client = TestClient.Create(_factory, out _);
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Bad", lastName = "Role", email = $"bad-{Guid.NewGuid():N}@example.com",
            password = "Passw0rd!", role = "Admin", phone = "0820000000"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsOkAndSetsSession()
    {
        var client = TestClient.Create(_factory, out var cookies);
        var email = await TestClient.RegisterAndGetEmailAsync(client, "Organiser");

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Passw0rd!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Contains(cookies.GetAllCookies(), c => c.Name == "raceday.session");
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        var client = TestClient.Create(_factory, out _);
        var email = await TestClient.RegisterAndGetEmailAsync(client, "Participant");

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPass1" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession_AndSubsequentRequestIsUnauthorized()
    {
        var client = TestClient.Create(_factory, out _);
        await TestClient.RegisterAndGetEmailAsync(client, "Participant");

        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }
}
