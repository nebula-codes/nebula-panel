using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using NebulaPanel.Shared;

namespace NebulaPanel.Updater;

public static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Nebula Panel Updater");
        Console.WriteLine("====================");

        var appDir = args.Length > 0 ? args[0] : FindAppDirectory();

        if (string.IsNullOrEmpty(appDir) || !Directory.Exists(appDir))
        {
            Console.WriteLine($"Error: Application directory not found: {appDir}");
            return 1;
        }

        // Initialize logger early
        using var logger = new UpdaterLogger(appDir);
        logger.Info($"Application directory: {appDir}");

        var updateDir = Path.Combine(appDir, "data", "updates");
        var manifestPath = Path.Combine(updateDir, "pending-update.json");

        if (!File.Exists(manifestPath))
        {
            logger.Info("No pending update found.");
            return 0;
        }

        UpdateManifest? manifest;
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath).ConfigureAwait(false);
            manifest = JsonSerializer.Deserialize<UpdateManifest>(json, JsonOptions);
            if (manifest is null)
            {
                logger.Error("Invalid update manifest - deserialization returned null.");
                return 1;
            }
        }
        catch (Exception ex)
        {
            logger.Error("Error reading manifest", ex);
            return 1;
        }

        logger.Info($"Update manifest loaded - Target version: {manifest.Version}");
        logger.Info($"Download path: {manifest.DownloadPath}");
        logger.Info($"Backup ID: {manifest.BackupId ?? "(none)"}");

        var completionMarker = new UpdateCompletionMarker
        {
            Version = manifest.Version,
            CompletedAt = DateTime.UtcNow,
            RestartServerIds = manifest.RestartGameServers ? manifest.RestartServerIds : []
        };

        try
        {
            // Pre-flight checks
            logger.Info("Running pre-flight checks...");

            // Check archive exists
            if (!File.Exists(manifest.DownloadPath))
            {
                throw new FileNotFoundException($"Update archive not found: {manifest.DownloadPath}");
            }

            // Check disk space
            var archiveSize = new FileInfo(manifest.DownloadPath).Length;
            logger.Info($"Archive size: {archiveSize:N0} bytes");

            if (!HasSufficientDiskSpace(appDir, archiveSize, logger))
            {
                throw new InvalidOperationException(
                    $"Insufficient disk space. Required: {archiveSize * 2:N0} bytes (2x archive size for extraction)");
            }

            // Verify archive integrity
            logger.Info("Verifying archive integrity...");
            if (!await VerifyArchiveAsync(manifest.DownloadPath, logger).ConfigureAwait(false))
            {
                throw new InvalidOperationException("Archive integrity check failed - file may be corrupted");
            }

            // Wait for main process to fully exit
            logger.Info("Waiting for main process to exit...");
            await WaitForProcessExitAsync("NebulaPanel.Web", TimeSpan.FromSeconds(30), logger).ConfigureAwait(false);

            // Extract update
            logger.Info("Extracting files...");
            var extractDir = Path.Combine(updateDir, "extracted");
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, recursive: true);
            }
            Directory.CreateDirectory(extractDir);

            try
            {
                await ExtractTarGzAsync(manifest.DownloadPath, extractDir).ConfigureAwait(false);

                // Verify extraction produced files
                var extractedFiles = Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories);
                if (extractedFiles.Length == 0)
                {
                    throw new InvalidOperationException("Extraction produced no files");
                }
                logger.Info($"Extracted {extractedFiles.Length} files");
            }
            catch (Exception ex)
            {
                logger.Error("Extraction failed, cleaning up partial extraction", ex);
                if (Directory.Exists(extractDir))
                {
                    try { Directory.Delete(extractDir, recursive: true); } catch { /* ignore */ }
                }
                throw;
            }

            // Replace files (preserve data directory and local config)
            logger.Info("Replacing application files...");
            var preserveItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "data",
                "appsettings.local.json",
                "appsettings.Development.json"
            };

            // Delete old files (except preserved)
            var deletedFiles = 0;
            foreach (var file in Directory.GetFiles(appDir))
            {
                var fileName = Path.GetFileName(file);
                if (!preserveItems.Contains(fileName))
                {
                    File.Delete(file);
                    deletedFiles++;
                }
            }

            var deletedDirs = 0;
            foreach (var dir in Directory.GetDirectories(appDir))
            {
                var dirName = Path.GetFileName(dir);
                if (!preserveItems.Contains(dirName))
                {
                    Directory.Delete(dir, recursive: true);
                    deletedDirs++;
                }
            }
            logger.Info($"Removed {deletedFiles} files and {deletedDirs} directories");

            // Copy new files from extracted directory
            CopyDirectory(extractDir, appDir, overwrite: true);
            logger.Info("New files copied successfully");

            // Set executable permissions on Linux
            SetExecutablePermissions(appDir, logger);

            // Run database migrations if needed
            logger.Info("Checking database migrations...");
            await RunMigrationsAsync(appDir, logger).ConfigureAwait(false);

            // Clean up
            logger.Info("Cleaning up...");
            File.Delete(manifestPath);
            if (File.Exists(manifest.DownloadPath))
            {
                File.Delete(manifest.DownloadPath);
            }
            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, recursive: true);
            }

            // Write completion marker
            var completionPath = Path.Combine(updateDir, "completed-update.json");
            await File.WriteAllTextAsync(
                completionPath,
                JsonSerializer.Serialize(completionMarker, JsonOptions)).ConfigureAwait(false);
            logger.Info($"Completion marker written to: {completionPath}");

            // Start the main application
            logger.Info("Starting Nebula Panel...");
            var processStarted = StartMainApplication(appDir, logger);

            if (processStarted)
            {
                // Wait for the application to become healthy
                logger.Info("Waiting for application to become healthy...");
                var isHealthy = await WaitForHealthCheckAsync(appDir, logger).ConfigureAwait(false);

                if (!isHealthy)
                {
                    logger.Warn("Application did not become healthy within timeout, marking update as potentially failed");
                    completionMarker = completionMarker with
                    {
                        HealthCheckFailed = true
                    };

                    // Rewrite completion marker with health check failure
                    var completionPathHealthFail = Path.Combine(updateDir, "completed-update.json");
                    await File.WriteAllTextAsync(
                        completionPathHealthFail,
                        JsonSerializer.Serialize(completionMarker, JsonOptions)).ConfigureAwait(false);
                }
            }

            logger.Info($"Update to {manifest.Version} completed successfully!");
            return 0;
        }
        catch (Exception ex)
        {
            logger.Error("Update failed", ex);

            completionMarker = completionMarker with
            {
                HadErrors = true,
                Errors = [ex.Message]
            };

            // Attempt rollback if backup exists
            if (!string.IsNullOrEmpty(manifest.BackupId))
            {
                logger.Warn("Attempting rollback from backup...");
                var rollbackSuccess = await RollbackAsync(appDir, manifest.BackupId, logger).ConfigureAwait(false);
                if (!rollbackSuccess)
                {
                    logger.Error("Rollback failed. Manual intervention may be required.");
                }
            }
            else
            {
                logger.Warn("No backup available for rollback.");
            }

            // Write error marker even on failure
            try
            {
                var completionPath = Path.Combine(updateDir, "completed-update.json");
                await File.WriteAllTextAsync(
                    completionPath,
                    JsonSerializer.Serialize(completionMarker, JsonOptions)).ConfigureAwait(false);
            }
            catch (Exception writeEx)
            {
                logger.Error("Failed to write error marker", writeEx);
            }

            // Try to start the application anyway
            logger.Info("Attempting to start application despite errors...");
            StartMainApplication(appDir, logger);

            return 1;
        }
    }

    private static string? FindAppDirectory()
    {
        // Try to find the app directory relative to the updater
        var currentDir = AppContext.BaseDirectory;

        // Check if we're in the same directory as the main app
        if (File.Exists(Path.Combine(currentDir, "NebulaPanel.Web.dll")))
        {
            return currentDir;
        }

        // Check parent directory
        var parentDir = Path.GetDirectoryName(currentDir);
        if (parentDir is not null && File.Exists(Path.Combine(parentDir, "NebulaPanel.Web.dll")))
        {
            return parentDir;
        }

        return currentDir;
    }

    private static bool HasSufficientDiskSpace(string targetDir, long archiveSize, UpdaterLogger logger)
    {
        try
        {
            var root = Path.GetPathRoot(targetDir);
            if (string.IsNullOrEmpty(root))
            {
                logger.Warn("Could not determine disk root, skipping space check");
                return true;
            }

            var driveInfo = new DriveInfo(root);
            var availableBytes = driveInfo.AvailableFreeSpace;

            // Require 2x the archive size (for extraction + replacement)
            var requiredBytes = archiveSize * 2;

            logger.Info($"Available disk space: {availableBytes:N0} bytes");
            logger.Info($"Required disk space: {requiredBytes:N0} bytes");

            return availableBytes >= requiredBytes;
        }
        catch (Exception ex)
        {
            logger.Warn($"Could not check disk space: {ex.Message}");
            // If we can't check, proceed with the update
            return true;
        }
    }

    private static async Task<bool> VerifyArchiveAsync(string archivePath, UpdaterLogger logger)
    {
        try
        {
            var entryCount = 0;

            await Task.Run(() =>
            {
                using var sourceStream = File.OpenRead(archivePath);
                using var gzipStream = new GZipInputStream(sourceStream);
                using var tarInputStream = new TarInputStream(gzipStream, null);

                // Iterate through entries to verify they can be read
                TarEntry? entry;
                while ((entry = tarInputStream.GetNextEntry()) is not null)
                {
                    entryCount++;
                }
            }).ConfigureAwait(false);

            logger.Info($"Archive verified: {entryCount} entries found");
            return entryCount > 0;
        }
        catch (Exception ex)
        {
            logger.Error($"Archive verification failed: {ex.Message}");
            return false;
        }
    }

    private static async Task WaitForProcessExitAsync(string processName, TimeSpan timeout, UpdaterLogger logger)
    {
        var startTime = DateTime.UtcNow;
        var lastLogTime = DateTime.MinValue;

        while (DateTime.UtcNow - startTime < timeout)
        {
            var processes = Process.GetProcessesByName(processName);
            if (processes.Length == 0)
            {
                logger.Info($"Process {processName} has exited");
                return;
            }

            // Log every 5 seconds
            if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(5))
            {
                logger.Info($"Waiting for {processes.Length} instance(s) of {processName} to exit...");
                lastLogTime = DateTime.UtcNow;
            }

            foreach (var process in processes)
            {
                process.Dispose();
            }

            await Task.Delay(500).ConfigureAwait(false);
        }

        logger.Warn($"Process {processName} did not exit within {timeout.TotalSeconds}s timeout");

        // Force kill after timeout
        var remainingProcesses = Process.GetProcessesByName(processName);
        foreach (var process in remainingProcesses)
        {
            try
            {
                logger.Warn($"Force killing process {process.Id}");
                process.Kill();
            }
            catch (Exception ex)
            {
                logger.Warn($"Failed to kill process {process.Id}: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static async Task ExtractTarGzAsync(string sourcePath, string destinationDir)
    {
        await using var sourceStream = File.OpenRead(sourcePath);
        await using var gzipStream = new GZipInputStream(sourceStream);
        using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, null);
        tarArchive.ExtractContents(destinationDir);
    }

    private static void CopyDirectory(string source, string destination, bool overwrite)
    {
        var dir = new DirectoryInfo(source);

        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {source}");
        }

        Directory.CreateDirectory(destination);

        foreach (var file in dir.GetFiles())
        {
            var targetPath = Path.Combine(destination, file.Name);
            file.CopyTo(targetPath, overwrite);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var targetPath = Path.Combine(destination, subDir.Name);
            CopyDirectory(subDir.FullName, targetPath, overwrite);
        }
    }

    private static void SetExecutablePermissions(string appDir, UpdaterLogger logger)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            !RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return;
        }

        logger.Info("Setting executable permissions for Linux/macOS...");

        var executableFiles = new[]
        {
            "NebulaPanel.Web",
            "NebulaPanel.Updater",
            "start.sh"
        };

        foreach (var file in executableFiles)
        {
            var path = Path.Combine(appDir, file);
            if (File.Exists(path))
            {
                try
                {
                    var chmod = Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{path}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    });

                    if (chmod is not null)
                    {
                        chmod.WaitForExit(5000);
                        if (chmod.ExitCode == 0)
                        {
                            logger.Info($"Set executable permission: {file}");
                        }
                        else
                        {
                            logger.Warn($"chmod failed for {file} with exit code {chmod.ExitCode}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn($"Failed to set executable permission for {file}: {ex.Message}");
                }
            }
        }
    }

    private static async Task RunMigrationsAsync(string appDir, UpdaterLogger logger)
    {
        // The main application runs migrations on startup via EF Core's MigrateAsync.
        // Running migrations in the updater would require including the Infrastructure layer
        // and all its dependencies, which would significantly bloat the updater executable.
        // Therefore, we defer migrations to the main application startup.

        var dbPath = Path.Combine(appDir, "data", "nebula.db");
        if (File.Exists(dbPath))
        {
            var fileInfo = new FileInfo(dbPath);
            logger.Info($"Database file exists: {dbPath} ({fileInfo.Length:N0} bytes)");
            logger.Info("Migrations will run on application startup.");
        }
        else
        {
            logger.Info("No existing database found, will be created on first run.");
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    private static async Task<bool> RollbackAsync(string appDir, string backupId, UpdaterLogger logger)
    {
        var backupPath = Path.Combine(appDir, "data", "backups", "updates", backupId);

        if (!Directory.Exists(backupPath))
        {
            logger.Error($"Backup not found: {backupId}");
            return false;
        }

        logger.Info($"Rolling back from backup: {backupId}");

        try
        {
            // Restore database
            var dbBackup = Path.Combine(backupPath, "nebula.db");
            var dbTarget = Path.Combine(appDir, "data", "nebula.db");
            if (File.Exists(dbBackup))
            {
                File.Copy(dbBackup, dbTarget, overwrite: true);
                logger.Info("Restored database from backup");

                // Restore WAL and SHM files if they exist (SQLite journal files)
                var walBackup = Path.Combine(backupPath, "nebula.db-wal");
                var shmBackup = Path.Combine(backupPath, "nebula.db-shm");
                if (File.Exists(walBackup))
                {
                    File.Copy(walBackup, dbTarget + "-wal", overwrite: true);
                    logger.Info("Restored WAL file");
                }
                if (File.Exists(shmBackup))
                {
                    File.Copy(shmBackup, dbTarget + "-shm", overwrite: true);
                    logger.Info("Restored SHM file");
                }
            }
            else
            {
                logger.Warn("No database backup found in backup directory");
            }

            // Restore config files (to app root, not data directory)
            var configFiles = Directory.GetFiles(backupPath, "appsettings*.json");
            foreach (var configFile in configFiles)
            {
                var targetPath = Path.Combine(appDir, Path.GetFileName(configFile));
                File.Copy(configFile, targetPath, overwrite: true);
                logger.Info($"Restored config: {Path.GetFileName(configFile)}");
            }

            logger.Info("Rollback completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error("Rollback failed", ex);
            return false;
        }
    }

    private static bool StartMainApplication(string appDir, UpdaterLogger logger)
    {
        string executablePath;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            executablePath = Path.Combine(appDir, "NebulaPanel.Web.exe");
        }
        else
        {
            executablePath = Path.Combine(appDir, "NebulaPanel.Web");
        }

        if (!File.Exists(executablePath))
        {
            // Fall back to dotnet command
            var dllPath = Path.Combine(appDir, "NebulaPanel.Web.dll");
            logger.Info($"Native executable not found, using dotnet: {dllPath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"\"{dllPath}\"",
                WorkingDirectory = appDir,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            var process = Process.Start(startInfo);
            if (process is not null)
            {
                logger.Info($"Started main application via dotnet (PID: {process.Id})");
                return true;
            }
            else
            {
                logger.Error("Failed to start main application via dotnet");
                return false;
            }
        }

        logger.Info($"Starting: {executablePath}");

        var processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = appDir,
            UseShellExecute = false,
            CreateNoWindow = false
        };

        var startedProcess = Process.Start(processStartInfo);
        if (startedProcess is not null)
        {
            logger.Info($"Started main application (PID: {startedProcess.Id})");
            return true;
        }
        else
        {
            logger.Error("Failed to start main application");
            return false;
        }
    }

    private static async Task<bool> WaitForHealthCheckAsync(string appDir, UpdaterLogger logger)
    {
        const int maxAttempts = 60; // 60 seconds total
        const int delayMs = 1000;

        // Try to read the port from appsettings, default to 5000
        var port = 5000;
        var appsettingsPath = Path.Combine(appDir, "appsettings.json");
        if (File.Exists(appsettingsPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(appsettingsPath).ConfigureAwait(false);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Kestrel", out var kestrel) &&
                    kestrel.TryGetProperty("Endpoints", out var endpoints) &&
                    endpoints.TryGetProperty("Http", out var http) &&
                    http.TryGetProperty("Url", out var url))
                {
                    var urlValue = url.GetString();
                    if (urlValue is not null && Uri.TryCreate(urlValue, UriKind.Absolute, out var uri))
                    {
                        port = uri.Port;
                    }
                }
            }
            catch
            {
                // Ignore parsing errors, use default port
            }
        }

        var healthUrl = $"http://localhost:{port}/health";
        logger.Info($"Checking health at: {healthUrl}");

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var response = await httpClient.GetAsync(healthUrl).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    logger.Info($"Health check passed on attempt {attempt}");
                    return true;
                }

                logger.Info($"Health check attempt {attempt}/{maxAttempts}: HTTP {(int)response.StatusCode}");
            }
            catch (HttpRequestException ex)
            {
                logger.Info($"Health check attempt {attempt}/{maxAttempts}: {ex.Message}");
            }
            catch (TaskCanceledException)
            {
                logger.Info($"Health check attempt {attempt}/{maxAttempts}: Timeout");
            }

            if (attempt < maxAttempts)
            {
                await Task.Delay(delayMs).ConfigureAwait(false);
            }
        }

        logger.Warn($"Health check failed after {maxAttempts} attempts");
        return false;
    }
}
