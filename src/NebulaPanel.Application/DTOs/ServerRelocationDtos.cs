using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Result of validating a server relocation request.
/// </summary>
public record ServerRelocationValidation(
    bool IsValid,
    string? ErrorMessage,
    long TotalSizeBytes,
    int TotalFileCount,
    long AvailableSpaceBytes,
    string OldPath,
    string NewPath
);

/// <summary>
/// Status of an active server relocation job.
/// </summary>
public record ServerRelocationStatus(
    Guid RelocationId,
    Guid ServerId,
    string OldPath,
    string NewPath,
    ServerRelocationProgress Progress,
    DateTime StartedAt,
    DateTime? CompletedAt
);

/// <summary>
/// Request to validate a server relocation.
/// </summary>
public record ValidateRelocationRequest(
    string NewPath
);

/// <summary>
/// Request to start a server relocation.
/// </summary>
public record StartRelocationRequest(
    string NewPath,
    bool DeleteOldFiles = true
);

/// <summary>
/// DTO for progress updates sent via SignalR.
/// </summary>
public record ServerRelocationProgressDto(
    Guid RelocationId,
    Guid ServerId,
    string State,
    double PercentComplete,
    string CurrentStep,
    string? CurrentFile,
    long BytesCopied,
    long TotalBytes,
    int FilesCopied,
    int TotalFiles,
    string? ErrorMessage,
    bool IsIndeterminate
)
{
    public static ServerRelocationProgressDto FromProgress(Guid relocationId, Guid serverId, ServerRelocationProgress progress) => new(
        relocationId,
        serverId,
        progress.State.ToString(),
        progress.PercentComplete,
        progress.CurrentStep,
        progress.CurrentFile,
        progress.BytesCopied,
        progress.TotalBytes,
        progress.FilesCopied,
        progress.TotalFiles,
        progress.ErrorMessage,
        progress.IsIndeterminate
    );
}
