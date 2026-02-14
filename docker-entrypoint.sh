#!/bin/bash
set -e

# If Docker socket is mounted, add nebula user to a group matching its GID
if [ -S /var/run/docker.sock ]; then
    DOCKER_GID=$(stat -c '%g' /var/run/docker.sock)
    # Create a group with the socket's GID (or reuse if one exists)
    if ! getent group "$DOCKER_GID" > /dev/null 2>&1; then
        groupadd -g "$DOCKER_GID" dockerhost
    fi
    DOCKER_GROUP=$(getent group "$DOCKER_GID" | cut -d: -f1)
    usermod -aG "$DOCKER_GROUP" nebula
fi

# Ensure nebula owns the server files directory so it can edit configs
# (game server containers may create files with a different UID)
chown -R nebula:nebula /app/servers 2>/dev/null || true

# Drop privileges and exec the application
exec gosu nebula "$@"
