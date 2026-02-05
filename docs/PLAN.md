# Nebula Panel - Claude Code Development Sessions

This guide provides prompts for building Nebula Panel incrementally with Claude Code. Each session builds on the previous ones. Complete them in order.

---

## Phase 1: Foundation

### Session 1 — Domain Entities
```
Create the core domain entities in NebulaPanel.Domain based on docs/SPECIFICATION.md:

Entities needed:
- Game (with ModProviderConfiguration, RconDefaults, ConfigurationSchema)
- GameServer (with DockerConfiguration, NativeConfiguration, RconConfiguration, ResourceLimits)
- User
- Role  
- Permission
- ServerMod

Include all enums: ServerStatus, ServerDeploymentType, ExecutableType, ModProviderType, 
RconProtocolType, CommandMethod, ConfigFileType, ConfigFieldType

Also create the base interfaces: IServerExecutor, IModProvider, IRconClient
```

### Session 2 — Database Setup
```
Set up EF Core in NebulaPanel.Infrastructure:

1. Create NebulaPanelDbContext with DbSets for all entities
2. Create entity configurations in Persistence/Configurations/ for proper mapping
3. Set up the database provider factory that supports SQLite (default) and PostgreSQL
4. Create the initial migration
5. Add a DatabaseSeeder for default admin user and roles

Reference the database schema and configuration sections in docs/SPECIFICATION.md.
Use SQLite-compatible types (TEXT for GUIDs, INTEGER for bools, TEXT for JSON).
```

### Session 3 — Authentication & Authorization
```
Implement the user authentication system:

1. Set up ASP.NET Core Identity with our User entity
2. Create AuthService with login, logout, JWT token generation
3. Create the permission system with role-based + per-server permissions
4. Add authorization policies and handlers
5. Create login/register Blazor pages with the nebula theme

Reference the User & Permission System section in docs/SPECIFICATION.md.
```

### Session 4 — Base UI Layout & Theming
```
Set up the Blazor UI foundation:

1. Create the CSS custom properties for dark and light themes (wwwroot/css/themes/)
2. Configure Tailwind with the nebula color tokens
3. Create MainLayout.razor with sidebar navigation and top bar
4. Create ThemeProvider.razor and ThemeSwitcher.razor components
5. Create shared components: Button, Input, Modal, Card, StatusBadge, Icon
6. Style the login page we created

Reference the UI/UX Specification and theming sections in docs/SPECIFICATION.md.
Match the space/nebula aesthetic with purple accents.
```

---

## Phase 2: Core Server Management

### Session 5 — Game Management Feature
```
Implement complete Game management:

1. Create GameService in Application layer
2. Create GameRepository in Infrastructure layer
3. Create DTOs: GameDto, CreateGameDto, UpdateGameDto
4. Create GamesController API endpoints
5. Create Blazor pages: Games/Index.razor (list with DataGrid), Games/Create.razor, Games/Edit.razor
6. Include mod provider configuration in the forms

Use the DataGrid component with search/sort/filter, not basic tables.
```

### Session 6 — Game Server CRUD
```
Implement Game Server management (CRUD only, no execution yet):

1. Create GameServerService and GameServerRepository
2. Create DTOs for server operations
3. Create API endpoints for server CRUD
4. Create Blazor pages: Servers/Index.razor (list all user's servers), Servers/Create.razor
5. Server creation wizard that lets user pick a game, configure ports, choose Docker vs Native

Don't implement start/stop yet - just the data management.
```

### Session 7 — Native Process Executor
```
Implement the native process executor for running game servers without Docker:

1. Create NativeProcessExecutor implementing IServerExecutor in Infrastructure/Executors/
2. Handle different executable types: Exe, Jar (with Java path), Shell scripts
3. Implement Start, Stop, Restart, GetStatus, SendCommand (via stdin)
4. Capture stdout/stderr for console output
5. Track process IDs and handle graceful vs forced shutdown
6. Add process resource monitoring using System.Diagnostics

Reference the Native Process Executor section in docs/SPECIFICATION.md.
Test with a simple executable first.
```

### Session 8 — Docker Executor
```
Implement the Docker executor using Docker.DotNet:

1. Create DockerServerExecutor implementing IServerExecutor
2. Implement container lifecycle: create, start, stop, remove
3. Handle port mappings, volume mounts, environment variables
4. Implement resource limits (memory, CPU)
5. Capture container logs for console output
6. Track container IDs and status

Reference the Docker Executor section in docs/SPECIFICATION.md.
```

### Session 9 — SteamCMD Integration
```
Implement SteamCMD for downloading and updating Steam-based game servers:

1. Create SteamCmdService in Infrastructure layer
2. Handle SteamCMD download/installation if not present
3. Implement InstallOrUpdateGameAsync with progress reporting
4. Parse SteamCMD output for download progress
5. Support anonymous login and branch selection (beta branches)
6. Integrate with GameServerService for server installation flow

Reference the SteamCMD Integration section in docs/SPECIFICATION.md.
```

### Session 10 — Server Control UI
```
Create the server control interface:

1. Create Servers/Detail.razor - main server management page
2. Add server control buttons: Start, Stop, Restart, Kill
3. Show real-time server status with visual indicators
4. Create ServerCard.razor component for the server list
5. Add quick actions dropdown menu (console, files, settings, delete)
6. Implement proper loading states and error handling

Wire up the UI to the executors we created. Both Docker and Native paths must work.
```

---

## Phase 3: Real-Time Features

### Session 11 — Console Streaming (SignalR)
```
Implement real-time console streaming:

1. Create ConsoleHub SignalR hub with IConsoleHubClient interface
2. Implement console output streaming from both Docker and Native executors
3. Create ConsoleViewer.razor component with terminal styling
4. Add command input with history (up/down arrows)
5. Support joining/leaving server console rooms
6. Add auto-scroll toggle and clear console button

Reference the SignalR Hubs section in docs/SPECIFICATION.md.
Style it like a real terminal with timestamps.
```

### Session 12 — RCON Integration
```
Implement RCON support for sending commands to running servers:

1. Create IRconClient interface and RconClientFactory
2. Implement SourceRconClient for Source engine games (Rust, ARK, CS2)
3. Implement MinecraftRconClient for Minecraft servers
4. Implement WebRconClient for Rust WebRCON
5. Update server executor to use RCON when available instead of stdin
6. Add RCON connection testing in server settings UI

Reference the RCON Integration section in docs/SPECIFICATION.md.
Handle connection failures gracefully with reconnection logic.
```

### Session 13 — Resource Monitoring
```
Implement resource monitoring for host and servers:

1. Create HostResourceMonitor using LibreHardwareMonitor
2. Implement CPU, memory, GPU, disk, network metrics collection
3. Create ServerResourceMonitor for per-server metrics (Docker stats / process metrics)
4. Create MetricsHub SignalR hub for real-time metric streaming
5. Create Dashboard.razor with host metrics gauges and charts
6. Add resource usage display to server cards and detail page

Reference the Resource Monitoring section in docs/SPECIFICATION.md.
Update metrics every 2 seconds.
```

---

## Phase 4: File & Configuration Management

### Session 14 — File Manager
```
Implement the web-based file manager:

1. Create IServerFileManager interface and implementation
2. Implement: list directory, read file, write file, upload, download, delete, rename, create directory
3. Create FileExplorer.razor component with tree/grid view
4. Add breadcrumb navigation and path input
5. Implement drag-and-drop file upload
6. Add context menu for file operations
7. Create simple code editor for text files (use Monaco or CodeMirror via JS interop)

Reference the File Manager section in docs/SPECIFICATION.md.
Validate paths to prevent directory traversal attacks.
```

### Session 15 — Configuration System
```
Implement the dynamic configuration system:

1. Create config file parsers: PropertiesFileParser, JsonFileParser, IniFileParser, YamlFileParser
2. Create ConfigParserFactory to select parser by file type
3. Create ConfigurationService to read/write server configs based on game schemas
4. Create ServerConfig.razor page with dynamic form generation
5. Support all field types: String, Int, Bool, Select, Slider, Password, etc.
6. Implement conditional field visibility (showWhen)
7. Group fields into collapsible sections

Reference the Configuration System Deep Dive in docs/SPECIFICATION.md.
Test with Minecraft server.properties first, then a JSON config.
```

---

## Phase 5: Mod Management

### Session 16 — Mod System Foundation
```
Set up the mod management foundation:

1. Create IModProvider interface with full method signatures
2. Create ModSearchQuery, ModSearchResult, ModDetails, ModVersion models
3. Create IUnifiedModService interface
4. Create ServerMod entity if not already done
5. Create ModsController API endpoints
6. Create basic Mods/Index.razor page structure

Reference the Mod Management section in docs/SPECIFICATION.md.
```

### Session 17 — Modrinth Provider
```
Implement the Modrinth mod provider:

1. Create ModrinthProvider implementing IModProvider
2. Implement search with facets (game version, mod loader, categories)
3. Implement GetDetailsAsync and GetVersionsAsync
4. Implement DownloadAsync with progress reporting and checksum verification
5. Handle rate limiting and errors gracefully
6. Map Modrinth API responses to our models

Test with Minecraft mod searches.
```

### Session 18 — CurseForge Provider
```
Implement the CurseForge mod provider:

1. Create CurseForgeProvider implementing IModProvider
2. Handle CurseForge API key configuration
3. Implement search, details, versions, download
4. Map CurseForge game IDs (Minecraft = 432)
5. Handle CurseForge's download URL requirements

Note: CurseForge requires an API key - add configuration for this.
```

### Session 19 — Unified Mod Service & UI
```
Complete the mod system with unified search and UI:

1. Implement UnifiedModService that aggregates searches across providers
2. Handle result deduplication by mod slug
3. Implement mod installation with dependency resolution
4. Implement update checking across all installed mods
5. Create Mods/Browse.razor with unified search UI
6. Create Mods/Installed.razor showing server's installed mods
7. Add provider filter tabs and mod cards with install buttons

Reference the Unified Mod Service section in docs/SPECIFICATION.md.
```

---

## Phase 6: Scheduling & Backups

### Session 20 — Scheduled Tasks
```
Implement the scheduled task system using Hangfire:

1. Configure Hangfire with in-memory storage (or SQLite)
2. Create ScheduledTask entity and repository
3. Create ScheduledTaskService for CRUD and execution
4. Implement task types: Restart, Backup, Update, Command, Start, Stop
5. Create cron expression parser/validator
6. Create Schedule.razor page for managing server schedules
7. Add recurring job registration on startup

Reference the Scheduled Tasks section in docs/SPECIFICATION.md.
```

### Session 21 — Backup System
```
Implement server backups:

1. Create IBackupService interface and implementation
2. Implement full server backup (compress server directory)
3. Implement selective backup (world/saves only)
4. Create backup rotation (keep N most recent)
5. Implement backup restoration
6. Create Backups.razor page listing backups with restore/delete actions
7. Integrate with scheduled tasks for automated backups

Store backups in configurable location with metadata JSON.
```

---

## Phase 7: Administration

### Session 22 — User Management UI
```
Create the user administration interface:

1. Create Users/Index.razor with user list DataGrid
2. Create Users/Create.razor and Users/Edit.razor
3. Implement password reset functionality
4. Show user's owned servers and permissions
5. Add user enable/disable toggle
6. Create user activity log display

Admin only - check permissions properly.
```

### Session 23 — Role & Permission Management
```
Create role and permission management:

1. Create Roles/Index.razor with role list
2. Create Roles/Edit.razor with permission assignment
3. Create permission tree view organized by category
4. Implement per-server permission overrides UI
5. Show effective permissions for a user on a server
6. Prevent editing system roles (Admin, User)

Reference the Permission Codes section in docs/SPECIFICATION.md.
```

### Session 24 — System Settings
```
Create system-wide settings management:

1. Create Settings/General.razor for app name, URL, etc.
2. Create Settings/Database.razor showing DB info and backup option
3. Create Settings/Updates.razor for update channel and auto-check settings
4. Create Settings/Appearance.razor for default theme
5. Store settings in database with SettingsService
6. Add settings import/export functionality
```

---

## Phase 8: Self-Update System

### Session 25 — Update Service
```
Implement the update checking and download service:

1. Create UpdateConfiguration model
2. Create IUpdateService interface
3. Implement version checking against GitHub releases API
4. Implement update download with progress reporting and SHA256 verification
5. Create UpdateHub SignalR hub for real-time status
6. Add periodic background check (configurable interval)

Reference the Self-Update System section in docs/SPECIFICATION.md.
```

### Session 26 — Update Backup & Apply
```
Implement update application:

1. Create UpdateBackup functionality (database, configs, custom paths)
2. Create UpdateManifest model for passing info to updater
3. Implement ApplyUpdateAsync that signals updater and triggers shutdown
4. Create backup listing and restoration
5. Handle the "exit code 100" convention for update requests

Reference the backup and apply sections in docs/SPECIFICATION.md.
```

### Session 27 — Updater Process
```
Create the standalone updater process (NebulaPanel.Updater):

1. Read pending-update.json manifest
2. Wait for main process to exit
3. Extract update archive
4. Replace application files (preserve data directory)
5. Run database migrations
6. Write completion marker
7. Start the main application
8. Implement rollback on failure

Also create the start.sh wrapper script for process management.
Reference the Updater Process section in docs/SPECIFICATION.md.
```

### Session 28 — Update UI
```
Create the update UI components:

1. Create UpdateBanner.razor (persistent notification when update available)
2. Create UpdateModal.razor (full update flow with progress)
3. Create WelcomeBack.razor (post-update screen)
4. Wire up SignalR for real-time progress updates
5. Show changelog and breaking changes warnings
6. Add update check button in Settings

Reference the UI Components section under Self-Update System in docs/SPECIFICATION.md.
```

---

## Phase 9: Polish & Production

### Session 29 — Error Handling & Logging
```
Improve error handling throughout the application:

1. Create Result<T> pattern for service methods
2. Add global exception handler middleware
3. Implement structured logging with Serilog
4. Create error boundary components for Blazor
5. Add user-friendly error messages and toasts
6. Create Logs viewer in admin panel

Log to both console and rolling file.
```

### Session 30 — Dashboard & Statistics
```
Create a comprehensive dashboard:

1. Enhance Dashboard.razor with summary cards (total servers, running, stopped)
2. Add recent activity feed
3. Add server resource usage charts (last 24h)
4. Add quick actions (start all, stop all)
5. Show system health indicators
6. Add announcements/news section (for admins)
```

### Session 31 — API Documentation & Polish
```
Finalize the API and add documentation:

1. Add Swagger/OpenAPI documentation
2. Create API authentication (JWT bearer)
3. Add rate limiting to API endpoints
4. Create API key management for external integrations
5. Document all endpoints with examples
6. Add API versioning (/api/v1/)
```

### Session 32 — Docker & Deployment
```
Prepare for production deployment:

1. Create optimized Dockerfile (multi-stage build)
2. Create docker-compose.yml examples (simple and advanced)
3. Add health check endpoint
4. Create environment variable configuration
5. Add Kubernetes manifests (optional)
6. Write deployment documentation

Test both SQLite and PostgreSQL configurations.
```

### Session 33 — Testing
```
Add comprehensive tests:

1. Unit tests for domain logic
2. Unit tests for services (mock repositories)
3. Integration tests for API endpoints
4. Integration tests for database operations
5. Add test fixtures and factories
6. Set up CI pipeline configuration (GitHub Actions)

Aim for coverage on critical paths: auth, server control, mod installation.
```

### Session 34 — Final Polish
```
Final UI/UX improvements:

1. Add loading skeletons for all data fetching
2. Implement optimistic UI updates where appropriate
3. Add keyboard shortcuts (Ctrl+K for search, etc.)
4. Improve mobile responsiveness
5. Add onboarding flow for first-time setup
6. Performance audit and optimization
7. Accessibility improvements (ARIA labels, focus management)
```

---

## Tips for Each Session

1. **Start each session** by telling Claude Code which session you're on
2. **Reference the spec** when Claude needs details: "Check docs/SPECIFICATION.md for the exact model"
3. **Test incrementally** - run the app and verify before moving on
4. **Commit often** - commit after each working feature
5. **Don't skip sessions** - they build on each other
6. **Ask for clarification** if a session seems too big - break it into parts

## Estimated Timeline

| Phase | Sessions | Estimated Time |
|-------|----------|----------------|
| Foundation | 1-4 | 1 week |
| Core Server Management | 5-10 | 2 weeks |
| Real-Time Features | 11-13 | 1 week |
| File & Config | 14-15 | 1 week |
| Mod Management | 16-19 | 1.5 weeks |
| Scheduling & Backups | 20-21 | 0.5 weeks |
| Administration | 22-24 | 1 week |
| Self-Update | 25-28 | 1 week |
| Polish & Production | 29-34 | 2 weeks |

**Total: ~11-12 weeks** for a complete, production-ready panel.
