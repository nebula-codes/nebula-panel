---
title: Nebula Panel - Testing
tags: [nebula-panel, testing]
---

# Testing

Tests live in `tests/`:
- `NebulaPanel.Domain.Tests`
- `NebulaPanel.Application.Tests`
- `NebulaPanel.Infrastructure.Tests`
- `NebulaPanel.Integration.Tests`

## Key Fixtures
- `tests/NebulaPanel.Infrastructure.Tests/Fixtures/` (sqlite in-memory, etc.)

## Tips
- Prefer unit tests for domain and application logic.
- Use integration tests for controller behaviors and EF Core flows.
