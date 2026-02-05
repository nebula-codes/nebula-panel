# Nebula Panel

Nebula Panel is a self-hosted game server management platform. It provides a unified web UI for provisioning, configuring, and monitoring game servers with support for both Docker-based and native process execution.

## Key Capabilities

- Manage multiple game servers from a single panel
- Docker and native process execution modes
- Real-time updates via SignalR (logs, status, metrics)
- Role-based access control for teams and communities
- SQLite by default, with configurable database providers
- Extensible support for official games, mods, and modpacks

## Technology Overview

- Backend: .NET 10, ASP.NET Core, SignalR
- UI: Blazor
- Data: EF Core with SQLite by default
- Operations: Docker support for server isolation and management

## Quick Start (Docker)

Using Docker Compose (development):

```bash
docker compose up -d
```

Using Docker Run:

```bash
docker run -d \
  --name nebula-panel \
  -p 5000:5000 \
  -v nebula-data:/app/data \
  -v nebula-servers:/app/servers \
  -v nebula-logs:/app/logs \
  -v /var/run/docker.sock:/var/run/docker.sock \
  -e Jwt__Secret=your-secret-key-minimum-64-characters-long \
  ghcr.io/nebula-codes/nebula-panel:latest
```

Access the panel at `http://localhost:5000`.

For production deployment (Traefik, HTTPS, resource limits), see `docs/DOCKER.md`.

## Configuration

Local development defaults are in `src/NebulaPanel.Web/appsettings.json`. The Docker image reads configuration from environment variables and mounts.

Common environment variables:

| Variable | Description | Required |
|----------|-------------|----------|
| `Jwt__Secret` | JWT signing key (min 64 characters) | Yes |
| `ConnectionStrings__DefaultConnection` | Database connection string | No |
| `CurseForge__ApiKey` | CurseForge API key for mod downloads | No |
| `Modrinth__ApiKey` | Modrinth API key for mod downloads | No |

Data and volume mounts (Docker):

| Path | Purpose | Required |
|------|---------|----------|
| `/app/data` | Database and configuration | Yes |
| `/app/servers` | Game server files and world data | Yes |
| `/app/logs` | Application logs | Recommended |
| `/var/run/docker.sock` | Docker socket (container management) | If using Docker servers |

## Local Development

Prerequisites:

- .NET 10 SDK
- Node.js (Tailwind CSS build)
- Docker (optional)

Steps:

```bash
dotnet restore

cd src/NebulaPanel.Web
npm install
cd ../..

dotnet run --project src/NebulaPanel.Web
```

By default, the SQLite database is located at `data/nebula.db`.

## Documentation

- `docs/DOCKER.md` - Docker deployment guide
- `docs/SPECIFICATION.md` - Architecture and technical specification
- `docs/README.md` - Documentation index

## Contributing

See `CONTRIBUTING.md` for setup, code style, and workflow guidelines.

## License and Trademark

Nebula Panel is released under the Nebula Panel Source Available License (NP-SAL) v1.2. Commercial resale, hosting, and SaaS use are not permitted. The project automatically becomes Apache 2.0 licensed on January 1, 2030.

See `LICENSE`, `TRADEMARK.md`, and `FAQ.md` for details.
