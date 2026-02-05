using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Shared;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Service that handles post-update tasks when the application starts after an update.
/// </summary>
public class PostUpdateService(
    ILogger<PostUpdateService> logger,
    IUpdateHistoryService? historyService = null,
    IUpdateNotifier? notifier = null,
    IGameServerService? gameServerService = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets whether a "What's New" notification should be shown.
    /// </summary>
    public bool ShouldShowWhatsNew { get; private set; }

    /// <summary>
    /// Gets the version to show in "What's New".
    /// </summary>
    public string? WhatsNewVersion { get; private set; }

    /// <summary>
    /// Gets the release notes to show in "What's New".
    /// </summary>
    public string? WhatsNewReleaseNotes { get; private set; }

    /// <summary>
    /// Handles any pending post-update tasks.
    /// Should be called on application startup.
    /// </summary>
    public async Task HandlePostUpdateAsync(CancellationToken cancellationToken = default)
    {
        var updateDir = Path.Combine(AppContext.BaseDirectory, "data", "updates");
        var completionPath = Path.Combine(updateDir, "completed-update.json");

        if (!File.Exists(completionPath))
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(completionPath, cancellationToken).ConfigureAwait(false);
            var marker = JsonSerializer.Deserialize<UpdateCompletionMarker>(json, JsonOptions);

            if (marker is null)
            {
                logger.LogWarning("Invalid update completion marker");
                File.Delete(completionPath);
                return;
            }

            logger.LogInformation("Detected completed update to version {Version}", marker.Version);

            var success = !marker.HadErrors && !marker.HealthCheckFailed;
            var errorMessage = marker.HadErrors
                ? string.Join(", ", marker.Errors)
                : marker.HealthCheckFailed
                    ? "Application health check failed after update"
                    : null;

            if (!success)
            {
                logger.LogWarning("Update had issues: {Error}", errorMessage);
            }

            // Record update completion in history
            if (historyService is not null && marker.HistoryId.HasValue)
            {
                await historyService.RecordUpdateCompletionAsync(
                    marker.HistoryId.Value,
                    success,
                    errorMessage,
                    cancellationToken).ConfigureAwait(false);
            }

            // Restart game servers that were running before the update
            if (gameServerService is not null && marker.RestartServerIds.Count > 0)
            {
                logger.LogInformation("Restarting {Count} game servers after update", marker.RestartServerIds.Count);

                foreach (var serverId in marker.RestartServerIds)
                {
                    try
                    {
                        logger.LogInformation("Restarting server {ServerId}", serverId);
                        await gameServerService.StartServerAsync(serverId, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to restart server {ServerId}", serverId);
                    }
                }
            }

            // Set up "What's New" notification if update was successful
            if (success && !string.IsNullOrEmpty(marker.ReleaseNotes))
            {
                ShouldShowWhatsNew = true;
                WhatsNewVersion = marker.Version;
                WhatsNewReleaseNotes = marker.ReleaseNotes;

                // Notify connected clients
                if (notifier is not null)
                {
                    await notifier.NotifyShowWhatsNewAsync(
                        marker.Version,
                        marker.ReleaseNotes,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            // Delete the completion marker
            File.Delete(completionPath);
            logger.LogInformation("Post-update tasks completed");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process post-update tasks");
            // Try to delete the marker to prevent repeated failures
            try { File.Delete(completionPath); } catch { /* ignore */ }
        }
    }
}
