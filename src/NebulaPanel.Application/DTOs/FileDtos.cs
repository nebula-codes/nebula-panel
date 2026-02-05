namespace NebulaPanel.Application.DTOs;

/// <summary>
/// DTO representing a file system entry.
/// </summary>
public record FileSystemEntryDto
{
    /// <summary>
    /// The name of the file or directory.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The relative path from the server root.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The type: "file" or "directory".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// File size in bytes (null for directories).
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    /// Last modification timestamp (UTC).
    /// </summary>
    public DateTime ModifiedAt { get; init; }

    /// <summary>
    /// MIME type for files.
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Whether the file can be edited in the web UI.
    /// </summary>
    public bool IsEditable { get; init; }

    /// <summary>
    /// Whether the entry is hidden.
    /// </summary>
    public bool IsHidden { get; init; }

    /// <summary>
    /// File extension (including dot).
    /// </summary>
    public string? Extension { get; init; }

    /// <summary>
    /// Child entries for directories.
    /// </summary>
    public IReadOnlyList<FileSystemEntryDto>? Children { get; init; }
}

/// <summary>
/// Response containing file content and metadata.
/// </summary>
public record ReadFileResponse
{
    /// <summary>
    /// The relative path of the file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The file content as text.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// The MIME type of the file.
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt { get; init; }

    /// <summary>
    /// The file extension.
    /// </summary>
    public string? Extension { get; init; }
}

/// <summary>
/// Request to write file content.
/// </summary>
public record WriteFileRequest
{
    /// <summary>
    /// The relative path of the file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The content to write.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Whether to create the file if it doesn't exist.
    /// </summary>
    public bool CreateIfNotExists { get; init; } = true;
}

/// <summary>
/// Request to rename a file or directory.
/// </summary>
public record RenameRequest
{
    /// <summary>
    /// The current relative path.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The new name (not path).
    /// </summary>
    public required string NewName { get; init; }
}

/// <summary>
/// Request to create a directory.
/// </summary>
public record CreateDirectoryRequest
{
    /// <summary>
    /// The relative path for the new directory.
    /// </summary>
    public required string Path { get; init; }
}

/// <summary>
/// Result of a single file upload.
/// </summary>
public record UploadResult
{
    /// <summary>
    /// The relative path where the file was uploaded.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// The original file name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Whether the upload was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if upload failed.
    /// </summary>
    public string? Error { get; init; }
}

/// <summary>
/// Result of bulk file upload.
/// </summary>
public record BulkUploadResult
{
    /// <summary>
    /// Results for each file.
    /// </summary>
    public required IReadOnlyList<UploadResult> Results { get; init; }

    /// <summary>
    /// Number of successful uploads.
    /// </summary>
    public int SuccessCount { get; init; }

    /// <summary>
    /// Number of failed uploads.
    /// </summary>
    public int FailureCount { get; init; }
}

/// <summary>
/// Information about a file download.
/// </summary>
public record FileDownloadInfo
{
    /// <summary>
    /// The file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    /// The MIME type.
    /// </summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// The file content stream.
    /// </summary>
    public required Stream Content { get; init; }

    /// <summary>
    /// The file size in bytes.
    /// </summary>
    public long Size { get; init; }
}
