---
title: Nebula Panel - Services & Background Jobs
tags: [nebula-panel, services, background]
---

# Services & Background Jobs

This note outlines the major application services and background jobs.

## Application Services (Use Cases)
Defined in `src/NebulaPanel.Application/Services/`.
Key ones:
- `GameService` - game list and metadata access.
- `OfficialGameService` - admin management of official games.
- `GameServerService` - server CRUD and orchestration.
- `ConfigurationService` - config file parsing and schema-driven editing.
- `BackupService` - backup orchestration.

## Infrastructure Services
Defined in `src/NebulaPanel.Infrastructure/Services/`.
Notable services:
- `UpdateService`, `UpdateBackgroundService` - update checks and scheduling.
- `ModCacheSyncService` - sync mod cache.
- `Hytale*` services - Hytale-specific auth and data flows.

## Background Jobs
- Hangfire is used for scheduling (in-memory by default).
- Scheduled tasks are synced on startup.

> [!tip] Where to add a new scheduled task
> Extend `IScheduledTaskService` (Application) and the Hangfire job manager in Infrastructure.
