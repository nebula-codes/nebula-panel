using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    server_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    resource_type = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    comparison = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    threshold = table.Column<double>(type: "REAL", nullable: false),
                    duration_seconds = table.Column<int>(type: "INTEGER", nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    cooldown_minutes = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alert_rules", x => x.id);
                    table.ForeignKey(
                        name: "fk_alert_rules_game_servers_server_id",
                        column: x => x.server_id,
                        principalTable: "game_servers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_alert_rules_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_is_enabled",
                table: "alert_rules",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_owner_id",
                table: "alert_rules",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_alert_rules_server_id",
                table: "alert_rules",
                column: "server_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alert_rules");
        }
    }
}
