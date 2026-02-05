---
title: Nebula Panel - Runtime & Startup
tags: [nebula-panel, runtime]
---

# Runtime & Startup

This note traces the app startup sequence and highlights the key runtime subsystems.

## Startup Composition Root
File: `src/NebulaPanel.Web/Program.cs`

Key phases:
1. **Configuration + Serilog** - logging and app settings.
2. **DI registration** - application and infrastructure services are registered.
3. **Auth + Authorization** - JWT settings and custom policies/handlers.
4. **Database** - EF Core with SQLite or PostgreSQL.
5. **Health checks** - DB, disk, memory, docker, external APIs.
6. **SignalR** - hubs for realtime status and progress.
7. **Hangfire** - background jobs for scheduled tasks.
8. **Migrations + Seeders** - runs on startup when not in testing.
9. **Post-start tasks** - scheduled job sync, post-update actions, mod provider sync.

## Seeders and Official Games
- **OfficialGameSeeder** runs during startup to seed or update official game definitions in the database.
- **OfficialGameDiscoveryService** (hosted service) discovers providers and JSON definitions, validates schemas, then seeds.

> [!warning] Ordering note
> Startup also explicitly calls `OfficialGameSeeder.SeedOfficialGamesAsync` in `Program.cs`. The hosted discovery service also seeds after provider discovery. These are intentional but can be confusing when following logs.

## Key Runtime Services
- `OfficialGameDiscoveryService` - provider discovery and validation.
- `OfficialGameSeeder` - database sync for official games.
- `UpdateBackgroundService` and `UpdateScheduleBackgroundService` - update workflows.
- SignalR hubs for install progress and notifications.

## Health Endpoints
- `/health` - full status
- `/health/ready` - readiness checks
- `/health/live` - liveness
