using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHytaleUserPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hytale_user_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    auto_refresh_token = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    notify_at24_hours = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    notify_at1_hour = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    email_on_expiry = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hytale_user_preferences", x => x.id);
                    table.ForeignKey(
                        name: "fk_hytale_user_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hytale_user_preferences_user_id",
                table: "hytale_user_preferences",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hytale_user_preferences");
        }
    }
}
