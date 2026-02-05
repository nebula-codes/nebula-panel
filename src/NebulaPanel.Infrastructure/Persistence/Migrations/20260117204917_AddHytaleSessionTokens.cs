using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NebulaPanel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHytaleSessionTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "identity_token",
                table: "hytale_user_credentials",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "owner_uuid",
                table: "hytale_user_credentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "profile_uuid",
                table: "hytale_user_credentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "session_expires_at",
                table: "hytale_user_credentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "session_token",
                table: "hytale_user_credentials",
                type: "TEXT",
                maxLength: 4096,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "identity_token",
                table: "hytale_user_credentials");

            migrationBuilder.DropColumn(
                name: "owner_uuid",
                table: "hytale_user_credentials");

            migrationBuilder.DropColumn(
                name: "profile_uuid",
                table: "hytale_user_credentials");

            migrationBuilder.DropColumn(
                name: "session_expires_at",
                table: "hytale_user_credentials");

            migrationBuilder.DropColumn(
                name: "session_token",
                table: "hytale_user_credentials");
        }
    }
}
