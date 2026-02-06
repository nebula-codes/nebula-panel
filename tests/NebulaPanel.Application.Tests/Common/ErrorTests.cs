namespace NebulaPanel.Application.Tests.Common;

public class ErrorTests
{
    // Resource errors

    [Fact]
    public void NotFound_WithResourceAndIdentifier_FormatsMessage()
    {
        var error = Error.NotFound("Server", "123");

        error.Code.Should().Be(ErrorCode.NotFound);
        error.Message.Should().Be("Server with ID '123' not found");
    }

    [Fact]
    public void NotFound_WithResourceOnly_FormatsMessageWithoutId()
    {
        var error = Error.NotFound("Server");

        error.Code.Should().Be(ErrorCode.NotFound);
        error.Message.Should().Be("Server not found");
    }

    [Fact]
    public void AlreadyExists_WithResourceAndIdentifier_FormatsMessage()
    {
        var error = Error.AlreadyExists("Game", "minecraft");

        error.Code.Should().Be(ErrorCode.AlreadyExists);
        error.Message.Should().Be("Game 'minecraft' already exists");
    }

    [Fact]
    public void AlreadyExists_WithResourceOnly_FormatsMessage()
    {
        var error = Error.AlreadyExists("User");

        error.Code.Should().Be(ErrorCode.AlreadyExists);
        error.Message.Should().Be("User already exists");
    }

    [Fact]
    public void Conflict_ReturnsConflictCode()
    {
        var error = Error.Conflict("Version mismatch", "Expected v2, got v1");

        error.Code.Should().Be(ErrorCode.Conflict);
        error.Message.Should().Be("Version mismatch");
        error.Details.Should().Be("Expected v2, got v1");
    }

    // Validation errors

    [Fact]
    public void Validation_ReturnsValidationErrorCode()
    {
        var error = Error.Validation("Name is required");

        error.Code.Should().Be(ErrorCode.ValidationError);
        error.Message.Should().Be("Name is required");
    }

    [Fact]
    public void InvalidInput_ReturnsInvalidInputCode()
    {
        var error = Error.InvalidInput("Port must be positive", "port=-1");

        error.Code.Should().Be(ErrorCode.InvalidInput);
        error.Message.Should().Be("Port must be positive");
        error.Details.Should().Be("port=-1");
    }

    [Fact]
    public void InvalidOperation_ReturnsInvalidOperationCode()
    {
        var error = Error.InvalidOperation("Cannot delete active server");

        error.Code.Should().Be(ErrorCode.InvalidOperation);
        error.Message.Should().Be("Cannot delete active server");
    }

    // Auth errors

    [Fact]
    public void Unauthorized_WithDefaultMessage()
    {
        var error = Error.Unauthorized();

        error.Code.Should().Be(ErrorCode.Unauthorized);
        error.Message.Should().Be("Authentication required");
    }

    [Fact]
    public void Unauthorized_WithCustomMessage()
    {
        var error = Error.Unauthorized("Token missing");

        error.Code.Should().Be(ErrorCode.Unauthorized);
        error.Message.Should().Be("Token missing");
    }

    [Fact]
    public void Forbidden_WithDefaultMessage()
    {
        var error = Error.Forbidden();

        error.Code.Should().Be(ErrorCode.Forbidden);
        error.Message.Should().Be("Access denied");
    }

    [Fact]
    public void InvalidCredentials_WithDefaultMessage()
    {
        var error = Error.InvalidCredentials();

        error.Code.Should().Be(ErrorCode.InvalidCredentials);
        error.Message.Should().Be("Invalid username or password");
    }

    [Fact]
    public void TokenExpired_ReturnsCorrectCodeAndMessage()
    {
        var error = Error.TokenExpired();

        error.Code.Should().Be(ErrorCode.TokenExpired);
        error.Message.Should().Be("Token has expired");
    }

    [Fact]
    public void AccountLocked_WithLockoutTime_IncludesTimeInMessage()
    {
        var lockoutEnd = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var error = Error.AccountLocked(lockoutEnd);

        error.Code.Should().Be(ErrorCode.AccountLocked);
        error.Message.Should().Contain("locked until");
        error.Details.Should().NotBeNull();
    }

    [Fact]
    public void AccountLocked_WithoutLockoutTime_UsesDefaultMessage()
    {
        var error = Error.AccountLocked();

        error.Code.Should().Be(ErrorCode.AccountLocked);
        error.Message.Should().Contain("too many failed login attempts");
    }

    [Fact]
    public void RateLimited_IncludesRetrySeconds()
    {
        var error = Error.RateLimited(30);

        error.Code.Should().Be(ErrorCode.RateLimited);
        error.Message.Should().Contain("30 seconds");
        error.Details.Should().Be("30");
    }

    [Fact]
    public void SessionExpired_ReturnsCorrectCode()
    {
        var error = Error.SessionExpired();

        error.Code.Should().Be(ErrorCode.SessionExpired);
        error.Message.Should().Contain("session has expired");
    }

    [Fact]
    public void AccountDisabled_ReturnsCorrectCode()
    {
        var error = Error.AccountDisabled();

        error.Code.Should().Be(ErrorCode.AccountDisabled);
        error.Message.Should().Contain("disabled");
    }

    // Server operation errors

    [Fact]
    public void ServerRunning_WithOperation_IncludesOperationName()
    {
        var error = Error.ServerRunning("delete");

        error.Code.Should().Be(ErrorCode.ServerRunning);
        error.Message.Should().Be("Cannot delete while server is running");
    }

    [Fact]
    public void ServerRunning_WithoutOperation_UsesDefaultMessage()
    {
        var error = Error.ServerRunning();

        error.Code.Should().Be(ErrorCode.ServerRunning);
        error.Message.Should().Contain("stopped to perform this operation");
    }

    [Fact]
    public void ServerStopped_WithOperation_IncludesOperationName()
    {
        var error = Error.ServerStopped("send command");

        error.Code.Should().Be(ErrorCode.ServerStopped);
        error.Message.Should().Contain("send command");
    }

    [Fact]
    public void ServerNotInstalled_ReturnsCorrectCode()
    {
        var error = Error.ServerNotInstalled();

        error.Code.Should().Be(ErrorCode.ServerNotInstalled);
        error.Message.Should().Contain("not installed");
    }

    [Fact]
    public void PortInUse_IncludesPortNumber()
    {
        var error = Error.PortInUse(25565);

        error.Code.Should().Be(ErrorCode.PortInUse);
        error.Message.Should().Be("Port 25565 is already in use");
    }

    [Fact]
    public void InstallationFailed_ReturnsCorrectCode()
    {
        var error = Error.InstallationFailed("Download failed", "timeout");

        error.Code.Should().Be(ErrorCode.InstallationFailed);
        error.Message.Should().Be("Download failed");
        error.Details.Should().Be("timeout");
    }

    // External service errors

    [Fact]
    public void ExternalService_FormatsServiceAndMessage()
    {
        var error = Error.ExternalService("CurseForge", "API rate limited");

        error.Code.Should().Be(ErrorCode.ExternalServiceError);
        error.Message.Should().Be("CurseForge: API rate limited");
    }

    [Fact]
    public void SteamCmd_ReturnsCorrectCode()
    {
        var error = Error.SteamCmd("Login failed");

        error.Code.Should().Be(ErrorCode.SteamCmdError);
        error.Message.Should().Be("Login failed");
    }

    [Fact]
    public void Docker_ReturnsCorrectCode()
    {
        var error = Error.Docker("Container not found", "id=abc123");

        error.Code.Should().Be(ErrorCode.DockerError);
        error.Message.Should().Be("Container not found");
        error.Details.Should().Be("id=abc123");
    }

    [Fact]
    public void Rcon_ReturnsCorrectCode()
    {
        var error = Error.Rcon("Connection refused");

        error.Code.Should().Be(ErrorCode.RconError);
        error.Message.Should().Be("Connection refused");
    }

    // Generic factory

    [Fact]
    public void FromException_CapturesMessageAndStackTrace()
    {
        Exception ex;
        try
        {
            throw new InvalidOperationException("test error");
        }
        catch (Exception caught)
        {
            ex = caught;
        }

        var error = Error.FromException(ex);

        error.Code.Should().Be(ErrorCode.Unknown);
        error.Message.Should().Be("test error");
        error.Details.Should().NotBeNullOrEmpty();
        error.Details.Should().Contain("FromException_CapturesMessageAndStackTrace");
    }
}
