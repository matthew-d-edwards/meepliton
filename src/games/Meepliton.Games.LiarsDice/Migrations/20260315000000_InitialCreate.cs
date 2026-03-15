using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Meepliton.Games.LiarsDice.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No game-owned tables in v1.
        // All game state is stored in rooms.game_state (JSONB) on the platform side.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Nothing to drop.
    }
}
