using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfficialGameManagementProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cached_version_count",
                table: "games",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_enabled",
                table: "games",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_version_check",
                table: "games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "schema_version",
                table: "games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cached_version_count",
                table: "games");

            migrationBuilder.DropColumn(
                name: "is_enabled",
                table: "games");

            migrationBuilder.DropColumn(
                name: "last_version_check",
                table: "games");

            migrationBuilder.DropColumn(
                name: "schema_version",
                table: "games");
        }
    }
}
