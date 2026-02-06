# Docker Deployment Guide

This guide covers deploying Nebula Panel using Docker.

## Quick Start

### Using Docker Run

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

### Using Docker Compose (Development)

```bash
# Clone the repository
git clone https://github.com/aelrou/nebula-panel.git
cd nebula-panel

# Start the application
docker compose up -d

# View logs
docker compose logs -f
```

Access the panel at http://localhost:5000

## Environment Variables

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production` | No |
| `Database__ConnectionString` | Database connection string | `Data Source=/app/data/nebula.db` | No |
| `Jwt__Secret` | JWT signing key (min 64 chars) | - | **Yes** |
| `CurseForge__ApiKey` | CurseForge API key for mod downloads | - | No |
| `Modrinth__ApiKey` | Modrinth API key for mod downloads | - | No |

### Setting Environment Variables

Create a `.env` file in the project root:

```env
JWT_SECRET=your-very-long-secret-key-at-least-64-characters-for-security
CURSEFORGE_API_KEY=your-curseforge-api-key
MODRINTH_API_KEY=your-modrinth-api-key

# Production only
DOMAIN=panel.yourdomain.com
ACME_EMAIL=admin@yourdomain.com
```

## Volume Mounts

| Path | Purpose | Required |
|------|---------|----------|
| `/app/data` | SQLite database and configuration | **Yes** |
| `/app/servers` | Game server files and world data | **Yes** |
| `/app/logs` | Application logs | Recommended |
| `/var/run/docker.sock` | Docker socket for container management | If using Docker servers |

### Data Persistence

Always mount `/app/data` and `/app/servers` to persist your configuration and game server data:

```bash
# Named volumes (recommended)
-v nebula-data:/app/data
-v nebula-servers:/app/servers

# Or bind mounts for direct access
-v /path/on/host/data:/app/data
-v /path/on/host/servers:/app/servers
```

## Docker-in-Docker (Game Server Containers)

Nebula Panel can manage game servers running in Docker containers. To enable this:

1. Mount the Docker socket:
   ```bash
   -v /var/run/docker.sock:/var/run/docker.sock
   ```

2. The panel runs as a non-root user (`nebula`, UID 10000). Ensure the Docker socket is accessible:
   ```bash
   # Option 1: Add nebula user to docker group (if applicable)
   # Option 2: Adjust socket permissions
   sudo chmod 666 /var/run/docker.sock
   ```

### Security Considerations

Mounting the Docker socket grants container management privileges. Consider:

- Running Nebula Panel on a dedicated Docker host
- Using Docker's authorization plugins for fine-grained control
- Limiting network access to the panel

## Production Deployment

For production, use `docker-compose.prod.yml` which includes:

- **Traefik** reverse proxy with automatic HTTPS (Let's Encrypt)
- Resource limits (memory, CPU)
- Production-optimized settings

### Setup

1. Configure your domain's DNS to point to your server

2. Create `.env` file:
   ```env
   DOMAIN=panel.yourdomain.com
   ACME_EMAIL=admin@yourdomain.com
   JWT_SECRET=your-very-long-secret-key-at-least-64-characters-for-security
   ```

3. Deploy:
   ```bash
   docker compose -f docker-compose.prod.yml up -d
   ```

4. Access at `https://panel.yourdomain.com`

### Resource Limits

The production compose file sets:
- Memory limit: 1GB
- CPU limit: 2 cores
- Memory reservation: 256MB
- CPU reservation: 0.25 cores

Adjust these in `docker-compose.prod.yml` based on your server capacity and expected load.

## Building from Source

```bash
# Build with default version
docker compose build

# Build with specific version
docker compose build --build-arg VERSION=1.2.3
```

## Health Checks

The container includes a health check that queries `/health` every 30 seconds:

```bash
# Check container health
docker inspect --format='{{.State.Health.Status}}' nebula-panel

# View health check logs
docker inspect --format='{{json .State.Health}}' nebula-panel | jq
```

## Troubleshooting

### Container Won't Start

1. Check logs:
   ```bash
   docker logs nebula-panel
   ```

2. Verify volume permissions:
   ```bash
   # Ensure data directories are writable
   docker run --rm -v nebula-data:/data alpine ls -la /data
   ```

### Database Errors

1. Ensure `/app/data` volume is mounted correctly
2. Check disk space on host
3. Verify SQLite file isn't corrupted:
   ```bash
   docker exec nebula-panel sqlite3 /app/data/nebula.db "PRAGMA integrity_check;"
   ```

### Docker Socket Permission Denied

If game server container management fails:

```bash
# Check socket permissions
ls -la /var/run/docker.sock

# Temporary fix (not recommended for production)
sudo chmod 666 /var/run/docker.sock

# Better: Add socket group access
sudo usermod -aG docker $(whoami)
```

### tModLoader File Browser Is Empty

If you are running Terraria (tModLoader) in Docker with the default image (`jacobsmile/tmodloader1.4`) and the file browser shows an empty directory, ensure the server volume is mounted to the correct container path:

- Host `InstallPath` should be mounted to `/terraria-server` in the container.
- If the server was created before this default, update the server's Docker volume mapping or recreate the server so the correct mount is applied.

### Health Check Failing

1. Ensure the application is fully started (check logs)
2. Verify port 5000 is not blocked
3. Test manually:
   ```bash
   docker exec nebula-panel curl -f http://localhost:5000/health
   ```

### High Memory Usage

1. Check for memory leaks in logs
2. Reduce resource limits if needed
3. Monitor with:
   ```bash
   docker stats nebula-panel
   ```

## Upgrading

### Using Docker Compose

```bash
# Pull latest image
docker compose pull

# Recreate container with new image
docker compose up -d
```

### Manual Upgrade

```bash
# Stop and remove old container
docker stop nebula-panel
docker rm nebula-panel

# Pull latest image
docker pull ghcr.io/nebula-codes/nebula-panel:latest

# Start new container with same volumes
docker run -d \
  --name nebula-panel \
  -p 5000:5000 \
  -v nebula-data:/app/data \
  -v nebula-servers:/app/servers \
  -v nebula-logs:/app/logs \
  ghcr.io/nebula-codes/nebula-panel:latest
```

### Backup Before Upgrade

```bash
# Backup data volume
docker run --rm -v nebula-data:/data -v $(pwd):/backup alpine \
  tar czf /backup/nebula-data-backup.tar.gz -C /data .

# Backup servers volume
docker run --rm -v nebula-servers:/data -v $(pwd):/backup alpine \
  tar czf /backup/nebula-servers-backup.tar.gz -C /data .
```
