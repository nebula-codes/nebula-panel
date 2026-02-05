using System.IO.Compression;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.FileManagement;

/// <summary>
/// Manages backup file operations for game servers.
/// </summary>
public class BackupFileManager(ILogger<BackupFileManager> logger) : IBackupFileManager
{
    private readonly ILogger<BackupFileManager> _logger = logger;

    public async Task<string> CreateBackupArchiveAsync(
        GameServer server,
        string backupDirectory,
        string backupName,
        IEnumerable<string>? selectivePaths = null,
        CancellationToken cancellationToken = default)
    {
        EnsureBackupDirectoryExists(backupDirectory);

        var backupFileName = $"{backupName}.zip";
        var backupFilePath = Path.Combine(backupDirectory, backupFileName);

        _logger.LogInformation(
            "Creating backup archive for server {ServerName} ({ServerId}) at {BackupPath}",
            server.Name, server.Id, backupFilePath);

        try
        {
            await using var fileStream = new FileStream(
                backupFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Create, leaveOpen: false);

            if (selectivePaths?.Any() == true)
            {
                // Selective backup: only include specified paths
                await CreateSelectiveBackupAsync(archive, server, selectivePaths, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                // Full backup: include entire server directory
                await AddDirectoryToArchiveAsync(archive, server.InstallPath, "", cancellationToken)
                    .ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Backup archive created successfully: {BackupPath} for server {ServerName}",
                backupFilePath, server.Name);

            return backupFilePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create backup archive for server {ServerName} ({ServerId})",
                server.Name, server.Id);

            // Clean up partial backup file on failure
            if (File.Exists(backupFilePath))
            {
                try
                {
                    File.Delete(backupFilePath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            throw;
        }
    }

    public async Task ExtractBackupArchiveAsync(
        GameServer server,
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
        }

        _logger.LogInformation(
            "Extracting backup archive {BackupPath} to server {ServerName} ({ServerId})",
            backupFilePath, server.Name, server.Id);

        try
        {
            await using var fileStream = new FileStream(
                backupFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);

            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Skip directory entries
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                var destinationPath = Path.Combine(server.InstallPath, entry.FullName);

                // Validate extracted path is within bounds (prevent zip slip attack)
                ValidatePathWithinBounds(server.InstallPath, destinationPath);

                // Ensure directory exists
                var entryDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(entryDir) && !Directory.Exists(entryDir))
                {
                    Directory.CreateDirectory(entryDir);
                }

                await using var entryStream = entry.Open();
                await using var outputStream = File.Create(destinationPath);
                await entryStream.CopyToAsync(outputStream, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Backup archive extracted successfully to server {ServerName}",
                server.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to extract backup archive {BackupPath} to server {ServerName}",
                backupFilePath, server.Name);
            throw;
        }
    }

    public Task DeleteBackupFileAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(backupFilePath))
        {
            File.Delete(backupFilePath);
            _logger.LogInformation("Deleted backup file: {BackupPath}", backupFilePath);
        }

        return Task.CompletedTask;
    }

    public Task<Stream> OpenBackupStreamAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
        }

        var stream = new FileStream(
            backupFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            useAsync: true);

        return Task.FromResult<Stream>(stream);
    }

    public long GetBackupFileSize(string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
        {
            return 0;
        }

        return new FileInfo(backupFilePath).Length;
    }

    public bool BackupFileExists(string backupFilePath)
    {
        return File.Exists(backupFilePath);
    }

    public void EnsureBackupDirectoryExists(string backupDirectory)
    {
        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
            _logger.LogInformation("Created backup directory: {BackupDirectory}", backupDirectory);
        }
    }

    public async Task<IReadOnlyList<string>> GetBackupContentsAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
        }

        await using var fileStream = new FileStream(
            backupFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

        return archive.Entries
            .Select(e => e.FullName)
            .OrderBy(name => name)
            .ToList();
    }

    #region Private Methods

    private async Task CreateSelectiveBackupAsync(
        ZipArchive archive,
        GameServer server,
        IEnumerable<string> selectivePaths,
        CancellationToken cancellationToken)
    {
        foreach (var relativePath in selectivePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = relativePath.Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            var fullPath = Path.Combine(server.InstallPath, normalizedPath);

            // Validate path is within server directory
            ValidatePathWithinBounds(server.InstallPath, fullPath);

            if (File.Exists(fullPath))
            {
                // Add single file
                await AddFileToArchiveAsync(archive, fullPath, normalizedPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (Directory.Exists(fullPath))
            {
                // Add directory recursively
                await AddDirectoryToArchiveAsync(archive, fullPath, normalizedPath, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning(
                    "Selective backup path not found, skipping: {Path} for server {ServerName}",
                    relativePath, server.Name);
            }
        }
    }

    private async Task<bool> AddFileToArchiveAsync(
        ZipArchive archive,
        string filePath,
        string entryName,
        CancellationToken cancellationToken)
    {
        // Normalize entry name to use forward slashes
        entryName = entryName.Replace('\\', '/');

        try
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            await using var entryStream = entry.Open();

            // Use FileShare.ReadWrite to allow reading files that are open by other processes
            await using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite,
                bufferSize: 81920,
                useAsync: true);

            await fileStream.CopyToAsync(entryStream, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException ex) when (IsFileLockedException(ex))
        {
            _logger.LogWarning(
                "Skipping locked file during backup: {FilePath} - {Message}",
                filePath, ex.Message);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                "Skipping inaccessible file during backup: {FilePath} - {Message}",
                filePath, ex.Message);
            return false;
        }
    }

    private async Task AddDirectoryToArchiveAsync(
        ZipArchive archive,
        string sourceDir,
        string entryPrefix,
        CancellationToken cancellationToken)
    {
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entryName = string.IsNullOrEmpty(entryPrefix)
                ? Path.GetFileName(file)
                : $"{entryPrefix}/{Path.GetFileName(file)}";

            await AddFileToArchiveAsync(archive, file, entryName, cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirName = Path.GetFileName(dir);
            var newPrefix = string.IsNullOrEmpty(entryPrefix) ? dirName : $"{entryPrefix}/{dirName}";

            await AddDirectoryToArchiveAsync(archive, dir, newPrefix, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool IsFileLockedException(IOException ex)
    {
        // Common HResult values for file locking errors
        // 0x80070020 = ERROR_SHARING_VIOLATION (32)
        // 0x80070021 = ERROR_LOCK_VIOLATION (33)
        var errorCode = ex.HResult & 0xFFFF;
        return errorCode == 32 || errorCode == 33;
    }

    private void ValidatePathWithinBounds(string basePath, string targetPath)
    {
        var normalizedBase = Path.GetFullPath(basePath).TrimEnd(Path.DirectorySeparatorChar);
        var normalizedTarget = Path.GetFullPath(targetPath);

        if (!normalizedTarget.StartsWith(normalizedBase + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !normalizedTarget.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Path traversal attempt detected during backup. TargetPath: {TargetPath}, BasePath: {BasePath}",
                targetPath, basePath);

            throw new UnauthorizedAccessException("Access denied: path traversal detected.");
        }
    }

    #endregion
}
