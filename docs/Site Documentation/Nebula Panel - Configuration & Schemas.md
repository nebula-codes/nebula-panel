---
title: Nebula Panel - Configuration & Schemas
tags: [nebula-panel, config, schemas]
---

# Configuration & Schemas

Configuration schemas are used to render dynamic configuration forms for games and to validate config structure.

## Schema Loader
- Implementation: `src/NebulaPanel.Infrastructure/OfficialGames/ConfigurationSchemaLoader.cs`
- Loads schemas from:
  1. Filesystem (override)
  2. Embedded resources

### Filesystem Paths
- `OfficialGames:SchemasPath` in app configuration
- Default: `AppContext.BaseDirectory/schemas/{gameSlug}/`

### Embedded Resources
- Example embedded resources are declared in `src/NebulaPanel.Infrastructure/NebulaPanel.Infrastructure.csproj`.
- Resource naming is inferred by `ConfigurationSchemaLoader.BuildResourceName`.

## JSON Game Definitions and Schemas
- `JsonGameDefinition.ConfigSchemaFiles` maps config file names to schema files.
- Example mapping:
  - `"server.properties" -> "server.properties.schema.json"`

## Schema Validation
- The loader validates schemas on discovery and logs warnings for issues like:
  - missing field keys
  - missing display names
  - select fields with no options

## Config File Parsers
Registered in `src/NebulaPanel.Infrastructure/DependencyInjection.cs`:
- Properties, JSON, INI, YAML

> [!tip] Override workflow
> If you need to tweak a schema without rebuilding, place the schema file under the filesystem override path with the same filename. The loader will prefer filesystem over embedded.
