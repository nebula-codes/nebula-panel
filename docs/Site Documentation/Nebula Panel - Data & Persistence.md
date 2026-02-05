---
title: Nebula Panel - Data & Persistence
tags: [nebula-panel, database, ef-core]
---

# Data & Persistence

Nebula Panel uses EF Core with SQLite by default and PostgreSQL as an option.

## Key Files
- DbContext and configuration live under `src/NebulaPanel.Infrastructure/Persistence/`.
- Migrations live under `src/NebulaPanel.Infrastructure/Persistence/Migrations/`.

## Startup behavior
`Program.cs` applies migrations and seeds data on startup (except in Testing env):
- `context.Database.MigrateAsync()`
- `DataSeeder.SeedDataAsync()`
- `OfficialGameSeeder.SeedOfficialGamesAsync()`

## Official Game Seeding
- `OfficialGameSeeder` creates or updates official games.
- It preserves user-managed fields (e.g., `IsEnabled`, `IconPath`) and avoids overwriting custom games.

## SQLite Optimizations
- `context.ApplySqliteOptimizationsAsync()` is called on startup for SQLite.

## Notes for Contributors
- If you add or modify entities, add migrations in Infrastructure.
- If you change official game definitions, be aware that seeding will update existing official games on startup.
