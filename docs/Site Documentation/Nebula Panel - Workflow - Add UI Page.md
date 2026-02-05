---
title: Workflow - Add UI Page
tags: [nebula-panel, ui, workflow]
---

# Workflow - Add UI Page

## 1. Create Razor Component
Create a new page in `src/NebulaPanel.Web/Components/Pages/`.

## 2. Register Route
Add `@page` at the top of the Razor file.

## 3. Wire Services
Inject application services using `@inject`.

## 4. Navigation
Add a link in the appropriate navigation component:
- `src/NebulaPanel.Web/Components/Layout/Sidebar.razor`
- `NavMenu.razor` or other layout components

## 5. Verify
Run the app and navigate to the new route.
