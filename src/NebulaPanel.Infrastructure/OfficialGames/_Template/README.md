# Adding a New Official Game to Nebula Panel

This guide explains how to add support for a new official game to Nebula Panel.

## Choose Your Approach

### Option 1: JSON-Based Definition (Recommended for Simple Games)

Best for games that:
- Use SteamCMD for installation
- Have a simple version API or static versions
- Don't require complex custom logic

**Pros:**
- No C# code required
- Can be updated without recompilation
- Easy to maintain

**Cons:**
- Limited customization
- Cannot implement complex version fetching or installation logic

### Option 2: Code-Based Provider (Required for Complex Games)

Best for games that:
- Have multiple loaders/variants (like Minecraft: Vanilla, Paper, Fabric, etc.)
- Require custom version fetching logic from multiple APIs
- Need special installation procedures (e.g., Java detection, mod loader installation)

**Pros:**
- Full control over version fetching and installation
- Can implement complex business logic
- Better error handling

**Cons:**
- Requires C# development
- Must recompile to update

---

## Option 1: JSON-Based Game Definition

### Step 1: Create the Game Folder

Create a new folder under `src/NebulaPanel.Infrastructure/OfficialGames/`:

```
OfficialGames/
└── YourGame/
    ├── game.json           # Game definition (required)
    └── Schemas/
        └── config.schema.json  # Configuration schema (optional)
```

### Step 2: Create game.json

```json
{
  "schemaVersion": "1.0",
  "slug": "your-game",
  "name": "Your Game Name",
  "steamAppId": "123456",
  "executableType": "Exe",
  "defaultExecutablePath": "server.exe",
  "defaultStartCommand": "server.exe -port {port} -maxplayers {maxPlayers}",
  "defaultStopCommand": "quit",
  "supportsDocker": true,
  "defaultDockerImage": "yourimage/server",
  "iconPath": "/images/games/your-game.png",
  "supportsMods": false,
  "modProviders": [],
  "rconDefaults": {
    "defaultEnabled": true,
    "protocol": "Source",
    "defaultPort": 27015
  },
  "configSchemaFiles": {
    "server.cfg": "config.schema.json"
  },
  "versionSources": [
    {
      "type": "SteamCmd",
      "url": "123456"
    }
  ],
  "installConfig": {
    "type": "SteamCmd",
    "steamAppId": "123456",
    "validateAfterInstall": true,
    "postInstallCommands": []
  }
}
```

### Step 3: Create Configuration Schema (Optional)

Create `Schemas/config.schema.json`:

```json
{
  "$schema": "https://nebula-panel.dev/schemas/config-schema-v1.json",
  "schemaVersion": "1.0",
  "fileName": "server.cfg",
  "fileType": "Properties",
  "fields": [
    {
      "key": "hostname",
      "displayName": "Server Name",
      "description": "The name of your server displayed in the server browser.",
      "type": "String",
      "defaultValue": "My Server",
      "category": "General"
    },
    {
      "key": "maxplayers",
      "displayName": "Max Players",
      "description": "Maximum number of players allowed on the server.",
      "type": "Int",
      "defaultValue": 24,
      "validation": {
        "minValue": 1,
        "maxValue": 64
      },
      "category": "General"
    },
    {
      "key": "password",
      "displayName": "Server Password",
      "description": "Password required to join the server (leave empty for no password).",
      "type": "String",
      "defaultValue": "",
      "category": "Security"
    }
  ]
}
```

### Field Types

| Type | Description | Example |
|------|-------------|---------|
| `String` | Text input | Server name, password |
| `Int` | Integer number | Max players, port |
| `Float` | Decimal number | Spawn rate multipliers |
| `Bool` | True/False toggle | PvP enabled |
| `Select` | Dropdown selection | Difficulty level |
| `Port` | Network port (1-65535) | Server port, RCON port |

### Version Sources

| Type | Description |
|------|-------------|
| `SteamCmd` | Returns "latest" version; used with SteamCMD installation |
| `HttpJson` | Fetches versions from a JSON API endpoint |
| `Static` | Static list of version strings |

### Installation Types

| Type | Description |
|------|-------------|
| `SteamCmd` | Install via SteamCMD (most common for dedicated servers) |
| `HttpDownload` | Download and extract from URL |
| `Manual` | User must install manually; system only manages configs |

---

## Option 2: Code-Based Provider

### Step 1: Create the Provider Folder

```
OfficialGames/
└── YourGame/
    ├── YourGameProvider.cs      # Main provider class
    ├── YourGameVersionFetcher.cs   # Version fetching logic
    ├── YourGameInstaller.cs     # Installation logic
    ├── Models/
    │   └── YourGameModels.cs    # API response models
    └── Schemas/
        └── config.schema.json   # Configuration schemas
```

### Step 2: Implement IOfficialGameProvider

```csharp
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.YourGame;

public class YourGameProvider : IOfficialGameProvider
{
    private readonly IConfigurationSchemaLoader _schemaLoader;
    private readonly ILogger<YourGameProvider> _logger;

    public string GameSlug => "your-game";

    public YourGameProvider(
        IConfigurationSchemaLoader schemaLoader,
        ILogger<YourGameProvider> logger)
    {
        _schemaLoader = schemaLoader;
        _logger = logger;
    }

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "Your Game",
        Slug = "your-game",
        SteamAppId = "123456",
        ExecutableType = ExecutableType.Exe,
        DefaultExecutablePath = "server.exe",
        DefaultStartCommand = "server.exe -port {port}",
        DefaultStopCommand = "quit",
        SupportsDocker = true,
        DefaultDockerImage = "yourimage/server",
        IconPath = "/images/games/your-game.png",
        SupportsMods = false,
        RconDefaults = new RconDefaults
        {
            DefaultEnabled = true,
            Protocol = RconProtocolType.Source,
            DefaultPort = 27015
        },
        ConfigurationSchemas = _schemaLoader.LoadAllSchemasAsync("your-game")
            .GetAwaiter().GetResult()
    };

    public async Task<IReadOnlyList<GameVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        // Implement version fetching logic
        // Return list of available versions
    }

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Implement installation logic
    }

    public async Task<ServerInstallationResult> UpdateServerAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Implement update logic (often same as install)
        return await InstallServerAsync(server, targetVersion, progress, cancellationToken);
    }
}
```

### Step 3: Register in DependencyInjection.cs

Add your provider to `Infrastructure/DependencyInjection.cs`:

```csharp
// In AddInfrastructureServices method:
services.AddSingleton<IOfficialGameProvider, YourGameProvider>();
```

---

## Testing Your Game

### 1. Build and Run

```bash
dotnet build
dotnet run --project src/NebulaPanel.Web
```

### 2. Check Discovery Logs

Look for these log messages:
```
Discovering official game providers...
Found X code-based providers
Seeded official game: Your Game (your-game)
```

### 3. Verify in Admin UI

1. Navigate to `/settings/official-games`
2. Verify your game appears in the list
3. Check the provider type (Json or Code)
4. Click "Refresh Versions" to test version fetching

### 4. Test Server Creation

1. Go to Servers > Create New Server
2. Select your game
3. Choose a version
4. Complete the wizard
5. Verify the server installs correctly

---

## Checklist

- [ ] Game definition is complete (all required fields)
- [ ] Configuration schemas validate correctly
- [ ] Version fetching returns valid versions
- [ ] Installation completes successfully
- [ ] Server can start and stop
- [ ] RCON connection works (if enabled)
- [ ] Game appears in admin UI
- [ ] Icon is added to `wwwroot/images/games/`

---

## Getting Help

- Check existing implementations in `OfficialGames/Minecraft/` for examples
- Review the `GameDefinition` and `GameVersionInfo` classes in Domain
- Look at `JsonGameProvider.cs` for JSON-based provider implementation

## Common Issues

### Game not appearing
- Check that `game.json` is marked as an embedded resource
- Verify the JSON is valid (no trailing commas, proper escaping)
- Check logs for discovery errors

### Versions not loading
- Verify the version source URL is accessible
- Check the JSON path configuration for HttpJson sources
- Look for exceptions in logs during version refresh

### Installation fails
- Verify SteamCMD is available and configured
- Check the Steam App ID is correct
- Ensure the install path has write permissions
