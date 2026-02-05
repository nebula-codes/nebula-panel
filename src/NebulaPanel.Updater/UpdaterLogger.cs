namespace NebulaPanel.Updater;

/// <summary>
/// Simple file-based logger for the updater process.
/// Writes to both console and file for troubleshooting.
/// </summary>
internal sealed class UpdaterLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _logPath;
    private bool _disposed;

    /// <summary>
    /// Path to the current log file.
    /// </summary>
    public string LogPath => _logPath;

    public UpdaterLogger(string appDir)
    {
        var logDir = Path.Combine(appDir, "data", "logs");
        Directory.CreateDirectory(logDir);

        // Clean up old logs (keep last 10)
        CleanupOldLogs(logDir);

        _logPath = Path.Combine(logDir, $"updater-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
        _writer = new StreamWriter(_logPath, append: false) { AutoFlush = true };

        Log("INFO", "Updater log started");
        Log("INFO", $"Log file: {_logPath}");
    }

    public void Log(string level, string message)
    {
        var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
        Console.WriteLine(line);

        if (!_disposed)
        {
            try
            {
                _writer.WriteLine(line);
            }
            catch
            {
                // Ignore write failures
            }
        }
    }

    public void Info(string message) => Log("INFO", message);
    public void Warn(string message) => Log("WARN", message);
    public void Error(string message) => Log("ERROR", message);

    public void Error(string message, Exception ex)
    {
        Log("ERROR", message);
        Log("ERROR", $"Exception: {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace is not null)
        {
            foreach (var line in ex.StackTrace.Split('\n'))
            {
                Log("ERROR", $"  {line.Trim()}");
            }
        }

        if (ex.InnerException is not null)
        {
            Log("ERROR", $"Inner Exception: {ex.InnerException.Message}");
        }
    }

    private static void CleanupOldLogs(string logDir)
    {
        try
        {
            var logFiles = Directory.GetFiles(logDir, "updater-*.log")
                .OrderByDescending(f => f)
                .Skip(10)
                .ToList();

            foreach (var file in logFiles)
            {
                try
                {
                    File.Delete(file);
                }
                catch
                {
                    // Ignore deletion failures
                }
            }
        }
        catch
        {
            // Ignore cleanup failures
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Log("INFO", "Updater log completed");

        try
        {
            _writer.Dispose();
        }
        catch
        {
            // Ignore dispose failures
        }
    }
}
