namespace NebulaPanel.Application.Common;

/// <summary>
/// Error codes for categorizing different types of failures.
/// </summary>
public enum ErrorCode
{
    // General errors (0-99)
    Unknown = 0,

    // Validation errors (100-199)
    ValidationError = 100,
    InvalidInput = 101,
    InvalidOperation = 102,

    // Resource errors (200-299)
    NotFound = 200,
    AlreadyExists = 201,
    Conflict = 202,

    // Authentication/Authorization errors (300-399)
    Unauthorized = 300,
    Forbidden = 301,
    InvalidCredentials = 302,
    TokenExpired = 303,
    AccountLocked = 304,
    RateLimited = 305,
    SessionExpired = 306,
    AccountDisabled = 307,

    // Server operation errors (400-499)
    ServerRunning = 400,
    ServerStopped = 401,
    ServerNotInstalled = 402,
    InstallationFailed = 403,
    PortInUse = 404,

    // External service errors (500-599)
    ExternalServiceError = 500,
    SteamCmdError = 501,
    DockerError = 502,
    RconError = 503
}

/// <summary>
/// Represents a typed error with code, message, and optional details.
/// </summary>
public record Error(ErrorCode Code, string Message, string? Details = null)
{
    // Resource errors
    public static Error NotFound(string resource, string? identifier = null) =>
        new(ErrorCode.NotFound, identifier is not null
            ? $"{resource} with ID '{identifier}' not found"
            : $"{resource} not found");

    public static Error AlreadyExists(string resource, string? identifier = null) =>
        new(ErrorCode.AlreadyExists, identifier is not null
            ? $"{resource} '{identifier}' already exists"
            : $"{resource} already exists");

    public static Error Conflict(string message, string? details = null) =>
        new(ErrorCode.Conflict, message, details);

    // Validation errors
    public static Error Validation(string message, string? details = null) =>
        new(ErrorCode.ValidationError, message, details);

    public static Error InvalidInput(string message, string? details = null) =>
        new(ErrorCode.InvalidInput, message, details);

    public static Error InvalidOperation(string message, string? details = null) =>
        new(ErrorCode.InvalidOperation, message, details);

    // Auth errors
    public static Error Unauthorized(string? message = null) =>
        new(ErrorCode.Unauthorized, message ?? "Authentication required");

    public static Error Forbidden(string? message = null) =>
        new(ErrorCode.Forbidden, message ?? "Access denied");

    public static Error InvalidCredentials(string? message = null) =>
        new(ErrorCode.InvalidCredentials, message ?? "Invalid username or password");

    public static Error TokenExpired() =>
        new(ErrorCode.TokenExpired, "Token has expired");

    public static Error AccountLocked(DateTime? lockoutEndTime = null) =>
        new(ErrorCode.AccountLocked,
            lockoutEndTime.HasValue
                ? $"Account is locked until {lockoutEndTime.Value:u}"
                : "Account is locked due to too many failed login attempts",
            lockoutEndTime?.ToString("o"));

    public static Error RateLimited(int retryAfterSeconds) =>
        new(ErrorCode.RateLimited,
            $"Too many login attempts. Please try again in {retryAfterSeconds} seconds.",
            retryAfterSeconds.ToString());

    public static Error SessionExpired() =>
        new(ErrorCode.SessionExpired, "Your session has expired. Please log in again.");

    public static Error AccountDisabled() =>
        new(ErrorCode.AccountDisabled, "This account has been disabled.");

    // Server operation errors
    public static Error ServerRunning(string? operation = null) =>
        new(ErrorCode.ServerRunning, operation is not null
            ? $"Cannot {operation} while server is running"
            : "Server must be stopped to perform this operation");

    public static Error ServerStopped(string? operation = null) =>
        new(ErrorCode.ServerStopped, operation is not null
            ? $"Cannot {operation} while server is stopped"
            : "Server must be running to perform this operation");

    public static Error ServerNotInstalled() =>
        new(ErrorCode.ServerNotInstalled, "Server is not installed");

    public static Error InstallationFailed(string message, string? details = null) =>
        new(ErrorCode.InstallationFailed, message, details);

    public static Error PortInUse(int port) =>
        new(ErrorCode.PortInUse, $"Port {port} is already in use");

    // External service errors
    public static Error ExternalService(string service, string message, string? details = null) =>
        new(ErrorCode.ExternalServiceError, $"{service}: {message}", details);

    public static Error SteamCmd(string message, string? details = null) =>
        new(ErrorCode.SteamCmdError, message, details);

    public static Error Docker(string message, string? details = null) =>
        new(ErrorCode.DockerError, message, details);

    public static Error Rcon(string message, string? details = null) =>
        new(ErrorCode.RconError, message, details);

    // Generic factory
    public static Error FromException(Exception ex) =>
        new(ErrorCode.Unknown, ex.Message, ex.StackTrace);
}
