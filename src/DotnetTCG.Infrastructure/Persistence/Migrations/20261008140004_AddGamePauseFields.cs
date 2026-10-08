using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DotnetTCG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGamePauseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PausedAt",
                table: "games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PausedByUserId",
                table: "games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PreviousStatus",
                table: "games",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PausedAt",
                table: "games");

            migrationBuilder.DropColumn(
                name: "PausedByUserId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "PreviousStatus",
                table: "games");
        }
    }
}
