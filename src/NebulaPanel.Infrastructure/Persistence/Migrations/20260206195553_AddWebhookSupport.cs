using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "api_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    key_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    key_prefix = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    scopes = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_used_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_api_keys", x => x.id);
                    table.ForeignKey(
                        name: "fk_api_keys_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_endpoints",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    owner_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    secret = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    is_enabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    subscribed_events = table.Column<string>(type: "TEXT", nullable: false),
                    failure_count = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_delivery_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "fk_webhook_endpoints_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    endpoint_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    payload = table.Column<string>(type: "TEXT", nullable: false),
                    http_status_code = table.Column<int>(type: "INTEGER", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    attempted_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    duration_ms = table.Column<int>(type: "INTEGER", nullable: false),
                    attempt_number = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_webhook_endpoints_endpoint_id",
                        column: x => x.endpoint_id,
                        principalTable: "webhook_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_key_hash",
                table: "api_keys",
                column: "key_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_api_keys_user_id",
                table: "api_keys",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_attempted_at",
                table: "webhook_deliveries",
                column: "attempted_at");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_endpoint_id",
                table: "webhook_deliveries",
                column: "endpoint_id");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_endpoints_is_enabled",
                table: "webhook_endpoints",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_endpoints_owner_id",
                table: "webhook_endpoints",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_keys");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "webhook_endpoints");
        }
    }
}
