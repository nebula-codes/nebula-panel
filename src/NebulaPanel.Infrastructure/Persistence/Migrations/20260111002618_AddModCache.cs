using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mod_cache_settings_json",
                table: "system_settings",
                type: "TEXT",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.CreateTable(
                name: "cached_mods",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    provider_mod_id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    slug = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    icon_url = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: true),
                    author = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    downloads = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    categories_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    game_versions_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    loaders_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    cached_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    description_html = table.Column<string>(type: "TEXT", nullable: true),
                    versions_json = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cached_mods", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mod_cache_sync_statuses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", nullable: false),
                    content_type = table.Column<string>(type: "TEXT", nullable: false),
                    state = table.Column<string>(type: "TEXT", nullable: false),
                    current_page = table.Column<int>(type: "INTEGER", nullable: false),
                    total_pages = table.Column<int>(type: "INTEGER", nullable: false),
                    items_synced = table.Column<int>(type: "INTEGER", nullable: false),
                    total_items = table.Column<int>(type: "INTEGER", nullable: false),
                    last_full_sync_started_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_full_sync_completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_incremental_sync_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    last_error = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mod_cache_sync_statuses", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cached_mods_cached_at",
                table: "cached_mods",
                column: "cached_at");

            migrationBuilder.CreateIndex(
                name: "ix_cached_mods_downloads",
                table: "cached_mods",
                column: "downloads");

            migrationBuilder.CreateIndex(
                name: "ix_cached_mods_name",
                table: "cached_mods",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_cached_mods_provider_content_type",
                table: "cached_mods",
                columns: new[] { "provider", "content_type" });

            migrationBuilder.CreateIndex(
                name: "ix_cached_mods_provider_provider_mod_id",
                table: "cached_mods",
                columns: new[] { "provider", "provider_mod_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cached_mods_updated_at",
                table: "cached_mods",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_mod_cache_sync_status_provider_content_type",
                table: "mod_cache_sync_statuses",
                columns: new[] { "provider", "content_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cached_mods");

            migrationBuilder.DropTable(
                name: "mod_cache_sync_statuses");

            migrationBuilder.DropColumn(
                name: "mod_cache_settings_json",
                table: "system_settings");
        }
    }
}
