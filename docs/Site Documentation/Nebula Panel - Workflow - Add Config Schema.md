---
title: Workflow - Add Config Schema
tags: [nebula-panel, config, workflow]
---

# Workflow - Add Config Schema

Schemas define how config files are rendered and validated in the UI.

## 1. Create Schema File
Add a `.schema.json` file under:
- `src/NebulaPanel.Infrastructure/OfficialGames/<Game>/Schemas/`

## 2. Embed Schema
Update `src/NebulaPanel.Infrastructure/NebulaPanel.Infrastructure.csproj`:
- Add an `<EmbeddedResource Include="OfficialGames/<Game>/Schemas/<file>.schema.json" />`

## 3. Map Schema to Config File
- If code-based provider: include in `GameDefinition.ConfigurationSchemas`.
- If JSON-based: map `configSchemaFiles` in `game.json`.

## 4. Optional Overrides
To override without rebuild, place the schema in the filesystem override path:
- `OfficialGames:SchemasPath` -> `.../schemas/<gameSlug>/<file>.schema.json`

## 5. Validate
Schema validation errors are logged by `ConfigurationSchemaLoader` during official game discovery.
