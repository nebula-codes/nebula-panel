---
title: Nebula Panel - Architecture
tags: [nebula-panel, architecture]
---

# Architecture

Nebula Panel follows a layered architecture with a clear separation between domain, application, infrastructure, and web UI.

## Layer Overview

### Domain (`src/NebulaPanel.Domain/`)
- Pure domain model and contracts.
- **Entities**: core business objects like games, servers, users, backups, roles.
- **ValueObjects**: configuration and DTO-like structures used across layers (e.g., `GameDefinition`, `JsonGameDefinition`).
- **Interfaces**: abstractions for services and providers (e.g., `IOfficialGameProvider`, `IOfficialGameRegistry`).
- **Enums**: standardized constants for types, status, and behaviors.

### Application (`src/NebulaPanel.Application/`)
- Business logic and orchestration.
- **Services**: use cases (e.g., `GameService`, `OfficialGameService`, `MinecraftInstallService`).
- **DTOs**: request/response shapes for UI and API boundary.
- **Common**: Result/Error helpers, validation utilities.

### Infrastructure (`src/NebulaPanel.Infrastructure/`)
- External integrations and system-side logic.
- **Persistence**: EF Core, migrations, seeders.
- **OfficialGames**: official game providers, discovery, JSON handling, schema loader.
- **Executors**: process and Docker server execution.
- **ModProviders/ModpackProviders**: Modrinth, CurseForge, Steam Workshop, etc.
- **Health**: readiness and system checks.
- **FileManagement**: server file operations and relocation.

### Web (`src/NebulaPanel.Web/`)
- Blazor UI and host runtime.
- **Components/Pages**: screens and forms.
- **Components/Shared**: reusable UI components (data grids, selectors, modals).
- **Hubs**: SignalR for realtime updates.
- **Program.cs**: DI, middleware pipeline, hosted startup tasks.

> [!info] How data flows
> UI -> Application service -> Infrastructure implementations -> Persistence or external systems.

## Key Architecture Files
- `src/NebulaPanel.Web/Program.cs` - runtime composition root
- `src/NebulaPanel.Infrastructure/DependencyInjection.cs` - service registration
- `src/NebulaPanel.Infrastructure/Persistence/OfficialGameSeeder.cs` - official game seeding
- `src/NebulaPanel.Infrastructure/OfficialGames/OfficialGameDiscoveryService.cs` - provider discovery
