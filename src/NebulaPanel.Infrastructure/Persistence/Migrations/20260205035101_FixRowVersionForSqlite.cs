using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixRowVersionForSqlite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "row_version",
                table: "game_servers",
                type: "BLOB",
                nullable: false,
                defaultValueSql: "randomblob(8)",
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldRowVersion: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<byte[]>(
                name: "row_version",
                table: "game_servers",
                type: "BLOB",
                rowVersion: true,
                nullable: false,
                oldClrType: typeof(byte[]),
                oldType: "BLOB",
                oldDefaultValueSql: "randomblob(8)");
        }
    }
}
