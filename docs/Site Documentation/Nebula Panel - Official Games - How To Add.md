---
title: Nebula Panel - Official Games - How To Add
tags: [nebula-panel, official-games, how-to]
---

# How To Add a New Official Game

This guide summarizes the required steps and points you to the exact files that control official game behavior.

## Choose a Provider Type

### JSON-Based (Simpler)
Use for SteamCMD or straightforward installs with minimal custom logic.

**Core files:**
- `src/NebulaPanel.Domain/ValueObjects/JsonGameDefinition.cs`
- `src/NebulaPanel.Infrastructure/OfficialGames/JsonGameProvider.cs`
- Template: `src/NebulaPanel.Infrastructure/OfficialGames/_Template/README.md`

**Steps**
1. Create folder: `src/NebulaPanel.Infrastructure/OfficialGames/YourGame/`
2. Add `game.json` matching the JsonGameDefinition schema.
3. Optional: add schemas in `Schemas/` and map via `configSchemaFiles` in `game.json`.
4. Ensure the JSON is discoverable:
   - **Embedded resource**: add the `game.json` to `src/NebulaPanel.Infrastructure/NebulaPanel.Infrastructure.csproj` as an `<EmbeddedResource>` entry.
   - **Filesystem**: place under the configured `OfficialGames:UserGamesPath` (defaults to `AppContext.BaseDirectory/games`).
5. Add an icon to `src/NebulaPanel.Web/wwwroot/images/games/` and reference via `iconPath`.

> [!warning] Embedded JSON note
> The discovery service only finds embedded JSON if it is compiled as an embedded resource. The csproj currently embeds schemas but not `game.json` by default.

### Code-Based (Advanced)
Required for custom version fetching, special installers, or multiple loaders.

**Core files:**
- `src/NebulaPanel.Domain/Interfaces/IOfficialGameProvider.cs`
- `src/NebulaPanel.Domain/ValueObjects/GameDefinition.cs`
- `src/NebulaPanel.Infrastructure/OfficialGames/_Template/README.md`

**Steps**
1. Create folder: `src/NebulaPanel.Infrastructure/OfficialGames/YourGame/`
2. Implement `IOfficialGameProvider` (provider, version fetcher, installer as needed).
3. Register provider in `src/NebulaPanel.Infrastructure/DependencyInjection.cs`:
   - `services.AddSingleton<IOfficialGameProvider, YourGameProvider>();`
4. Add schemas under `Schemas/` and embed via `NebulaPanel.Infrastructure.csproj`.
5. Add icon in `src/NebulaPanel.Web/wwwroot/images/games/`.

## Verify in UI
1. Run the app.
2. Visit `Settings -> Official Games`.
3. Confirm the game shows up and is enabled.
4. Use "Refresh Versions" to test version fetching.
5. Create a server and validate install/start.

## Common Failure Points
- Missing embedded resource entries for schemas or game.json.
- Incorrect slug casing (slug is lowercased during seeding).
- Schema validation errors (logged during discovery).
- SteamCMD not configured for Steam-based installs.
