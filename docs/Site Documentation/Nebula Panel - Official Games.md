---
title: Nebula Panel - Official Games
tags: [nebula-panel, official-games]
---

# Official Games

"Official games" are first-class, provider-defined game templates that Nebula Panel manages and keeps in sync with the database. This system supports both **code-based** providers and **JSON-based** definitions.

## Core Concepts

### Provider Interface
- Contract: `src/NebulaPanel.Domain/Interfaces/IOfficialGameProvider.cs`
- Provides:
  - Static `GameDefinition`
  - Available versions
  - Install and update logic

### Registry
- Implementation: `src/NebulaPanel.Infrastructure/OfficialGames/OfficialGameRegistry.cs`
- Tracks providers and metadata and exposes them to services.

### Discovery
- Hosted service: `src/NebulaPanel.Infrastructure/OfficialGames/OfficialGameDiscoveryService.cs`
- Responsibilities:
  - Find code-based providers from DI.
  - Discover JSON `game.json` definitions (embedded resources + filesystem).
  - Validate configuration schemas.
  - Seed official games to the database.

### Seeding
- `src/NebulaPanel.Infrastructure/Persistence/OfficialGameSeeder.cs`
- Creates or updates `Game` entities based on provider definitions.
- Preserves user-configurable fields such as `IsEnabled` and custom icons.

## Definition Types

### Code-Based Providers
- Examples: Minecraft, Hytale, Terraria
- Registered in `src/NebulaPanel.Infrastructure/DependencyInjection.cs`
- Full control over version fetching and install/update logic.

### JSON-Based Providers
- Implemented via `JsonGameProvider`
- Definition schema: `src/NebulaPanel.Domain/ValueObjects/JsonGameDefinition.cs`
- The discovery service can load JSON from:
  - **Embedded resources** in the Infrastructure assembly
  - **Filesystem** path `OfficialGames:UserGamesPath` (defaults to `AppContext.BaseDirectory/games`)

## Admin UI
- List and management: `src/NebulaPanel.Web/Components/Pages/Settings/OfficialGames.razor`
- Details view: `src/NebulaPanel.Web/Components/Pages/Settings/OfficialGameDetail.razor`

## Related Concepts
- Config schemas are handled via `ConfigurationSchemaLoader` and are mapped into `GameDefinition.ConfigurationSchemas`.
- Mod providers are synced via `IOfficialGameService.SyncAllModProvidersAsync` at startup.

> [!tip] Best starting point
> The template guide at `src/NebulaPanel.Infrastructure/OfficialGames/_Template/README.md` is the authoritative workflow for adding new official games.
