using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackupScopeAndMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "scheduled_task_id",
                table: "backups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "scope",
                table: "backups",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "source_paths",
                table: "backups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_backups_server_id_created_at",
                table: "backups",
                columns: new[] { "server_id", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_backups_server_id_created_at",
                table: "backups");

            migrationBuilder.DropColumn(
                name: "scheduled_task_id",
                table: "backups");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "backups");

            migrationBuilder.DropColumn(
                name: "source_paths",
                table: "backups");
        }
    }
}
