using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthSecurityImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "failed_login_attempts",
                table: "users",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_failed_login_at",
                table: "users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "lockout_end_time",
                table: "users",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "security_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    username = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    event_type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    ip_address = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    details = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_security_audit_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_security_audit_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_event_type",
                table: "security_audit_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_event_type_occurred_at",
                table: "security_audit_events",
                columns: new[] { "event_type", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_ip_address",
                table: "security_audit_events",
                column: "ip_address");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_occurred_at",
                table: "security_audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_user_id",
                table: "security_audit_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_audit_events_user_id_occurred_at",
                table: "security_audit_events",
                columns: new[] { "user_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_audit_events");

            migrationBuilder.DropColumn(
                name: "failed_login_attempts",
                table: "users");

            migrationBuilder.DropColumn(
                name: "last_failed_login_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "lockout_end_time",
                table: "users");
        }
    }
}
