---
title: Nebula Panel - Server Execution & Files
tags: [nebula-panel, server-execution, files]
---

# Server Execution & Files

Nebula Panel can run servers as Docker containers or native processes.

## Executors
Located in `src/NebulaPanel.Infrastructure/Executors/`:
- `DockerServerExecutor`
- `NativeProcessExecutor`
- `ServerExecutorFactory`

These implement the `IServerExecutor` interface from Domain and are injected via factory.

## File Management
Located in `src/NebulaPanel.Infrastructure/FileManagement/`:
- `ServerFileManager` - file operations and edit flows
- `BackupFileManager` - backup and restore
- `ServerRelocationService` - moving server data

## Config Editing
- Config parsing uses `IConfigFileParser` implementations.
- Config schemas map to UI forms through `ConfigurationService`.

## RCON
- RCON defaults come from `GameDefinition.RconDefaults`.
- Implementation is in `src/NebulaPanel.Infrastructure/Rcon/`.
