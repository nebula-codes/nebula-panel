---
title: Workflow - Add Official Game (Code)
tags: [nebula-panel, official-games, workflow]
---

# Workflow - Add Official Game (Code)

## 1. Create Provider Folder
`src/NebulaPanel.Infrastructure/OfficialGames/YourGame/`

## 2. Implement Provider
Implement `IOfficialGameProvider` and return a `GameDefinition`.

## 3. Implement Version Fetching
Provide a `GetAvailableVersionsAsync` method using game-specific API(s).

## 4. Implement Install/Update
Provide `InstallServerAsync` and `UpdateServerAsync`.

## 5. Register in DI
`DependencyInjection.cs`:
- `services.AddSingleton<IOfficialGameProvider, YourGameProvider>();`

## 6. Add Schemas and Icon
- Schemas in `OfficialGames/YourGame/Schemas/`
- Icons in `wwwroot/images/games/`

## 7. Verify
Check logs, Admin UI, and create a server to test.
