---
title: Workflow - Add Official Game (JSON)
tags: [nebula-panel, official-games, workflow]
---

# Workflow - Add Official Game (JSON)

## 1. Create Definition
- `src/NebulaPanel.Infrastructure/OfficialGames/YourGame/game.json`
- Follow `JsonGameDefinition` schema.

## 2. Add Schemas (optional)
- `src/NebulaPanel.Infrastructure/OfficialGames/YourGame/Schemas/`.
- Map in `configSchemaFiles`.

## 3. Make Discoverable
Choose one:
- **Embedded resource** (recommended for official): update `NebulaPanel.Infrastructure.csproj`.
- **Filesystem**: place under `OfficialGames:UserGamesPath` at runtime.

## 4. Add Icon
- `src/NebulaPanel.Web/wwwroot/images/games/your-game.png`
- Update `iconPath` in JSON.

## 5. Validate
- Run app, check logs for discovery.
- Verify in `Settings -> Official Games`.
