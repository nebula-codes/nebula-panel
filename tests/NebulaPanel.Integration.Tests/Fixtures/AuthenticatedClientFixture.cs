using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Integration.Tests.Fixtures;

/// <summary>
/// Helper class for creating authenticated HTTP clients for testing.
/// </summary>
public class AuthenticatedClientFixture
{
    private readonly HttpClient _client;
    private readonly NebulaPanelWebApplicationFactory _factory;
    private string? _accessToken;

    public AuthenticatedClientFixture(NebulaPanelWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>
    /// Gets the underlying HTTP client.
    /// </summary>
    public HttpClient Client => _client;

    /// <summary>
    /// Gets the current access token.
    /// </summary>
    public string? AccessToken => _accessToken;

    /// <summary>
    /// Registers a new user and authenticates the client.
    /// </summary>
    public async Task<RegisterResponse> RegisterAndAuthenticateAsync(
        string username = "testuser",
        string email = "test@example.com",
        string password = "TestPassword123!")
    {
        var registerRequest = new { Username = username, Email = email, Password = password };
        var registerResponse = await _client.PostAsync(
            "/api/auth/register",
            new StringContent(
                JsonSerializer.Serialize(registerRequest),
                Encoding.UTF8,
                "application/json"));

        registerResponse.EnsureSuccessStatusCode();

        var responseJson = await registerResponse.Content.ReadAsStringAsync();
        var registerResult = JsonSerializer.Deserialize<RegisterResponse>(
            responseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Now login to get the access token
        var loginRequest = new { Username = username, Password = password };
        var loginResponse = await _client.PostAsync(
            "/api/auth/login",
            new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"));

        loginResponse.EnsureSuccessStatusCode();

        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<AuthResponse>(
            loginJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _accessToken = loginResult!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        return registerResult!;
    }

    /// <summary>
    /// Authenticates with existing user credentials.
    /// </summary>
    public async Task<AuthResponse> AuthenticateAsync(string username, string password)
    {
        var loginRequest = new { Username = username, Password = password };
        var loginResponse = await _client.PostAsync(
            "/api/auth/login",
            new StringContent(
                JsonSerializer.Serialize(loginRequest),
                Encoding.UTF8,
                "application/json"));

        loginResponse.EnsureSuccessStatusCode();

        var loginJson = await loginResponse.Content.ReadAsStringAsync();
        var loginResult = JsonSerializer.Deserialize<AuthResponse>(
            loginJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _accessToken = loginResult!.AccessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        return loginResult;
    }

    /// <summary>
    /// Sets the authorization header manually for testing.
    /// </summary>
    public void SetAccessToken(string accessToken)
    {
        _accessToken = accessToken;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    }

    /// <summary>
    /// Clears the authorization header for unauthenticated requests.
    /// </summary>
    public void ClearAuthentication()
    {
        _accessToken = null;
        _client.DefaultRequestHeaders.Authorization = null;
    }
}

/// <summary>
/// Response from register endpoint.
/// </summary>
public record RegisterResponse(Guid UserId, string Message);

/// <summary>
/// Response from login endpoint.
/// </summary>
public record AuthResponse(string AccessToken, DateTime ExpiresAt);
