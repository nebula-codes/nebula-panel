---
title: Workflow - Add Modpack Provider
tags: [nebula-panel, modpacks, workflow]
---

# Workflow - Add Modpack Provider

This guide adds a new modpack provider.

## 1. Implement Provider
Create provider in `src/NebulaPanel.Infrastructure/ModpackProviders/` implementing `IModpackProvider`.

## 2. Register in DI
Add to `DependencyInjection.cs`:
- `services.AddSingleton<IModpackProvider, YourProvider>();`

## 3. Registry
Ensure `IModpackProviderRegistry` is aware of the provider (current registry is used by UnifiedModpackService).

## 4. UI Hook
Minecraft modpack UI uses `IUnifiedModpackService`. If your provider implements it, it will appear.

## 5. Verify
Test:
- Browse modpacks
- Install a modpack
- Update and remove
