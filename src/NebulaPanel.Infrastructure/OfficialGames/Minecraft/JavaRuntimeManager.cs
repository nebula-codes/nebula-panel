using System.Collections.Concurrent;
using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Downloads and manages portable JRE installations from Eclipse Adoptium (Temurin).
/// Each major Java version is cached in its own subdirectory.
/// </summary>
public class JavaRuntimeManager(
    IHttpClientFactory httpClientFactory,
    ILogger<JavaRuntimeManager> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<JavaRuntimeManager> _logger = logger;

    private static readonly string DefaultBasePath = Path.Combine(
        AppContext.BaseDirectory, "data", "java");

    private readonly ConcurrentDictionary<int, SemaphoreSlim> _downloadLocks = new();

    /// <summary>
    /// Ensures a JRE of the given major version is installed and returns the path to the java executable.
    /// Downloads from Adoptium if not already cached.
    /// </summary>
    /// <param name="majorVersion">Java major version (e.g. 8, 17, 21).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Absolute path to the java executable.</returns>
    public async Task<string> EnsureInstalledAsync(
        int majorVersion,
        CancellationToken cancellationToken = default)
    {
        var versionDir = GetVersionDirectory(majorVersion);

        // Fast path: already downloaded
        var existingJava = FindJavaExecutable(versionDir);
        if (existingJava is not null)
            return existingJava;

        // Serialize downloads per version to avoid duplicate work
        var semaphore = _downloadLocks.GetOrAdd(majorVersion, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Re-check after acquiring lock (another thread may have completed download)
            existingJava = FindJavaExecutable(versionDir);
            if (existingJava is not null)
                return existingJava;

            _logger.LogInformation("Auto-downloading Java {Version} from Adoptium...", majorVersion);

            Directory.CreateDirectory(versionDir);

            var downloadUrl = BuildAdoptiumUrl(majorVersion);
            var client = _httpClientFactory.CreateClient("Adoptium");

            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength;
            if (contentLength.HasValue)
            {
                _logger.LogInformation("Downloading Java {Version} JRE ({Size:F1} MB)...",
                    majorVersion, contentLength.Value / 1024.0 / 1024.0);
            }

            // Download to temp file then extract
            var tempFile = Path.Combine(versionDir, "download.tmp");
            try
            {
                await using (var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                await using (var fileStream = File.Create(tempFile))
                {
                    await downloadStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                }

                // Extract based on OS
                if (OperatingSystem.IsWindows())
                {
                    ExtractZip(tempFile, versionDir);
                }
                else
                {
                    await ExtractTarGzAsync(tempFile, versionDir, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); }
                    catch { /* ignore cleanup failure */ }
                }
            }

            // Find and validate the extracted java executable
            var javaPath = FindJavaExecutable(versionDir)
                ?? throw new InvalidOperationException(
                    $"Java executable not found after extracting JRE {majorVersion} to {versionDir}");

            // Ensure executable permission on Unix
            if (!OperatingSystem.IsWindows())
            {
                MakeExecutable(javaPath);
            }

            _logger.LogInformation("Java {Version} installed at {Path}", majorVersion, javaPath);
            return javaPath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to download Java {Version} from Adoptium", majorVersion);
            throw;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static string GetVersionDirectory(int majorVersion) =>
        Path.Combine(DefaultBasePath, majorVersion.ToString());

    /// <summary>
    /// Searches a version directory for the java executable inside the extracted JDK/JRE folder.
    /// </summary>
    private static string? FindJavaExecutable(string versionDir)
    {
        if (!Directory.Exists(versionDir))
            return null;

        var javaName = OperatingSystem.IsWindows() ? "java.exe" : "java";

        // Look for bin/java inside any subdirectory (the extracted archive creates a named folder)
        foreach (var subDir in Directory.EnumerateDirectories(versionDir))
        {
            var javaPath = Path.Combine(subDir, "bin", javaName);
            if (File.Exists(javaPath))
                return javaPath;
        }

        // Also check direct bin/ in case of flat extraction
        var directPath = Path.Combine(versionDir, "bin", javaName);
        return File.Exists(directPath) ? directPath : null;
    }

    private static string BuildAdoptiumUrl(int majorVersion)
    {
        var os = GetAdoptiumOs();
        var arch = GetAdoptiumArch();

        return $"https://api.adoptium.net/v3/binary/latest/{majorVersion}/ga/{os}/{arch}/jre/hotspot/normal/eclipse";
    }

    private static string GetAdoptiumOs()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "mac";
        return "linux";
    }

    private static string GetAdoptiumArch()
    {
        return RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "aarch64",
            Architecture.X86 => "x32",
            _ => "x64"
        };
    }

    private static void ExtractZip(string zipPath, string targetDir)
    {
        ZipFile.ExtractToDirectory(zipPath, targetDir, overwriteFiles: true);
    }

    private static async Task ExtractTarGzAsync(
        string tarGzPath, string targetDir, CancellationToken cancellationToken)
    {
        await using var fileStream = File.OpenRead(tarGzPath);
        await using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);
        await TarFile.ExtractToDirectoryAsync(gzipStream, targetDir, overwriteFiles: true, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void MakeExecutable(string filePath)
    {
        try
        {
            File.SetUnixFileMode(filePath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
        catch
        {
            // Fallback: ignore if the API isn't available
        }
    }
}
