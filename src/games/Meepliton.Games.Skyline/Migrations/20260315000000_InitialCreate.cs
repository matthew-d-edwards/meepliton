using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Meepliton.Games.Skyline.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "skyline_game_results",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoomId = table.Column<string>(type: "text", nullable: false),
                FinalScores = table.Column<string>(type: "jsonb", nullable: false),
                WinnerId = table.Column<string>(type: "text", nullable: true),
                CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_skyline_game_results", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "skyline_player_stats",
            columns: table => new
            {
                Id = table.Column<string>(type: "text", nullable: false),
                UserId = table.Column<string>(type: "text", nullable: false),
                GamesPlayed = table.Column<int>(type: "integer", nullable: false),
                GamesWon = table.Column<int>(type: "integer", nullable: false),
                TotalScore = table.Column<long>(type: "bigint", nullable: false),
                LastPlayedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_skyline_player_stats", x => x.Id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "skyline_game_results");
        migrationBuilder.DropTable(name: "skyline_player_stats");
    }
}
