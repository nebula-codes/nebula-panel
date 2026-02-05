using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    steam_app_id = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    executable_type = table.Column<int>(type: "INTEGER", nullable: false),
                    default_executable_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    default_start_command = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    default_stop_command = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    supports_docker = table.Column<bool>(type: "INTEGER", nullable: false),
                    default_docker_image = table.Column<string>(type: "TEXT", maxLength: 255, nullable: true),
                    icon_path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    supports_mods = table.Column<bool>(type: "INTEGER", nullable: false),
                    mod_providers = table.Column<string>(type: "TEXT", nullable: false),
                    rcon_defaults = table.Column<string>(type: "TEXT", nullable: true),
                    configuration_schemas = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_games", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    is_system_role = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    priority = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    username = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_login_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    permission_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_id });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_id",
                        column: x => x.permission_id,
                        principalTable: "permissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_servers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    game_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    deployment_type = table.Column<int>(type: "INTEGER", nullable: false),
                    install_path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    docker_container_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    docker_config = table.Column<string>(type: "TEXT", nullable: true),
                    native_config = table.Column<string>(type: "TEXT", nullable: true),
                    rcon_config = table.Column<string>(type: "TEXT", nullable: true),
                    primary_port = table.Column<int>(type: "INTEGER", nullable: false),
                    additional_ports = table.Column<string>(type: "TEXT", nullable: false),
                    bind_address = table.Column<string>(type: "TEXT", maxLength: 45, nullable: false, defaultValue: "0.0.0.0"),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    last_started = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_stopped = table.Column<DateTime>(type: "TEXT", nullable: true),
                    process_id = table.Column<int>(type: "INTEGER", nullable: true),
                    resource_limits = table.Column<string>(type: "TEXT", nullable: true),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_servers", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_servers_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_servers_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    assigned_by = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "backups",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    file_path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    file_size_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    type = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    includes_world = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    includes_config = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    includes_mods = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_backups", x => x.id);
                    table.ForeignKey(
                        name: "fk_backups_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    task_type = table.Column<int>(type: "INTEGER", nullable: false),
                    cron_expression = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    next_run_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_run_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    configuration = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduled_tasks", x => x.id);
                    table.ForeignKey(
                        name: "fk_scheduled_tasks_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_mods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    mod_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    version_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    provider = table.Column<int>(type: "INTEGER", nullable: false),
                    installed_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    local_path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    icon_url = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    file_size_bytes = table.Column<long>(type: "INTEGER", nullable: true),
                    file_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_mods", x => x.id);
                    table.ForeignKey(
                        name: "fk_server_mods_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    permission_code = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    is_granted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_permissions", x => x.id);
                    table.ForeignKey(
                        name: "fk_server_permissions_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_server_permissions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_backups_server_id",
                table: "backups",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_servers_deployment_type",
                table: "game_servers",
                column: "deployment_type");

            migrationBuilder.CreateIndex(
                name: "ix_game_servers_game_id",
                table: "game_servers",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_servers_owner_id",
                table: "game_servers",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_servers_owner_id_name",
                table: "game_servers",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_games_slug",
                table: "games",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_permissions_code",
                table: "permissions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_id",
                table: "role_permissions",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_name",
                table: "roles",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_tasks_server_id",
                table: "scheduled_tasks",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_server_mods_server_id",
                table: "server_mods",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_server_mods_server_id_provider_mod_id",
                table: "server_mods",
                columns: new[] { "server_id", "provider", "mod_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_server_permissions_server_id",
                table: "server_permissions",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_server_permissions_user_id_server_id_permission_code",
                table: "server_permissions",
                columns: new[] { "user_id", "server_id", "permission_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "backups");

            migrationBuilder.DropTable(
                name: "role_permissions");

            migrationBuilder.DropTable(
                name: "scheduled_tasks");

            migrationBuilder.DropTable(
                name: "server_mods");

            migrationBuilder.DropTable(
                name: "server_permissions");

            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "game_servers");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
