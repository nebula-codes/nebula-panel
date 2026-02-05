namespace NebulaPanel.Domain.ValueObjects;

public class NativeConfiguration
{
    public string WorkingDirectory { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public string? JavaPath { get; set; }               // For JAR files
    public string? JavaArguments { get; set; }          // -Xmx4G -Xms2G
    public bool RunAsService { get; set; }
    public string? RunAsUser { get; set; }              // Linux user to run as

    // Startup script support (for modpacks with custom .bat/.sh scripts)
    public bool UseStartupScript { get; set; }          // Use script instead of jar
    public string? StartupScriptPath { get; set; }      // e.g., "start.bat", "run.sh"
}
