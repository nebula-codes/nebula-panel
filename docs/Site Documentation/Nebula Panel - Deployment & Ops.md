---
title: Nebula Panel - Deployment & Ops
tags: [nebula-panel, deployment]
---

# Deployment & Ops

Deployment resources live under `docs/`:
- `docs/DOCKER.md`
- `docs/DEPLOYMENT.md` (if present in your version)

## Docker
- `docker-compose.yml` and `docker-compose.prod.yml` are provided.
- `Dockerfile` builds the app container.

## Logging
- Serilog writes to console and `data/logs/`.

## Health Checks
- `/health`, `/health/ready`, `/health/live`

## Backups
- Configured under `Backup` in `appsettings.json`.
