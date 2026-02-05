using System.Text;
using System.Text.Json;
using NebulaPanel.Integration.Tests.Fixtures;

namespace NebulaPanel.Integration.Tests.Controllers;

[Collection("Integration")]
public class AuthControllerTests
{
    private readonly NebulaPanelWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(NebulaPanelWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var request = new
        {
            Username = $"newuser_{uniqueId}",
            Email = $"newuser_{uniqueId}@example.com",
            Password = "Password123!"
        };

        // Act
        var response = await _client.PostAsync(
            "/api/auth/register",
            CreateJsonContent(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange - First registration
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var request = new
        {
            Username = $"dupuser_{uniqueId}",
            Email = $"first_{uniqueId}@example.com",
            Password = "Password123!"
        };
        await _client.PostAsync("/api/auth/register", CreateJsonContent(request));

        // Act - Second registration with same username
        var duplicateRequest = new
        {
            Username = $"dupuser_{uniqueId}",
            Email = $"second_{uniqueId}@example.com",
            Password = "Password123!"
        };
        var response = await _client.PostAsync(
            "/api/auth/register",
            CreateJsonContent(duplicateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - First registration
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var request = new
        {
            Username = $"user1_{uniqueId}",
            Email = $"dup_{uniqueId}@example.com",
            Password = "Password123!"
        };
        await _client.PostAsync("/api/auth/register", CreateJsonContent(request));

        // Act - Second registration with same email
        var duplicateRequest = new
        {
            Username = $"user2_{uniqueId}",
            Email = $"dup_{uniqueId}@example.com",
            Password = "Password123!"
        };
        var response = await _client.PostAsync(
            "/api/auth/register",
            CreateJsonContent(duplicateRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        // Arrange - Register first
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerRequest = new
        {
            Username = $"loginuser_{uniqueId}",
            Email = $"login_{uniqueId}@example.com",
            Password = "Password123!"
        };
        await _client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));

        // Act
        var loginRequest = new
        {
            Email = $"login_{uniqueId}@example.com",
            Password = "Password123!"
        };
        var response = await _client.PostAsync(
            "/api/auth/login",
            CreateJsonContent(loginRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("accessToken");
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange - Register first
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerRequest = new
        {
            Username = $"wrongpassuser_{uniqueId}",
            Email = $"wrongpass_{uniqueId}@example.com",
            Password = "Password123!"
        };
        await _client.PostAsync("/api/auth/register", CreateJsonContent(registerRequest));

        // Act - Login with wrong password
        var loginRequest = new
        {
            Email = $"wrongpass_{uniqueId}@example.com",
            Password = "WrongPassword!"
        };
        var response = await _client.PostAsync(
            "/api/auth/login",
            CreateJsonContent(loginRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Act
        var loginRequest = new
        {
            Email = $"nonexistent_{Guid.NewGuid():N}@example.com",
            Password = "Password123!"
        };
        var response = await _client.PostAsync(
            "/api/auth/login",
            CreateJsonContent(loginRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithValidToken_ReturnsUser()
    {
        // Arrange
        var authFixture = new AuthenticatedClientFixture(_factory);
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        await authFixture.RegisterAndAuthenticateAsync(
            username: $"meuser_{uniqueId}",
            email: $"me_{uniqueId}@example.com",
            password: "Password123!");

        // Act
        var response = await authFixture.Client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain($"meuser_{uniqueId}");
    }

    [Fact]
    public async Task GetMe_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithValidToken_ReturnsSuccess()
    {
        // Arrange
        var authFixture = new AuthenticatedClientFixture(_factory);
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        await authFixture.RegisterAndAuthenticateAsync(
            username: $"logoutuser_{uniqueId}",
            email: $"logout_{uniqueId}@example.com",
            password: "Password123!");

        // Act
        var response = await authFixture.Client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var authFixture = new AuthenticatedClientFixture(_factory);
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        await authFixture.RegisterAndAuthenticateAsync(
            username: $"changepassuser_{uniqueId}",
            email: $"changepass_{uniqueId}@example.com",
            password: "OldPassword123!");

        // Act
        var changeRequest = new
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword123!"
        };
        var response = await authFixture.Client.PostAsync(
            "/api/auth/change-password",
            CreateJsonContent(changeRequest));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify can login with new password
        authFixture.ClearAuthentication();
        var loginResponse = await authFixture.AuthenticateAsync(
            $"changepass_{uniqueId}@example.com",
            "NewPassword123!");

        loginResponse.AccessToken.Should().NotBeNullOrEmpty();
    }

    private static StringContent CreateJsonContent(object obj)
    {
        return new StringContent(
            JsonSerializer.Serialize(obj),
            Encoding.UTF8,
            "application/json");
    }
}
