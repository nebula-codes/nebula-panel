using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for Hytale server installation through the wizard.
/// </summary>
public interface IHytaleServerInstallService
{
    /// <summary>
    /// Gets the available Hytale server version for a patchline.
    /// </summary>
    /// <param name="patchline">The patchline (e.g., "release" or "pre-release").</param>
    /// <param name="userId">The user ID to get credentials for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The version info, or null if unavailable.</returns>
    Task<HytaleVersionInfo?> GetVersionAsync(
        string patchline,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets version information for all available patchlines.
    /// </summary>
    /// <param name="userId">The user ID to get credentials for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of patchline to version info.</returns>
    Task<IReadOnlyDictionary<string, HytaleVersionInfo>> GetAllVersionsAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Validates wizard data before installation.
    /// </summary>
    /// <param name="data">The wizard data to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing any validation errors.</returns>
    Task<Result<IReadOnlyList<string>>> ValidateWizardDataAsync(
        HytaleServerWizardData data,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if a port is available on the local machine.
    /// </summary>
    /// <param name="port">The port to check.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsPortAvailableAsync(int port, CancellationToken ct = default);

    /// <summary>
    /// Creates and installs a Hytale server from wizard data.
    /// Returns the server ID on success.
    /// </summary>
    /// <param name="data">The wizard data.</param>
    /// <param name="ownerId">The owner user ID.</param>
    /// <param name="installationId">Unique ID for this installation (for progress tracking).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result<Guid>> InstallServerAsync(
        HytaleServerWizardData data,
        Guid ownerId,
        Guid installationId,
        CancellationToken ct = default);

    /// <summary>
    /// Cancels an ongoing installation.
    /// </summary>
    /// <param name="installationId">The installation ID to cancel.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result> CancelInstallationAsync(
        Guid installationId,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the default install path for a new Hytale server.
    /// </summary>
    /// <param name="serverName">The server name.</param>
    string GetDefaultInstallPath(string serverName);

    /// <summary>
    /// Gets available patchline options for UI display.
    /// </summary>
    IReadOnlyList<HytalePatchlineInfo> GetPatchlineInfos();
}

/// <summary>
/// Information about a Hytale patchline for UI display.
/// </summary>
public record HytalePatchlineInfo(
    string Patchline,
    string DisplayName,
    string Description,
    bool IsRecommended = false
);
