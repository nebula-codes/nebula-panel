---
title: Nebula Panel - New Contributor Checklist
tags: [nebula-panel, onboarding, checklist]
---

# New Contributor Checklist

Use this to get productive fast.

## Day 0: Environment
- [ ] Install .NET SDK 10 (per `global.json`)
- [ ] Install Node.js
- [ ] Clone repo and open `NebulaPanel.sln`
- [ ] Review `src/NebulaPanel.Web/appsettings.json`

## Day 1: Orientation
- [ ] Read [[Nebula Panel - Architecture]]
- [ ] Read [[Nebula Panel - Runtime & Startup]]
- [ ] Browse `src/NebulaPanel.Infrastructure/DependencyInjection.cs`
- [ ] Find `OfficialGameDiscoveryService` and `OfficialGameSeeder`

## Day 1: Run
- [ ] Run the app (Web project)
- [ ] Visit `Settings -> Official Games`
- [ ] Create a server and verify install/start

## Day 2: Small Change
Pick one:
- [ ] Add a config schema for an existing game
- [ ] Add a small UI page
- [ ] Add a unit test

## Day 3: Project Extension
Pick one:
- [ ] Add a JSON-based official game
- [ ] Add a mod provider integration
- [ ] Add a modpack provider

> [!tip] Suggested path
> Follow the official game JSON workflow first; it touches most of the system without deep custom code.
