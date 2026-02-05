namespace NebulaPanel.Application.Services;

using NebulaPanel.Application.DTOs;

/// <summary>
/// Manages Hytale game session lifecycle (create/refresh/end).
/// Sessions are required for authenticating dedicated servers.
/// </summary>
public interface IHytaleSessionManager
{
    /// <summary>
    /// Ensures the user has a valid game session, creating one if needed.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The full credentials including session tokens.</returns>
    Task<HytaleFullCredentials?> EnsureSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new game session for the user.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the session was created successfully.</returns>
    Task<bool> CreateSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Refreshes an existing game session.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the session was refreshed successfully.</returns>
    Task<bool> RefreshSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Ends the user's game session.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EndSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Gets the current session tokens for the user (decrypted).
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Tuple of (sessionToken, identityToken) or null if no valid session.</returns>
    Task<(string SessionToken, string IdentityToken)?> GetSessionTokensAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Checks if the user has a valid (non-expired) game session.
    /// </summary>
    /// <param name="userId">The Nebula Panel user ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if a valid session exists.</returns>
    Task<bool> HasValidSessionAsync(Guid userId, CancellationToken ct = default);
}
