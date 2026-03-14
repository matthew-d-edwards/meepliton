using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Meepliton.Api.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Identity tables ──────────────────────────────────────────────────

        migrationBuilder.CreateTable(
            name: "roles",
            columns: table => new
            {
                id                = table.Column<string>(type: "text", nullable: false),
                name              = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_name   = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                concurrency_stamp = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_roles", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id                     = table.Column<string>(type: "text", nullable: false),
                display_name           = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                avatar_url             = table.Column<string>(type: "text", nullable: true),
                theme                  = table.Column<string>(type: "text", nullable: false, defaultValue: "system"),
                created_at             = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                last_seen_at           = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                user_name              = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_user_name   = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email                  = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                normalized_email       = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                email_confirmed        = table.Column<bool>(type: "boolean", nullable: false),
                password_hash          = table.Column<string>(type: "text", nullable: true),
                security_stamp         = table.Column<string>(type: "text", nullable: true),
                concurrency_stamp      = table.Column<string>(type: "text", nullable: true),
                phone_number           = table.Column<string>(type: "text", nullable: true),
                phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                two_factor_enabled     = table.Column<bool>(type: "boolean", nullable: false),
                lockout_end            = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                lockout_enabled        = table.Column<bool>(type: "boolean", nullable: false),
                access_failed_count    = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "role_claims",
            columns: table => new
            {
                id          = table.Column<int>(type: "integer", nullable: false)
                                   .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                role_id     = table.Column<string>(type: "text", nullable: false),
                claim_type  = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_role_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_role_claims_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_claims",
            columns: table => new
            {
                id          = table.Column<int>(type: "integer", nullable: false)
                                   .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                user_id     = table.Column<string>(type: "text", nullable: false),
                claim_type  = table.Column<string>(type: "text", nullable: true),
                claim_value = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_claims", x => x.id);
                table.ForeignKey(
                    name: "fk_user_claims_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_logins",
            columns: table => new
            {
                login_provider        = table.Column<string>(type: "text", nullable: false),
                provider_key          = table.Column<string>(type: "text", nullable: false),
                provider_display_name = table.Column<string>(type: "text", nullable: true),
                user_id               = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                table.ForeignKey(
                    name: "fk_user_logins_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            columns: table => new
            {
                user_id = table.Column<string>(type: "text", nullable: false),
                role_id = table.Column<string>(type: "text", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                table.ForeignKey(
                    name: "fk_user_roles_roles_role_id",
                    column: x => x.role_id,
                    principalTable: "roles",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_user_roles_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_tokens",
            columns: table => new
            {
                user_id        = table.Column<string>(type: "text", nullable: false),
                login_provider = table.Column<string>(type: "text", nullable: false),
                name           = table.Column<string>(type: "text", nullable: false),
                value          = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                table.ForeignKey(
                    name: "fk_user_tokens_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        // ── Platform tables ──────────────────────────────────────────────────

        migrationBuilder.CreateTable(
            name: "games",
            columns: table => new
            {
                id              = table.Column<string>(type: "text", nullable: false),
                name            = table.Column<string>(type: "text", nullable: false),
                description     = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                min_players     = table.Column<int>(type: "integer", nullable: false),
                max_players     = table.Column<int>(type: "integer", nullable: false),
                thumbnail_url   = table.Column<string>(type: "text", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_games", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "rooms",
            columns: table => new
            {
                id            = table.Column<string>(type: "text", nullable: false),
                game_id       = table.Column<string>(type: "text", nullable: false),
                host_id       = table.Column<string>(type: "text", nullable: false),
                join_code     = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                status        = table.Column<string>(type: "text", nullable: false, defaultValue: "Waiting"),
                state_version = table.Column<int>(type: "integer", nullable: false),
                game_state    = table.Column<string>(type: "jsonb", nullable: true),
                game_options  = table.Column<string>(type: "jsonb", nullable: true),
                created_at    = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                updated_at    = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                expires_at    = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_rooms", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "room_players",
            columns: table => new
            {
                id         = table.Column<string>(type: "text", nullable: false),
                room_id    = table.Column<string>(type: "text", nullable: false),
                user_id    = table.Column<string>(type: "text", nullable: false),
                seat_index = table.Column<int>(type: "integer", nullable: false),
                joined_at  = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_room_players", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "action_log",
            columns: table => new
            {
                id            = table.Column<long>(type: "bigint", nullable: false)
                                     .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                room_id       = table.Column<string>(type: "text", nullable: false),
                player_id     = table.Column<string>(type: "text", nullable: false),
                action        = table.Column<string>(type: "jsonb", nullable: false),
                state_version = table.Column<int>(type: "integer", nullable: false),
                created_at    = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_action_log", x => x.id);
            });

        // ── Indexes ──────────────────────────────────────────────────────────

        migrationBuilder.CreateIndex(
            name: "ix_roles_normalized_name",
            table: "roles",
            column: "normalized_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_users_normalized_email",
            table: "users",
            column: "normalized_email");

        migrationBuilder.CreateIndex(
            name: "ix_users_normalized_user_name",
            table: "users",
            column: "normalized_user_name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_role_claims_role_id",
            table: "role_claims",
            column: "role_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_claims_user_id",
            table: "user_claims",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_user_logins_user_id",
            table: "user_logins",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_rooms_join_code",
            table: "rooms",
            column: "join_code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_rooms_status",
            table: "rooms",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ix_rooms_expires_at",
            table: "rooms",
            column: "expires_at");

        migrationBuilder.CreateIndex(
            name: "ix_room_players_room_id",
            table: "room_players",
            column: "room_id");

        migrationBuilder.CreateIndex(
            name: "ix_room_players_user_id",
            table: "room_players",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ix_action_log_room_id",
            table: "action_log",
            column: "room_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "action_log");
        migrationBuilder.DropTable(name: "room_players");
        migrationBuilder.DropTable(name: "rooms");
        migrationBuilder.DropTable(name: "games");
        migrationBuilder.DropTable(name: "user_tokens");
        migrationBuilder.DropTable(name: "user_roles");
        migrationBuilder.DropTable(name: "user_logins");
        migrationBuilder.DropTable(name: "user_claims");
        migrationBuilder.DropTable(name: "role_claims");
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "roles");
    }
}
