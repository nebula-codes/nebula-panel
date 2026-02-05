---
title: Workflow - Add Mod Provider
tags: [nebula-panel, mods, workflow]
---

# Workflow - Add Mod Provider

This guide adds a new mod provider integration.

## 1. Implement Provider
Create a new provider in `src/NebulaPanel.Infrastructure/ModProviders/` that implements `IModProvider`.

## 2. Register in DI
Add the provider in `src/NebulaPanel.Infrastructure/DependencyInjection.cs`:
- `services.AddSingleton<IModProvider, YourProvider>();`

## 3. Add Settings (if needed)
Add configuration to `appsettings.json` and bind in `Program.cs` or Infrastructure.

## 4. UI Integration
Hook into the relevant UI panels (e.g., Minecraft, Hytale, Terraria) if you need custom UI.

## 5. Verify
- Run app.
- Navigate to Mods tab in a server.
- Ensure list and install flows work.
