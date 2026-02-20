using System.IO.Compression;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.FileManagement;

/// <summary>
/// Manages file operations for game servers with security validations.
/// </summary>
public class ServerFileManager(ILogger<ServerFileManager> logger) : IServerFileManager
{
    private readonly ILogger<ServerFileManager> _logger = logger;

    /// <summary>
    /// File extensions that can be edited in the web UI.
    /// </summary>
    private static readonly HashSet<string> EditableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".json", ".yaml", ".yml", ".xml", ".properties", ".cfg", ".conf",
        ".ini", ".toml", ".sh", ".bat", ".cmd", ".ps1", ".lua", ".js", ".ts",
        ".py", ".java", ".cs", ".md", ".log", ".csv", ".env", ".htaccess"
    };

    /// <summary>
    /// Maximum file size for text editing (5MB).
    /// </summary>
    private const long MaxEditableFileSize = 5 * 1024 * 1024;

    public async Task<FileSystemEntry> GetDirectoryContentsAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        var dirInfo = new DirectoryInfo(fullPath);
        if (!dirInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
        }

        var entries = new List<FileSystemEntry>();

        // Add directories first, sorted by name
        foreach (var dir in dirInfo.GetDirectories().OrderBy(d => d.Name))
        {
            ct.ThrowIfCancellationRequested();
            entries.Add(CreateDirectoryEntry(server, dir));
        }

        // Then add files, sorted by name
        foreach (var file in dirInfo.GetFiles().OrderBy(f => f.Name))
        {
            ct.ThrowIfCancellationRequested();
            entries.Add(CreateFileEntry(server, file));
        }

        // At root level, append synthetic entries for Docker volume mounts
        if (string.IsNullOrEmpty(relativePath) && server.DockerConfig?.Volumes != null)
        {
            var volumes = server.DockerConfig.Volumes
                .Where(v => !v.IsNamedVolume && !string.IsNullOrEmpty(v.HostPath))
                .ToList();

            for (int i = 0; i < volumes.Count; i++)
            {
                var vol = volumes[i];
                if (!Directory.Exists(vol.HostPath)) continue;

                var volDir = new DirectoryInfo(vol.HostPath);
                entries.Add(new FileSystemEntry
                {
                    Name = $"[Volume] {vol.ContainerPath}",
                    Path = $"vol:{i}",
                    Type = FileEntryType.Directory,
                    ModifiedAt = volDir.LastWriteTimeUtc,
                    IsHidden = false
                });
            }
        }

        return new FileSystemEntry
        {
            Name = string.IsNullOrEmpty(relativePath) ? server.Name : dirInfo.Name,
            Path = NormalizePath(relativePath),
            Type = FileEntryType.Directory,
            ModifiedAt = dirInfo.LastWriteTimeUtc,
            Children = entries
        };
    }

    public async Task<string> ReadFileAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        if (fileInfo.Length > MaxEditableFileSize)
        {
            throw new InvalidOperationException(
                $"File is too large to read ({FormatFileSize(fileInfo.Length)}). Maximum size is {FormatFileSize(MaxEditableFileSize)}.");
        }

        return await File.ReadAllTextAsync(fullPath, ct).ConfigureAwait(false);
    }

    public async Task WriteFileAsync(
        GameServer server,
        string relativePath,
        string content,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        // Ensure parent directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, content, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "File written: {Path} for server {ServerId}",
            relativePath, server.Id);
    }

    public Task<Stream> DownloadFileAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        return Task.FromResult<Stream>(stream);
    }

    public async Task UploadFileAsync(
        GameServer server,
        string relativePath,
        Stream content,
        long length,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        // Ensure parent directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var fileStream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await content.CopyToAsync(fileStream, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "File uploaded: {Path} ({Size}) for server {ServerId}",
            relativePath, FormatFileSize(length), server.Id);
    }

    public Task DeleteAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new InvalidOperationException("Cannot delete the root directory.");
        }

        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation(
                "File deleted: {Path} for server {ServerId}",
                relativePath, server.Id);
        }
        else if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
            _logger.LogInformation(
                "Directory deleted: {Path} for server {ServerId}",
                relativePath, server.Id);
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {relativePath}");
        }

        return Task.CompletedTask;
    }

    public Task RenameAsync(
        GameServer server,
        string relativePath,
        string newName,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new InvalidOperationException("Cannot rename the root directory.");
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New name cannot be empty.", nameof(newName));
        }

        // Validate new name doesn't contain path separators
        if (newName.Contains('/') || newName.Contains('\\'))
        {
            throw new ArgumentException("New name cannot contain path separators.", nameof(newName));
        }

        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        var parentDir = Path.GetDirectoryName(fullPath);
        var baseRoot = server.InstallPath;
        var volumeResult = TryResolveVolumePrefix(server, relativePath);
        if (volumeResult.HasValue)
            baseRoot = volumeResult.Value.BasePath;
        var newPath = Path.Combine(parentDir ?? baseRoot, newName);

        // Validate the new path is also within bounds
        ValidatePath(server, newPath);

        if (File.Exists(fullPath))
        {
            if (File.Exists(newPath))
            {
                throw new InvalidOperationException($"A file named '{newName}' already exists.");
            }
            File.Move(fullPath, newPath);
        }
        else if (Directory.Exists(fullPath))
        {
            if (Directory.Exists(newPath))
            {
                throw new InvalidOperationException($"A directory named '{newName}' already exists.");
            }
            Directory.Move(fullPath, newPath);
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {relativePath}");
        }

        _logger.LogInformation(
            "Renamed: {OldPath} -> {NewName} for server {ServerId}",
            relativePath, newName, server.Id);

        return Task.CompletedTask;
    }

    public Task CreateDirectoryAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new InvalidOperationException("Directory path cannot be empty.");
        }

        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Directory already exists: {relativePath}");
        }

        if (File.Exists(fullPath))
        {
            throw new InvalidOperationException($"A file with this name already exists: {relativePath}");
        }

        Directory.CreateDirectory(fullPath);

        _logger.LogInformation(
            "Directory created: {Path} for server {ServerId}",
            relativePath, server.Id);

        return Task.CompletedTask;
    }

    public async Task<byte[]> CreateArchiveAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        using var memoryStream = new MemoryStream();
        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            if (File.Exists(fullPath))
            {
                // Archive single file
                var fileName = Path.GetFileName(fullPath);
                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
                await using var entryStream = entry.Open();
                await using var fileStream = File.OpenRead(fullPath);
                await fileStream.CopyToAsync(entryStream, ct).ConfigureAwait(false);
            }
            else if (Directory.Exists(fullPath))
            {
                // Archive directory
                await AddDirectoryToArchiveAsync(archive, fullPath, "", ct).ConfigureAwait(false);
            }
            else
            {
                throw new FileNotFoundException($"Path not found: {relativePath}");
            }
        }

        _logger.LogInformation(
            "Archive created for: {Path} ({Size}) for server {ServerId}",
            relativePath, FormatFileSize(memoryStream.Length), server.Id);

        return memoryStream.ToArray();
    }

    public async Task ExtractArchiveAsync(
        GameServer server,
        string relativePath,
        Stream archive,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);
        }

        using var zipArchive = new ZipArchive(archive, ZipArchiveMode.Read);

        foreach (var entry in zipArchive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            // Skip directory entries
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var entryPath = Path.Combine(fullPath, entry.FullName);

            // Validate extracted path is within bounds (prevent zip slip attack)
            ValidatePath(server, entryPath);

            // Ensure directory exists
            var entryDir = Path.GetDirectoryName(entryPath);
            if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
            {
                Directory.CreateDirectory(entryDir);
            }

            await using var entryStream = entry.Open();
            await using var fileStream = File.Create(entryPath);
            await entryStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Archive extracted to: {Path} for server {ServerId}",
            relativePath, server.Id);
    }

    public Task<FileSystemEntry> GetInfoAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);
        ValidatePath(server, fullPath);

        if (File.Exists(fullPath))
        {
            return Task.FromResult(CreateFileEntry(server, new FileInfo(fullPath)));
        }

        if (Directory.Exists(fullPath))
        {
            return Task.FromResult(CreateDirectoryEntry(server, new DirectoryInfo(fullPath)));
        }

        throw new FileNotFoundException($"Path not found: {relativePath}");
    }

    public Task<bool> ExistsAsync(
        GameServer server,
        string relativePath,
        CancellationToken ct = default)
    {
        var fullPath = ResolvePath(server, relativePath);

        try
        {
            ValidatePath(server, fullPath);
            return Task.FromResult(File.Exists(fullPath) || Directory.Exists(fullPath));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    public Task CopyAsync(
        GameServer server,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default)
    {
        var fullSource = ResolvePath(server, sourcePath);
        ValidatePath(server, fullSource);

        var fullDestination = ResolvePath(server, destinationPath);
        ValidatePath(server, fullDestination);

        if (File.Exists(fullSource))
        {
            if (File.Exists(fullDestination) || Directory.Exists(fullDestination))
            {
                throw new InvalidOperationException($"Destination already exists: {destinationPath}");
            }

            var destDir = Path.GetDirectoryName(fullDestination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Copy(fullSource, fullDestination);
        }
        else if (Directory.Exists(fullSource))
        {
            if (File.Exists(fullDestination) || Directory.Exists(fullDestination))
            {
                throw new InvalidOperationException($"Destination already exists: {destinationPath}");
            }

            CopyDirectoryRecursive(fullSource, fullDestination);
        }
        else
        {
            throw new FileNotFoundException($"Source not found: {sourcePath}");
        }

        _logger.LogInformation(
            "Copied: {Source} -> {Destination} for server {ServerId}",
            sourcePath, destinationPath, server.Id);

        return Task.CompletedTask;
    }

    public Task MoveAsync(
        GameServer server,
        string sourcePath,
        string destinationPath,
        CancellationToken ct = default)
    {
        var fullSource = ResolvePath(server, sourcePath);
        ValidatePath(server, fullSource);

        var fullDestination = ResolvePath(server, destinationPath);
        ValidatePath(server, fullDestination);

        if (File.Exists(fullSource))
        {
            if (File.Exists(fullDestination) || Directory.Exists(fullDestination))
            {
                throw new InvalidOperationException($"Destination already exists: {destinationPath}");
            }

            var destDir = Path.GetDirectoryName(fullDestination);
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            File.Move(fullSource, fullDestination);
        }
        else if (Directory.Exists(fullSource))
        {
            if (File.Exists(fullDestination) || Directory.Exists(fullDestination))
            {
                throw new InvalidOperationException($"Destination already exists: {destinationPath}");
            }

            Directory.Move(fullSource, fullDestination);
        }
        else
        {
            throw new FileNotFoundException($"Source not found: {sourcePath}");
        }

        _logger.LogInformation(
            "Moved: {Source} -> {Destination} for server {ServerId}",
            sourcePath, destinationPath, server.Id);

        return Task.CompletedTask;
    }

    public async Task ExtractArchiveInPlaceAsync(
        GameServer server,
        string archivePath,
        string targetPath,
        CancellationToken ct = default)
    {
        var fullArchivePath = ResolvePath(server, archivePath);
        ValidatePath(server, fullArchivePath);

        if (!File.Exists(fullArchivePath))
        {
            throw new FileNotFoundException($"Archive not found: {archivePath}");
        }

        await using var stream = new FileStream(
            fullArchivePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        await ExtractArchiveAsync(server, targetPath, stream, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "Archive extracted in-place: {ArchivePath} -> {TargetPath} for server {ServerId}",
            archivePath, targetPath, server.Id);
    }

    #region Private Methods

    private const string VolumePrefixBase = "vol:";

    /// <summary>
    /// Gets all allowed root paths for a server (InstallPath + Docker volume mount host paths).
    /// </summary>
    private static IReadOnlyList<string> GetAllowedRoots(GameServer server)
    {
        var roots = new List<string> { server.InstallPath };
        if (server.DockerConfig?.Volumes != null)
        {
            foreach (var vol in server.DockerConfig.Volumes)
            {
                if (!vol.IsNamedVolume && !string.IsNullOrEmpty(vol.HostPath)
                    && Directory.Exists(vol.HostPath))
                    roots.Add(vol.HostPath);
            }
        }
        return roots;
    }

    /// <summary>
    /// Tries to resolve a volume prefix (e.g. "vol:0/some/path") to a base path and relative path.
    /// Returns null if the path doesn't use a volume prefix.
    /// </summary>
    private static (string BasePath, string RelativePath)? TryResolveVolumePrefix(
        GameServer server, string path)
    {
        if (!path.StartsWith(VolumePrefixBase))
            return null;

        var afterPrefix = path[VolumePrefixBase.Length..];
        var slashIdx = afterPrefix.IndexOf('/');
        var indexStr = slashIdx >= 0 ? afterPrefix[..slashIdx] : afterPrefix;
        var rest = slashIdx >= 0 ? afterPrefix[(slashIdx + 1)..] : "";

        if (!int.TryParse(indexStr, out var volIndex))
            return null;

        var volumes = server.DockerConfig?.Volumes?
            .Where(v => !v.IsNamedVolume && !string.IsNullOrEmpty(v.HostPath))
            .ToList();

        if (volumes == null || volIndex < 0 || volIndex >= volumes.Count)
            return null;

        return (volumes[volIndex].HostPath, rest);
    }

    /// <summary>
    /// Computes a relative path with volume prefix if the full path is under a volume mount.
    /// Falls back to relative-to-InstallPath for paths under the install directory.
    /// </summary>
    private static string GetPrefixedRelativePath(GameServer server, string fullPath)
    {
        var normalizedFull = Path.GetFullPath(fullPath);
        var normalizedInstall = Path.GetFullPath(server.InstallPath).TrimEnd(Path.DirectorySeparatorChar);

        // Check if under InstallPath first
        if (normalizedFull.StartsWith(normalizedInstall + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            normalizedFull.Equals(normalizedInstall, StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetRelativePath(server.InstallPath, fullPath);
        }

        // Check volume mounts
        if (server.DockerConfig?.Volumes != null)
        {
            var volumes = server.DockerConfig.Volumes
                .Where(v => !v.IsNamedVolume && !string.IsNullOrEmpty(v.HostPath))
                .ToList();

            for (int i = 0; i < volumes.Count; i++)
            {
                var normalizedVolRoot = Path.GetFullPath(volumes[i].HostPath).TrimEnd(Path.DirectorySeparatorChar);
                if (normalizedFull.StartsWith(normalizedVolRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    normalizedFull.Equals(normalizedVolRoot, StringComparison.OrdinalIgnoreCase))
                {
                    var rel = Path.GetRelativePath(volumes[i].HostPath, fullPath);
                    return $"vol:{i}/{rel}";
                }
            }
        }

        // Fallback
        return Path.GetRelativePath(server.InstallPath, fullPath);
    }

    /// <summary>
    /// Resolves a relative path to an absolute path within the server's install directory
    /// or a Docker volume mount host path (via "vol:N/" prefix).
    /// </summary>
    private static string ResolvePath(GameServer server, string relativePath)
    {
        // Check for volume mount prefix
        var volumeResult = TryResolveVolumePrefix(server, relativePath ?? "");
        if (volumeResult.HasValue)
        {
            var (basePath, rest) = volumeResult.Value;
            rest = rest.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            return string.IsNullOrEmpty(rest) ? basePath : Path.Combine(basePath, rest);
        }

        // Default: resolve against InstallPath
        relativePath = relativePath?.Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(Path.DirectorySeparatorChar) ?? "";

        return Path.Combine(server.InstallPath, relativePath);
    }

    /// <summary>
    /// Normalizes a path to use forward slashes.
    /// </summary>
    private static string NormalizePath(string path)
    {
        return path?.Replace('\\', '/').Trim('/') ?? "";
    }

    /// <summary>
    /// Validates that the resolved path is within the server's install directory
    /// or an allowed Docker volume mount host path.
    /// Throws UnauthorizedAccessException if path traversal is detected.
    /// </summary>
    private void ValidatePath(GameServer server, string fullPath)
    {
        var normalizedPath = Path.GetFullPath(fullPath);

        foreach (var root in GetAllowedRoots(server))
        {
            var normalizedBase = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            if (normalizedPath.StartsWith(normalizedBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        _logger.LogWarning(
            "Path traversal attempt detected. Server: {ServerId}, AttemptedPath: {Path}, InstallPath: {BasePath}",
            server.Id, fullPath, server.InstallPath);

        throw new UnauthorizedAccessException("Access denied: path traversal detected.");
    }

    /// <summary>
    /// Creates a FileSystemEntry for a file.
    /// </summary>
    private FileSystemEntry CreateFileEntry(GameServer server, FileInfo file)
    {
        var relativePath = GetPrefixedRelativePath(server, file.FullName);
        var extension = file.Extension.ToLowerInvariant();

        return new FileSystemEntry
        {
            Name = file.Name,
            Path = NormalizePath(relativePath),
            Type = FileEntryType.File,
            Size = file.Length,
            ModifiedAt = file.LastWriteTimeUtc,
            Extension = extension,
            MimeType = GetMimeType(extension),
            IsEditable = IsFileEditable(file),
            IsHidden = file.Name.StartsWith('.') || (file.Attributes & FileAttributes.Hidden) != 0
        };
    }

    /// <summary>
    /// Creates a FileSystemEntry for a directory.
    /// </summary>
    private FileSystemEntry CreateDirectoryEntry(GameServer server, DirectoryInfo dir)
    {
        var relativePath = GetPrefixedRelativePath(server, dir.FullName);

        return new FileSystemEntry
        {
            Name = dir.Name,
            Path = NormalizePath(relativePath),
            Type = FileEntryType.Directory,
            ModifiedAt = dir.LastWriteTimeUtc,
            IsHidden = dir.Name.StartsWith('.') || (dir.Attributes & FileAttributes.Hidden) != 0
        };
    }

    /// <summary>
    /// Determines if a file can be edited in the web UI.
    /// </summary>
    private static bool IsFileEditable(FileInfo file)
    {
        return EditableExtensions.Contains(file.Extension) &&
               file.Length <= MaxEditableFileSize;
    }

    /// <summary>
    /// Gets the MIME type for a file extension.
    /// </summary>
    private static string GetMimeType(string extension) => extension switch
    {
        ".json" => "application/json",
        ".xml" => "application/xml",
        ".yaml" or ".yml" => "application/x-yaml",
        ".properties" => "text/x-java-properties",
        ".cfg" or ".conf" or ".ini" => "text/plain",
        ".toml" => "application/toml",
        ".lua" => "text/x-lua",
        ".js" => "application/javascript",
        ".ts" => "application/typescript",
        ".py" => "text/x-python",
        ".java" => "text/x-java",
        ".cs" => "text/x-csharp",
        ".md" => "text/markdown",
        ".sh" => "application/x-sh",
        ".bat" or ".cmd" => "application/x-bat",
        ".ps1" => "application/x-powershell",
        ".log" or ".txt" => "text/plain",
        ".csv" => "text/csv",
        ".env" => "text/plain",
        ".zip" => "application/zip",
        ".tar" => "application/x-tar",
        ".gz" => "application/gzip",
        ".7z" => "application/x-7z-compressed",
        ".jar" => "application/java-archive",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".svg" => "image/svg+xml",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
    /// </summary>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Recursively copies a directory.
    /// </summary>
    private static void CopyDirectoryRecursive(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }

    /// <summary>
    /// Recursively adds a directory to a zip archive.
    /// </summary>
    private static async Task AddDirectoryToArchiveAsync(
        ZipArchive archive,
        string sourceDir,
        string entryPrefix,
        CancellationToken ct)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();

            var entryName = string.IsNullOrEmpty(entryPrefix)
                ? Path.GetFileName(file)
                : $"{entryPrefix}/{Path.GetFileName(file)}";

            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();
            await using var fileStream = File.OpenRead(file);
            await fileStream.CopyToAsync(entryStream, ct).ConfigureAwait(false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();

            var dirName = Path.GetFileName(dir);
            var newPrefix = string.IsNullOrEmpty(entryPrefix) ? dirName : $"{entryPrefix}/{dirName}";

            await AddDirectoryToArchiveAsync(archive, dir, newPrefix, ct).ConfigureAwait(false);
        }
    }

    #endregion
}
