namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Indicates whether a game is an official (pre-configured) or custom (user-created) game.
/// </summary>
public enum GameSourceType
{
    /// <summary>
    /// Official game that ships with Nebula Panel. Configuration is managed by providers
    /// and cannot be modified by users.
    /// </summary>
    Official,

    /// <summary>
    /// Custom game created by the user. Fully editable.
    /// </summary>
    Custom
}
