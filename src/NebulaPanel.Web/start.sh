#!/bin/bash
# Nebula Panel - Process Wrapper Script
# Handles updates and automatic restarts
#
# Exit codes:
#   0   = Clean shutdown (no restart)
#   100 = Update requested (run updater, then restart)
#   *   = Crash (restart after delay)

set -e

# Get the directory where this script is located
APP_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MAIN_APP="$APP_DIR/NebulaPanel.Web"
UPDATER="$APP_DIR/NebulaPanel.Updater"
LOG_DIR="$APP_DIR/data/logs"

# Ensure log directory exists
mkdir -p "$LOG_DIR"

# Logging function
log() {
    local timestamp
    timestamp=$(date '+%Y-%m-%d %H:%M:%S')
    echo "[$timestamp] $1" | tee -a "$LOG_DIR/wrapper.log"
}

# Rotate wrapper log if it gets too large (>10MB)
rotate_log() {
    local log_file="$LOG_DIR/wrapper.log"
    local max_size=$((10 * 1024 * 1024))

    if [ -f "$log_file" ]; then
        local size
        size=$(stat -f%z "$log_file" 2>/dev/null || stat -c%s "$log_file" 2>/dev/null || echo 0)
        if [ "$size" -gt "$max_size" ]; then
            mv "$log_file" "$log_file.old"
            log "Rotated wrapper log"
        fi
    fi
}

log "=========================================="
log "Nebula Panel Wrapper Starting"
log "App Directory: $APP_DIR"
log "=========================================="

# Main loop
while true; do
    rotate_log

    log "Launching main application..."

    # Run the main application
    set +e  # Don't exit on error
    if [ -x "$MAIN_APP" ]; then
        "$MAIN_APP"
    elif [ -f "$APP_DIR/NebulaPanel.Web.dll" ]; then
        dotnet "$APP_DIR/NebulaPanel.Web.dll"
    else
        log "ERROR: Could not find NebulaPanel.Web executable or DLL"
        exit 1
    fi

    EXIT_CODE=$?
    set -e

    log "Application exited with code $EXIT_CODE"

    if [ $EXIT_CODE -eq 100 ]; then
        # Exit code 100 = update requested
        log "Update requested, running updater..."

        set +e
        if [ -x "$UPDATER" ]; then
            "$UPDATER" "$APP_DIR"
        elif [ -f "$APP_DIR/NebulaPanel.Updater.dll" ]; then
            dotnet "$APP_DIR/NebulaPanel.Updater.dll" "$APP_DIR"
        else
            log "ERROR: Could not find NebulaPanel.Updater executable or DLL"
            log "Skipping update, restarting application..."
        fi

        UPDATER_EXIT=$?
        set -e

        if [ $UPDATER_EXIT -ne 0 ]; then
            log "Updater failed with code $UPDATER_EXIT, restarting in 5s..."
            sleep 5
        else
            log "Update completed successfully"
        fi

        # Loop continues, starting updated app

    elif [ $EXIT_CODE -eq 0 ]; then
        # Clean exit requested
        log "Application exited normally (clean shutdown)"
        log "Wrapper exiting"
        exit 0

    else
        # Crash - restart after delay
        log "Application crashed (code $EXIT_CODE), restarting in 5s..."
        sleep 5
    fi
done
