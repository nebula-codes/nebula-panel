---
title: Nebula Panel - Development Setup
tags: [nebula-panel, onboarding]
---

# Development Setup

This note helps a new contributor get the app running and understand the local dev workflow.

## Prerequisites
- .NET SDK 10 (per `global.json`)
- Node.js (for Tailwind/build assets)
- Docker (optional, for containerized servers)

## Solution Layout
- `NebulaPanel.sln` is the root solution.
- `src/NebulaPanel.Web` is the host app.

## Run Locally
- Backend + UI are hosted together by the Web project.

Typical workflow:
1. Restore dependencies.
2. Run `src/NebulaPanel.Web`.

## Where config lives
- `src/NebulaPanel.Web/appsettings.json` is the default config.
- You can override with environment variables or appsettings in your hosting environment.

## Common issues
- If startup fails in production due to JWT secret, see `Program.cs` for validation logic.
- Database defaults to SQLite at `data/nebula.db` unless configured.

> [!tip] First task for a new dev
> Open `src/NebulaPanel.Web/Program.cs` and `src/NebulaPanel.Infrastructure/DependencyInjection.cs` to understand the main runtime wiring.
