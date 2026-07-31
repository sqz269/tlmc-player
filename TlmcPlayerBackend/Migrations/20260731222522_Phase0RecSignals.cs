using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class Phase0RecSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:credit_role", "arranger,composer,lyricist,performer,staff,vocalist")
                .Annotation("Npgsql:Enum:play_source", "album,map,playlist,queue,radio,recommended,search,shuffle,similar,unknown")
                .Annotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .Annotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .Annotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:credit_role", "arranger,composer,lyricist,performer,staff,vocalist")
                .OldAnnotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .OldAnnotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .OldAnnotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .OldAnnotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "rec_impression",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    surface = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    context = table.Column<string>(type: "jsonb", nullable: true),
                    served_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rec_impression", x => x.id);
                    table.ForeignKey(
                        name: "fk_rec_impression_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_rec_impression_user_profile_user_id",
                        column: x => x.user_id,
                        principalTable: "user_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rec_impression_track_id",
                table: "rec_impression",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_rec_impression_user_id_served_at",
                table: "rec_impression",
                columns: new[] { "user_id", "served_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_rec_impression_user_id_track_id_served_at",
                table: "rec_impression",
                columns: new[] { "user_id", "track_id", "served_at" });

            // Docs/RECOMMENDER.md section 3c: the one place the conversion
            // thresholds (48h window, 0.8 completed, 0.25 skipped) are defined.
            // Bandit counters and the evaluation funnel read this, not the raw
            // tables.
            migrationBuilder.Sql("""
                CREATE VIEW rec_attribution AS
                SELECT i.id AS impression_id,
                       i.user_id,
                       i.surface,
                       i.track_id,
                       i.served_at,
                       pe.id AS play_event_id,
                       pe.played_at,
                       pe.ms_played,
                       ratio.completion_ratio,
                       coalesce(ratio.completion_ratio >= 0.8, false) AS completed,
                       coalesce(ratio.completion_ratio < 0.25, false) AS skipped
                FROM rec_impression i
                LEFT JOIN LATERAL (
                    SELECT pe.id, pe.played_at, pe.ms_played
                    FROM play_event pe
                    WHERE pe.user_id = i.user_id
                      AND pe.track_id = i.track_id
                      AND pe.played_at >= i.served_at
                      AND pe.played_at < i.served_at + interval '48 hours'
                    ORDER BY pe.played_at
                    LIMIT 1) pe ON true
                LEFT JOIN track t ON t.id = i.track_id
                CROSS JOIN LATERAL (
                    SELECT CASE WHEN pe.ms_played IS NOT NULL
                                     AND t.duration IS NOT NULL
                                     AND extract(epoch FROM t.duration) > 0
                                THEN pe.ms_played / (extract(epoch FROM t.duration) * 1000.0)
                           END AS completion_ratio) ratio;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW IF EXISTS rec_attribution;");

            migrationBuilder.DropTable(
                name: "rec_impression");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:credit_role", "arranger,composer,lyricist,performer,staff,vocalist")
                .Annotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .Annotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .Annotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .Annotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:credit_role", "arranger,composer,lyricist,performer,staff,vocalist")
                .OldAnnotation("Npgsql:Enum:play_source", "album,map,playlist,queue,radio,recommended,search,shuffle,similar,unknown")
                .OldAnnotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .OldAnnotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .OldAnnotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
