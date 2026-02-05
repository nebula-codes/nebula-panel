using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Complete form data for Hytale server creation wizard.
/// </summary>
public class HytaleServerWizardData
{
    // Step 1: Version Selection
    /// <summary>
    /// The patchline to install from (e.g., "release" or "pre-release").
    /// </summary>
    public string Patchline { get; set; } = "release";

    /// <summary>
    /// The specific server version selected (e.g., "2025.01.31-abc1234").
    /// </summary>
    public string? ServerVersion { get; set; }

    // Step 2: Server Configuration
    /// <summary>
    /// Display name for the server.
    /// </summary>
    public string ServerName { get; set; } = "";

    /// <summary>
    /// Directory where server files will be installed.
    /// </summary>
    public string InstallPath { get; set; } = "";

    /// <summary>
    /// Game port for the server.
    /// </summary>
    public int Port { get; set; } = 5520;

    /// <summary>
    /// IP address to bind the server to.
    /// </summary>
    public string BindAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// Minimum memory allocation in MB.
    /// </summary>
    public int MinMemoryMb { get; set; } = 2048;

    /// <summary>
    /// Maximum memory allocation in MB.
    /// </summary>
    public int MaxMemoryMb { get; set; } = 4096;

    /// <summary>
    /// Custom JVM arguments (e.g., "-XX:+UseG1GC -Dfile.encoding=UTF-8").
    /// These are appended after memory arguments.
    /// </summary>
    public string? CustomJvmArguments { get; set; }

    /// <summary>
    /// Whether to run as native process or Docker container.
    /// </summary>
    public ServerDeploymentType DeploymentType { get; set; } = ServerDeploymentType.Native;

    // Docker-specific settings (if DeploymentType == Docker)
    /// <summary>
    /// Docker image name (if using Docker deployment).
    /// </summary>
    public string? DockerImage { get; set; }

    /// <summary>
    /// Docker image tag (if using Docker deployment).
    /// </summary>
    public string? DockerTag { get; set; }
}

/// <summary>
/// Installation progress DTO for SignalR broadcast during Hytale server installation.
/// </summary>
public record HytaleInstallProgressDto
{
    /// <summary>
    /// Unique identifier for this installation.
    /// </summary>
    public required Guid InstallationId { get; init; }

    /// <summary>
    /// Current phase of installation (Preparing, Authenticating, Downloading, Extracting, Configuring, Complete).
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Bytes downloaded so far (for download phase).
    /// </summary>
    public long? BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download (for download phase).
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// When this progress update was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the installation is complete (success or failure).
    /// </summary>
    public bool IsComplete { get; init; }

    /// <summary>
    /// Whether the installation ended in error.
    /// </summary>
    public bool HasError { get; init; }

    /// <summary>
    /// Error message if HasError is true.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static HytaleInstallProgressDto Starting(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Preparing",
        Message = "Initializing installation...",
        Percentage = 0
    };

    public static HytaleInstallProgressDto ValidatingCredentials(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Validating",
        Message = "Validating Hytale account credentials...",
        Percentage = 2
    };

    public static HytaleInstallProgressDto Authenticating(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Authenticating",
        Message = "Authenticating with Hytale servers...",
        Percentage = 5
    };

    public static HytaleInstallProgressDto CreatingServerRecord(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Creating",
        Message = "Creating server configuration...",
        Percentage = 8
    };

    public static HytaleInstallProgressDto FetchingVersion(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Fetching",
        Message = "Retrieving latest server version from Hytale...",
        Percentage = 10
    };

    public static HytaleInstallProgressDto StartingDownload(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Downloading",
        Message = "Starting download from Hytale servers...",
        Percentage = 12
    };

    public static HytaleInstallProgressDto Downloading(
        Guid installationId,
        double percentage,
        long? bytesDownloaded = null,
        long? totalBytes = null)
    {
        var message = "Downloading server files...";
        if (bytesDownloaded.HasValue && totalBytes.HasValue && totalBytes.Value > 0)
        {
            var mbDownloaded = bytesDownloaded.Value / (1024.0 * 1024.0);
            var mbTotal = totalBytes.Value / (1024.0 * 1024.0);
            message = $"Downloading: {mbDownloaded:F1} MB / {mbTotal:F1} MB ({percentage:F0}%)";
        }

        return new()
        {
            InstallationId = installationId,
            Phase = "Downloading",
            Message = message,
            Percentage = 12 + (percentage * 0.68), // 12-80% range
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes
        };
    }

    public static HytaleInstallProgressDto Verifying(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Verifying",
        Message = "Verifying downloaded files...",
        Percentage = 82
    };

    public static HytaleInstallProgressDto Extracting(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Extracting",
        Message = "Extracting server files to installation directory...",
        Percentage = 85
    };

    public static HytaleInstallProgressDto Configuring(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Configuring",
        Message = "Applying server configuration...",
        Percentage = 92
    };

    public static HytaleInstallProgressDto SavingToDatabase(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Saving",
        Message = "Saving server to database...",
        Percentage = 96
    };

    public static HytaleInstallProgressDto Finalizing(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Finalizing",
        Message = "Finalizing installation...",
        Percentage = 98
    };

    public static HytaleInstallProgressDto Completed(Guid installationId) => new()
    {
        InstallationId = installationId,
        Phase = "Complete",
        Message = "Server installed successfully!",
        Percentage = 100,
        IsComplete = true
    };

    public static HytaleInstallProgressDto Failed(Guid installationId, string error) => new()
    {
        InstallationId = installationId,
        Phase = "Failed",
        Message = error,
        Percentage = 0,
        IsComplete = true,
        HasError = true,
        ErrorMessage = error
    };
}

/// <summary>
/// Result of Hytale server installation.
/// </summary>
public record HytaleInstallResult
{
    /// <summary>
    /// Whether the installation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The ID of the created server (if successful).
    /// </summary>
    public Guid? ServerId { get; init; }

    /// <summary>
    /// Error message (if failed).
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// The installed server version.
    /// </summary>
    public string? InstalledVersion { get; init; }

    /// <summary>
    /// The path where the server was installed.
    /// </summary>
    public string? InstalledPath { get; init; }
}
