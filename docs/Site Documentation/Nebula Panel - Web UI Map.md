---
title: Nebula Panel - Web UI Map
tags: [nebula-panel, web-ui]
---

# Web UI Map

This note highlights where the core UI pages live and how they connect to the backend.

## Key Pages
- Games list/detail/create:
  - `src/NebulaPanel.Web/Components/Pages/Games/`
- Official games admin:
  - `src/NebulaPanel.Web/Components/Pages/Settings/OfficialGames.razor`
  - `src/NebulaPanel.Web/Components/Pages/Settings/OfficialGameDetail.razor`
- Server creation flow:
  - `src/NebulaPanel.Web/Components/Pages/Servers/Create.razor`
  - Minecraft and Hytale wizards: `src/NebulaPanel.Web/Components/Pages/Servers/Minecraft/`, `src/NebulaPanel.Web/Components/Pages/Servers/Hytale/`

## Shared Components
- Data grids and inputs: `src/NebulaPanel.Web/Components/Shared/`
- Modals and panels: `src/NebulaPanel.Web/Components/Shared/`
- Configuration editor: `src/NebulaPanel.Web/Components/Shared/Config/`

## SignalR Hubs
- `src/NebulaPanel.Web/Hubs/InstallProgressHub.cs`
- `src/NebulaPanel.Web/Hubs/UpdateHub.cs`
- `src/NebulaPanel.Web/Hubs/NotificationHub.cs`
- `src/NebulaPanel.Web/Hubs/DashboardHub.cs`

## Theming and Assets
- Tailwind config: `src/NebulaPanel.Web/tailwind.config.js`
- App CSS: `src/NebulaPanel.Web/wwwroot/app.css`
- Game icons: `src/NebulaPanel.Web/wwwroot/images/games/`
