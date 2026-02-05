using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "announcements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "TEXT", nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    is_pinned = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    display_order = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_announcements", x => x.id);
                    table.ForeignKey(
                        name: "fk_announcements_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_usage_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    cpu_percent = table.Column<double>(type: "REAL", nullable: false),
                    memory_bytes = table.Column<long>(type: "INTEGER", nullable: false),
                    player_count = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_usage_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_usage_history_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "server_activities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    activity_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    metadata = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_server_activities", x => x.id);
                    table.ForeignKey(
                        name: "fk_server_activities_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_server_activities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_announcements_created_by_user_id",
                table: "announcements",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_display_order",
                table: "announcements",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "ix_announcements_is_active_expires_at",
                table: "announcements",
                columns: new[] { "is_active", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_usage_history_server_id",
                table: "resource_usage_history",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_usage_history_server_id_timestamp",
                table: "resource_usage_history",
                columns: new[] { "server_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_usage_history_timestamp",
                table: "resource_usage_history",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_server_activities_server_id",
                table: "server_activities",
                column: "server_id");

            migrationBuilder.CreateIndex(
                name: "ix_server_activities_server_id_timestamp",
                table: "server_activities",
                columns: new[] { "server_id", "timestamp" });

            migrationBuilder.CreateIndex(
                name: "ix_server_activities_timestamp",
                table: "server_activities",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_server_activities_user_id",
                table: "server_activities",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "announcements");

            migrationBuilder.DropTable(
                name: "resource_usage_history");

            migrationBuilder.DropTable(
                name: "server_activities");
        }
    }
}
