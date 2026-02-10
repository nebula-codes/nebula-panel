using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MultiNodePhase0 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "node_id",
                table: "game_servers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "cluster_locks",
                columns: table => new
                {
                    lock_name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    holder_node_id = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    acquired_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cluster_locks", x => x.lock_name);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    friendly_name = table.Column<string>(type: "TEXT", nullable: true),
                    xml = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    address = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    agent_token = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    last_heartbeat = table.Column<DateTime>(type: "TEXT", nullable: false),
                    capacity = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nodes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_servers_node_id",
                table: "game_servers",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_nodes_name",
                table: "nodes",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_nodes_status",
                table: "nodes",
                column: "status");

            migrationBuilder.AddForeignKey(
                name: "fk_game_servers_nodes_node_id",
                table: "game_servers",
                column: "node_id",
                principalTable: "nodes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_game_servers_nodes_node_id",
                table: "game_servers");

            migrationBuilder.DropTable(
                name: "cluster_locks");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "nodes");

            migrationBuilder.DropIndex(
                name: "ix_game_servers_node_id",
                table: "game_servers");

            migrationBuilder.DropColumn(
                name: "node_id",
                table: "game_servers");
        }
    }
}
