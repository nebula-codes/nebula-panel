---
title: Nebula Panel - Mods & Modpacks
tags: [nebula-panel, mods]
---

# Mods & Modpacks

Nebula Panel integrates with multiple mod providers and modpack providers.

## Mod Providers
Infrastructure location: `src/NebulaPanel.Infrastructure/ModProviders/`
- Modrinth
- CurseForge
- Steam Workshop
- Modtale (Hytale)

Providers implement `IModProvider` (Domain).

## Modpack Providers
Infrastructure location: `src/NebulaPanel.Infrastructure/ModpackProviders/`
- CurseForge
- Modrinth
- FTB

Providers implement `IModpackProvider` and are aggregated by `UnifiedModpackService`.

## UI
- Minecraft modpack flows live in `src/NebulaPanel.Web/Components/Shared/Minecraft/`.
- Terraria mod UI lives in `src/NebulaPanel.Web/Components/Shared/Terraria/`.
- Hytale mod UI lives in `src/NebulaPanel.Web/Components/Shared/Hytale/`.
