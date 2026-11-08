using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RaceDay.Tests;

/// <summary>Helpers for creating cookie-aware test clients and registering users.</summary>
public static class TestClient
{
    /// <summary>Creates an HttpClient that stores the session cookie in a CookieContainer.</summary>
    public static HttpClient Create(ApiFactory factory, out CookieContainer cookies)
    {
        cookies = new CookieContainer();
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = cookies };
        return factory.CreateDefaultClient(handler);
    }

    /// <summary>Registers (and thereby signs in) a user with a unique email.</summary>
    public static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string role)
    {
        var email = $"{role.ToLower()}-{Guid.NewGuid():N}@example.com";
        return await client.PostAsJsonAsync("/api/auth/register", new
        {
            firstName = "Test",
            lastName = role,
            email,
            password = "Passw0rd!",
            role,
            phone = "0820000000"
        });
    }

    /// <summary>Registers a user and returns their email (the client stays signed in).</summary>
    public static async Task<string> RegisterAndGetEmailAsync(HttpClient client, string role)
    {
        var response = await RegisterAsync(client, role);
        response.EnsureSuccessStatusCode();
        using var payload = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return payload.RootElement.GetProperty("email").GetString()!;
    }
}
