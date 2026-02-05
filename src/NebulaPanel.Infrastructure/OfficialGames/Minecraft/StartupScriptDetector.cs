using System.Text.RegularExpressions;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft;

/// <summary>
/// Represents a parsed Java command from a startup script.
/// </summary>
public record ParsedJavaCommand(
    string? JavaPath,
    List<string> JvmArgs,
    string? JarFile,
    List<string> ProgramArgs,
    bool UsesArgsFile,
    string? ArgsFilePath
);

/// <summary>
/// Detects available startup scripts in a Minecraft server directory.
/// Many modpacks include custom .bat or .sh scripts with optimized JVM flags.
/// </summary>
public static partial class StartupScriptDetector
{
    /// <summary>
    /// Common Windows startup script names used by modpacks.
    /// </summary>
    private static readonly string[] WindowsScripts =
    [
        "start.bat",
        "run.bat",
        "ServerStart.bat",
        "start-server.bat",
        "launch.bat",
        "startserver.bat",
        "StartServer.bat"
    ];

    /// <summary>
    /// Common Linux/macOS startup script names used by modpacks.
    /// </summary>
    private static readonly string[] LinuxScripts =
    [
        "start.sh",
        "run.sh",
        "ServerStart.sh",
        "start-server.sh",
        "launch.sh",
        "startserver.sh",
        "StartServer.sh"
    ];

    /// <summary>
    /// Detects available startup scripts in the specified server directory.
    /// Returns scripts appropriate for the current operating system.
    /// </summary>
    /// <param name="serverPath">Path to the server directory.</param>
    /// <returns>List of detected script file names (not full paths).</returns>
    public static IReadOnlyList<string> DetectScripts(string serverPath)
    {
        if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
        {
            return [];
        }

        var scripts = OperatingSystem.IsWindows() ? WindowsScripts : LinuxScripts;

        return scripts
            .Where(s => File.Exists(Path.Combine(serverPath, s)))
            .ToList();
    }

    /// <summary>
    /// Detects all startup scripts regardless of OS (for display purposes).
    /// </summary>
    /// <param name="serverPath">Path to the server directory.</param>
    /// <returns>List of all detected script file names.</returns>
    public static IReadOnlyList<string> DetectAllScripts(string serverPath)
    {
        if (string.IsNullOrEmpty(serverPath) || !Directory.Exists(serverPath))
        {
            return [];
        }

        var allScripts = WindowsScripts.Concat(LinuxScripts).Distinct();

        return allScripts
            .Where(s => File.Exists(Path.Combine(serverPath, s)))
            .ToList();
    }

    /// <summary>
    /// Checks if a specific script exists in the server directory.
    /// </summary>
    /// <param name="serverPath">Path to the server directory.</param>
    /// <param name="scriptName">Name of the script file.</param>
    /// <returns>True if the script exists.</returns>
    public static bool ScriptExists(string serverPath, string scriptName)
    {
        if (string.IsNullOrEmpty(serverPath) || string.IsNullOrEmpty(scriptName))
        {
            return false;
        }

        return File.Exists(Path.Combine(serverPath, scriptName));
    }

    /// <summary>
    /// Parses a startup script and extracts the Java command.
    /// Works with both .bat and .sh files.
    /// </summary>
    /// <param name="scriptPath">Full path to the script file.</param>
    /// <returns>Parsed Java command or null if parsing fails.</returns>
    public static ParsedJavaCommand? ParseStartupScript(string scriptPath)
    {
        if (!File.Exists(scriptPath))
            return null;

        try
        {
            var content = File.ReadAllText(scriptPath);
            var isBatch = scriptPath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                          scriptPath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);

            return isBatch ? ParseBatchScript(content) : ParseShellScript(content);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses a Windows batch script to extract the Java command.
    /// </summary>
    private static ParsedJavaCommand? ParseBatchScript(string content)
    {
        // Look for java command lines
        // Common patterns:
        // java -Xmx4G -jar server.jar nogui
        // java @user_jvm_args.txt @libraries/net/minecraftforge/forge/1.20.1-47.2.0/win_args.txt %*
        // "%JAVA_HOME%\bin\java" -Xmx4G -jar forge.jar

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip comments and echo commands
            if (line.StartsWith("@echo", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("::") ||
                line.StartsWith("pause", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("set ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Look for java command
            if (line.Contains("java", StringComparison.OrdinalIgnoreCase))
            {
                return ParseJavaCommandLine(line);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a shell script to extract the Java command.
    /// </summary>
    private static ParsedJavaCommand? ParseShellScript(string content)
    {
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip comments, shebang, and common shell commands
            if (line.StartsWith('#') ||
                line.StartsWith("echo ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("cd ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("export ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("if ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("while ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("read ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Look for java command
            if (line.Contains("java", StringComparison.OrdinalIgnoreCase))
            {
                return ParseJavaCommandLine(line);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses a Java command line and extracts components.
    /// </summary>
    private static ParsedJavaCommand? ParseJavaCommandLine(string line)
    {
        var jvmArgs = new List<string>();
        var programArgs = new List<string>();
        string? javaPath = null;
        string? jarFile = null;
        bool usesArgsFile = false;
        string? argsFilePath = null;

        // Remove batch/shell specific stuff
        line = line.Replace("%*", "").Replace("$@", "").Replace("\"$@\"", "");
        line = line.Replace("%JAVA_HOME%", "").Replace("$JAVA_HOME", "");

        // Tokenize the command (simple tokenization, handles quotes)
        var tokens = TokenizeCommand(line);
        var afterJar = false;

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            // Skip empty tokens
            if (string.IsNullOrWhiteSpace(token))
                continue;

            // Detect java executable
            if (token.Contains("java", StringComparison.OrdinalIgnoreCase) &&
                (token.EndsWith("java", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith("java.exe", StringComparison.OrdinalIgnoreCase) ||
                 token.EndsWith("javaw.exe", StringComparison.OrdinalIgnoreCase)))
            {
                javaPath = token;
                continue;
            }

            // Detect @args files (Forge/NeoForge style)
            if (token.StartsWith('@'))
            {
                usesArgsFile = true;
                argsFilePath ??= token[1..]; // Store first args file
                jvmArgs.Add(token);
                continue;
            }

            // Detect -jar flag
            if (token.Equals("-jar", StringComparison.OrdinalIgnoreCase))
            {
                // Next token is the jar file
                if (i + 1 < tokens.Count)
                {
                    jarFile = tokens[i + 1];
                    i++; // Skip the jar file token
                }
                afterJar = true;
                continue;
            }

            // JVM arguments start with -
            if (token.StartsWith('-') && !afterJar)
            {
                jvmArgs.Add(token);
                continue;
            }

            // After -jar, everything is program arguments
            if (afterJar)
            {
                programArgs.Add(token);
            }
        }

        // Return null if we didn't find anything useful
        if (jarFile == null && !usesArgsFile)
            return null;

        return new ParsedJavaCommand(javaPath, jvmArgs, jarFile, programArgs, usesArgsFile, argsFilePath);
    }

    /// <summary>
    /// Tokenizes a command line, handling quoted strings.
    /// </summary>
    private static List<string> TokenizeCommand(string line)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var quoteChar = '"';

        foreach (var c in line)
        {
            if ((c == '"' || c == '\'') && !inQuotes)
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (c == quoteChar && inQuotes)
            {
                inQuotes = false;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }

    /// <summary>
    /// Builds a Docker-compatible command from a parsed startup script.
    /// Converts Windows-specific paths and removes incompatible arguments.
    /// </summary>
    public static IList<string> BuildDockerCommand(ParsedJavaCommand parsed)
    {
        var command = new List<string> { "java" };

        foreach (var arg in parsed.JvmArgs)
        {
            // Convert Windows args file paths to Unix paths
            if (arg.StartsWith('@'))
            {
                var argsFile = arg[1..];
                // Convert backslashes to forward slashes
                argsFile = argsFile.Replace('\\', '/');
                // Use unix_args.txt instead of win_args.txt
                argsFile = argsFile.Replace("win_args.txt", "unix_args.txt");
                command.Add($"@{argsFile}");
            }
            else
            {
                command.Add(arg);
            }
        }

        if (parsed.JarFile != null)
        {
            command.Add("-jar");
            // Convert backslashes in jar path
            command.Add(parsed.JarFile.Replace('\\', '/'));
        }

        foreach (var arg in parsed.ProgramArgs)
        {
            // Skip Windows-specific things
            if (arg == "pause" || arg.StartsWith('%'))
                continue;
            command.Add(arg);
        }

        // Always add nogui if not present
        if (!command.Any(a => a.Equals("nogui", StringComparison.OrdinalIgnoreCase)))
        {
            command.Add("nogui");
        }

        return command;
    }
}
