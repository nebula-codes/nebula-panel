using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUpdateScheduleAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "update_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    from_version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    to_version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    success = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    error_message = table.Column<string>(type: "TEXT", nullable: true),
                    release_notes = table.Column<string>(type: "TEXT", nullable: true),
                    initiated_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    was_scheduled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    schedule_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    was_rolled_back = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    rolled_back_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_update_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_update_histories_users_initiated_by_user_id",
                        column: x => x.initiated_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "update_schedules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    scheduled_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    create_backup = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    stop_game_servers = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    restart_game_servers = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    is_cancelled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    cancelled_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    release_notes = table.Column<string>(type: "TEXT", nullable: true),
                    is_executed = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    executed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_update_schedules", x => x.id);
                    table.ForeignKey(
                        name: "fk_update_schedules_users_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_update_schedules_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_update_histories_initiated_by_user_id",
                table: "update_histories",
                column: "initiated_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_update_histories_started_at",
                table: "update_histories",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_update_histories_success",
                table: "update_histories",
                column: "success");

            migrationBuilder.CreateIndex(
                name: "ix_update_schedules_cancelled_by_user_id",
                table: "update_schedules",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_update_schedules_created_by_user_id",
                table: "update_schedules",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_update_schedules_is_cancelled_is_executed_scheduled_at",
                table: "update_schedules",
                columns: new[] { "is_cancelled", "is_executed", "scheduled_at" });

            migrationBuilder.CreateIndex(
                name: "ix_update_schedules_scheduled_at",
                table: "update_schedules",
                column: "scheduled_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "update_histories");

            migrationBuilder.DropTable(
                name: "update_schedules");
        }
    }
}
