using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HashRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "token",
                table: "refresh_tokens",
                newName: "token_hash");

            migrationBuilder.RenameColumn(
                name: "replaced_by_token",
                table: "refresh_tokens",
                newName: "replaced_by_token_hash");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_tokens_token",
                table: "refresh_tokens",
                newName: "ix_refresh_tokens_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "token_hash",
                table: "refresh_tokens",
                newName: "token");

            migrationBuilder.RenameColumn(
                name: "replaced_by_token_hash",
                table: "refresh_tokens",
                newName: "replaced_by_token");

            migrationBuilder.RenameIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                newName: "ix_refresh_tokens_token");
        }
    }
}
