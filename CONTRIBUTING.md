# Contributing to Nebula Panel

Thank you for your interest in contributing to Nebula Panel! This document provides guidelines and information for contributors.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Node.js (for Tailwind CSS)
- Docker (optional, for container testing)
- SQLite or PostgreSQL

### Setting Up the Development Environment

1. Clone the repository:
   ```bash
   git clone https://github.com/Nebula-Codes/nebula-panel.git
   cd nebula-panel
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Install Node dependencies (for Tailwind):
   ```bash
   cd src/NebulaPanel.Web
   npm install
   cd ../..
   ```

4. Run the application:
   ```bash
   dotnet run --project src/NebulaPanel.Web
   ```

### Project Structure

```
src/
├── NebulaPanel.Domain/          # Entities, enums, interfaces (no dependencies)
├── NebulaPanel.Application/     # Business logic, services, DTOs
├── NebulaPanel.Infrastructure/  # EF Core, external APIs, file system
├── NebulaPanel.Web/             # Blazor UI, SignalR hubs, API controllers
├── NebulaPanel.Updater/         # Standalone update process
└── NebulaPanel.Shared/          # Shared models between client/server
```

## Code Style

### General Guidelines

- Use file-scoped namespaces
- Use primary constructors where appropriate
- Prefer records for DTOs and immutable data
- Use `CancellationToken` on all async methods
- Use `ConfigureAwait(false)` in library code (non-UI)

### Naming Conventions

- Interfaces: `I` prefix (e.g., `IServerExecutor`)
- Async methods: `Async` suffix
- Private fields: `_camelCase`
- Database tables: `snake_case` (EF Core convention)

### Architecture Rules

- **Domain layer** has NO external dependencies
- **Application layer** depends only on Domain
- **Infrastructure** implements interfaces from Domain/Application
- **Web layer** wires everything together via DI

## Making Changes

### Branch Naming

- `feature/description` - New features
- `fix/description` - Bug fixes
- `docs/description` - Documentation changes
- `refactor/description` - Code refactoring

### Commit Messages

Write clear, concise commit messages that explain *why* the change was made:

```
Add server health monitoring endpoint

Adds a new API endpoint for retrieving real-time server health metrics.
This enables the dashboard to display CPU/memory usage without polling.
```

### Pull Requests

1. Create a branch from `main`
2. Make your changes
3. Ensure all tests pass: `dotnet test`
4. Update documentation if needed
5. Submit a PR with a clear description

## Testing

### Running Tests

```bash
dotnet test
```

### Writing Tests

- Place tests in the corresponding `tests/` project
- Use xUnit for test framework
- Name tests clearly: `MethodName_Scenario_ExpectedResult`

## Adding Game Support

Want to add support for a new game server? See the existing implementations in `src/NebulaPanel.Infrastructure/OfficialGames/` for examples.

Each game needs:
1. A folder under `OfficialGames/` with the game name
2. A `game.json` configuration file
3. Schema files for server configuration (if applicable)

## Questions?

- Open a [Discussion](https://github.com/Nebula-Codes/nebula-panel/discussions) for questions
- Check the [Wiki](https://github.com/Nebula-Codes/nebula-panel/wiki) for documentation

## License

By contributing, you agree that your contributions will be licensed under the same license as the project (see LICENSE file).
