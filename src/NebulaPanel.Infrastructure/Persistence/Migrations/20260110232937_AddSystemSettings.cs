using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    general_settings_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    update_settings_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    appearance_settings_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_modified_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    last_modified_by_user_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_settings", x => x.id);
                    table.ForeignKey(
                        name: "fk_system_settings_users_last_modified_by_user_id",
                        column: x => x.last_modified_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_system_settings_last_modified_by_user_id",
                table: "system_settings",
                column: "last_modified_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_settings");
        }
    }
}
