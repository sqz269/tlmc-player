using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Models.Playlist;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialV6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:credit_role", "arranger,lyricist,performer,staff,vocalist")
                .Annotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .Annotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .Annotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .Annotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "asset",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    root = table.Column<StorageRoot>(type: "storage_root", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    mime = table.Column<string>(type: "text", nullable: true),
                    byte_size = table.Column<long>(type: "bigint", nullable: false),
                    content_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_asset", x => x.id);
                    table.CheckConstraint("asset_storage_key_shape", "storage_key <> '' AND storage_key NOT LIKE '/%' AND storage_key !~ '(^|/)\\.\\.(/|$)'");
                });

            migrationBuilder.CreateTable(
                name: "circle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    established = table.Column<DateOnly>(type: "date", nullable: true),
                    country = table.Column<string>(type: "text", nullable: true),
                    alias = table.Column<List<string>>(type: "text[]", nullable: false),
                    data_source = table.Column<List<string>>(type: "text[]", nullable: false),
                    tlmc_reference = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_circle", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "embedding_config",
                columns: table => new
                {
                    id = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    model = table.Column<string>(type: "text", nullable: false),
                    loaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    track_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_embedding_config", x => x.id);
                    table.CheckConstraint("embedding_config_single_row", "id");
                });

            migrationBuilder.CreateTable(
                name: "lyrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    variants = table.Column<List<LyricsVariant>>(type: "jsonb", nullable: false),
                    reference_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lyrics", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "original_work",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_key = table.Column<string>(type: "text", nullable: false),
                    work_type = table.Column<string>(type: "text", nullable: false),
                    full_name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    short_name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    external_ref = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_original_work", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tag",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    name_sort = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(name)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tag", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_profile",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    date_joined = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_profile", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "artwork",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_asset_id = table.Column<Guid>(type: "uuid", nullable: false),
                    colors = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artwork", x => x.id);
                    table.ForeignKey(
                        name: "fk_artwork_asset_source_asset_id",
                        column: x => x.source_asset_id,
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "circle_website",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    invalid = table.Column<bool>(type: "boolean", nullable: false),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_circle_website", x => x.id);
                    table.ForeignKey(
                        name: "fk_circle_website_circle_circle_id",
                        column: x => x.circle_id,
                        principalTable: "circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "original_song",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_key = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    track_index = table.Column<short>(type: "smallint", nullable: true),
                    external_ref = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_original_song", x => x.id);
                    table.ForeignKey(
                        name: "fk_original_song_original_work_original_work_id",
                        column: x => x.original_work_id,
                        principalTable: "original_work",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<PlaylistKind>(type: "playlist_kind", nullable: false),
                    visibility = table.Column<PlaylistVisibility>(type: "playlist_visibility", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist", x => x.id);
                    table.CheckConstraint("playlist_name_length", "char_length(name) BETWEEN 1 AND 200");
                    table.ForeignKey(
                        name: "fk_playlist_user_profile_owner_id",
                        column: x => x.owner_id,
                        principalTable: "user_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "artwork_variant",
                columns: table => new
                {
                    artwork_id = table.Column<Guid>(type: "uuid", nullable: false),
                    size_px = table.Column<short>(type: "smallint", nullable: false),
                    asset_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_artwork_variant", x => new { x.artwork_id, x.size_px });
                    table.ForeignKey(
                        name: "fk_artwork_variant_artwork_artwork_id",
                        column: x => x.artwork_id,
                        principalTable: "artwork",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_artwork_variant_asset_asset_id",
                        column: x => x.asset_id,
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    name_sort = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(name->>'default')", stored: true),
                    release_date = table.Column<DateOnly>(type: "date", nullable: true),
                    release_convention = table.Column<string>(type: "text", nullable: true),
                    catalog_number = table.Column<string>(type: "text", nullable: true),
                    websites = table.Column<List<string>>(type: "text[]", nullable: false),
                    data_sources = table.Column<List<string>>(type: "text[]", nullable: false),
                    tlmc_root_reference = table.Column<List<string>>(type: "text[]", nullable: false),
                    artwork_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release", x => x.id);
                    table.ForeignKey(
                        name: "fk_release_artworks_artwork_id",
                        column: x => x.artwork_id,
                        principalTable: "artwork",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "disc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    disc_number = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_disc", x => x.id);
                    table.CheckConstraint("disc_number_positive", "disc_number >= 1");
                    table.ForeignKey(
                        name: "fk_disc_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "release_circle",
                columns: table => new
                {
                    release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    circle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release_circle", x => new { x.release_id, x.circle_id });
                    table.ForeignKey(
                        name: "fk_release_circle_circles_circle_id",
                        column: x => x.circle_id,
                        principalTable: "circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_release_circle_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    disc_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_number = table.Column<short>(type: "smallint", nullable: false),
                    name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    name_sort = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(name->>'default')", stored: true),
                    duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    original_non_touhou = table.Column<bool>(type: "boolean", nullable: true),
                    media_key = table.Column<string>(type: "text", nullable: true),
                    hls_bitrates = table.Column<List<short>>(type: "smallint[]", nullable: false),
                    has_dash = table.Column<bool>(type: "boolean", nullable: false),
                    source_asset_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lyrics_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track", x => x.id);
                    table.ForeignKey(
                        name: "fk_track_assets_source_asset_id",
                        column: x => x.source_asset_id,
                        principalTable: "asset",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_track_disc_disc_id",
                        column: x => x.disc_id,
                        principalTable: "disc",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_track_lyrics_lyrics_id",
                        column: x => x.lyrics_id,
                        principalTable: "lyrics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "play_event",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    played_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    source = table.Column<PlaySource>(type: "play_source", nullable: false),
                    ms_played = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_play_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_play_event_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_play_event_user_profile_user_id",
                        column: x => x.user_id,
                        principalTable: "user_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "playlist_item",
                columns: table => new
                {
                    playlist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_playlist_item", x => new { x.playlist_id, x.track_id });
                    table.CheckConstraint("playlist_item_position_positive", "position >= 1");
                    table.ForeignKey(
                        name: "fk_playlist_item_playlist_playlist_id",
                        column: x => x.playlist_id,
                        principalTable: "playlist",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_playlist_item_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queue_item",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enqueued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_item", x => x.id);
                    table.CheckConstraint("queue_item_position_positive", "position >= 1");
                    table.ForeignKey(
                        name: "fk_queue_item_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_queue_item_user_profile_user_id",
                        column: x => x.user_id,
                        principalTable: "user_profile",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "similar_track",
                columns: table => new
                {
                    anchor_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<short>(type: "smallint", nullable: false),
                    neighbor_track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_similar_track", x => new { x.anchor_track_id, x.rank });
                    table.CheckConstraint("similar_track_no_self", "anchor_track_id <> neighbor_track_id");
                    table.CheckConstraint("similar_track_rank_positive", "rank >= 1");
                    table.ForeignKey(
                        name: "fk_similar_track_track_anchor_track_id",
                        column: x => x.anchor_track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_similar_track_track_neighbor_track_id",
                        column: x => x.neighbor_track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_credit",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<CreditRole>(type: "credit_role", nullable: false),
                    ordinal = table.Column<short>(type: "smallint", nullable: false),
                    credit_name = table.Column<string>(type: "text", nullable: false),
                    credit_name_sort = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(credit_name)", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_credit", x => new { x.track_id, x.role, x.ordinal });
                    table.ForeignKey(
                        name: "fk_track_credit_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_embedding",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    embedding_mean = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    embedding_mean_max = table.Column<HalfVector>(type: "halfvec(2048)", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_embedding", x => x.track_id);
                    table.ForeignKey(
                        name: "fk_track_embedding_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_original_song",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_song_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_original_song", x => new { x.track_id, x.original_song_id });
                    table.ForeignKey(
                        name: "fk_track_original_song_original_song_original_song_id",
                        column: x => x.original_song_id,
                        principalTable: "original_song",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_track_original_song_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "track_tag",
                columns: table => new
                {
                    track_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_track_tag", x => new { x.track_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_track_tag_tag_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_track_tag_track_track_id",
                        column: x => x.track_id,
                        principalTable: "track",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_artwork_source_asset_id",
                table: "artwork",
                column: "source_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_artwork_variant_asset_id",
                table: "artwork_variant",
                column: "asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_asset_content_hash",
                table: "asset",
                column: "content_hash",
                filter: "content_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_asset_root_storage_key",
                table: "asset",
                columns: new[] { "root", "storage_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_circle_website_circle_id",
                table: "circle_website",
                column: "circle_id");

            migrationBuilder.CreateIndex(
                name: "ix_disc_release_id_disc_number",
                table: "disc",
                columns: new[] { "release_id", "disc_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_original_song_external_key",
                table: "original_song",
                column: "external_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_original_song_original_work_id",
                table: "original_song",
                column: "original_work_id");

            migrationBuilder.CreateIndex(
                name: "ix_original_work_external_key",
                table: "original_work",
                column: "external_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_play_event_track_id",
                table: "play_event",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_play_event_user_id_played_at",
                table: "play_event",
                columns: new[] { "user_id", "played_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_play_event_user_id_track_id",
                table: "play_event",
                columns: new[] { "user_id", "track_id" });

            migrationBuilder.CreateIndex(
                name: "ix_playlist_visibility",
                table: "playlist",
                column: "visibility",
                filter: "visibility = 'public'");

            migrationBuilder.CreateIndex(
                name: "playlist_one_favorite_per_owner",
                table: "playlist",
                column: "owner_id",
                unique: true,
                filter: "kind = 'favorite'");

            migrationBuilder.CreateIndex(
                name: "ix_playlist_item_track_id",
                table: "playlist_item",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_queue_item_track_id",
                table: "queue_item",
                column: "track_id");

            migrationBuilder.CreateIndex(
                name: "ix_queue_item_user_id",
                table: "queue_item",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_release_artwork_id",
                table: "release",
                column: "artwork_id");

            migrationBuilder.CreateIndex(
                name: "ix_release_catalog_number",
                table: "release",
                column: "catalog_number",
                filter: "catalog_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_release_name_sort",
                table: "release",
                column: "name_sort");

            migrationBuilder.CreateIndex(
                name: "ix_release_release_date",
                table: "release",
                column: "release_date");

            migrationBuilder.CreateIndex(
                name: "ix_release_updated_at",
                table: "release",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_release_circle_circle_id",
                table: "release_circle",
                column: "circle_id");

            migrationBuilder.CreateIndex(
                name: "ix_similar_track_neighbor_track_id",
                table: "similar_track",
                column: "neighbor_track_id");

            migrationBuilder.CreateIndex(
                name: "ix_tag_name_sort",
                table: "tag",
                column: "name_sort",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_track_disc_id_track_number",
                table: "track",
                columns: new[] { "disc_id", "track_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_track_lyrics_id",
                table: "track",
                column: "lyrics_id");

            migrationBuilder.CreateIndex(
                name: "ix_track_name_sort",
                table: "track",
                column: "name_sort");

            migrationBuilder.CreateIndex(
                name: "ix_track_source_asset_id",
                table: "track",
                column: "source_asset_id");

            migrationBuilder.CreateIndex(
                name: "ix_track_updated_at",
                table: "track",
                column: "updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_track_credit_credit_name_sort",
                table: "track_credit",
                column: "credit_name_sort");

            migrationBuilder.CreateIndex(
                name: "ix_track_embedding_embedding_mean",
                table: "track_embedding",
                column: "embedding_mean")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_track_embedding_embedding_mean_max",
                table: "track_embedding",
                column: "embedding_mean_max")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "halfvec_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_track_original_song_original_song_id_track_id",
                table: "track_original_song",
                columns: new[] { "original_song_id", "track_id" });

            migrationBuilder.CreateIndex(
                name: "ix_track_tag_tag_id",
                table: "track_tag",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_profile_display_name",
                table: "user_profile",
                column: "display_name",
                unique: true);

            // HAND-ADDED, do not lose on regeneration: EF cannot express DEFERRABLE
            // constraints, and the model snapshot does not know about these
            // (Docs/SCHEMA-V6.md sections 8 and 16). DEFERRABLE so a renumbering pass
            // can shuffle positions inside one transaction without a temporary offset.
            migrationBuilder.Sql(
                "ALTER TABLE playlist_item ADD CONSTRAINT playlist_item_position_unique " +
                "UNIQUE (playlist_id, position) DEFERRABLE INITIALLY DEFERRED;");
            migrationBuilder.Sql(
                "ALTER TABLE queue_item ADD CONSTRAINT queue_item_position_unique " +
                "UNIQUE (user_id, position) DEFERRABLE INITIALLY DEFERRED;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "artwork_variant");

            migrationBuilder.DropTable(
                name: "circle_website");

            migrationBuilder.DropTable(
                name: "embedding_config");

            migrationBuilder.DropTable(
                name: "play_event");

            migrationBuilder.DropTable(
                name: "playlist_item");

            migrationBuilder.DropTable(
                name: "queue_item");

            migrationBuilder.DropTable(
                name: "release_circle");

            migrationBuilder.DropTable(
                name: "similar_track");

            migrationBuilder.DropTable(
                name: "track_credit");

            migrationBuilder.DropTable(
                name: "track_embedding");

            migrationBuilder.DropTable(
                name: "track_original_song");

            migrationBuilder.DropTable(
                name: "track_tag");

            migrationBuilder.DropTable(
                name: "playlist");

            migrationBuilder.DropTable(
                name: "circle");

            migrationBuilder.DropTable(
                name: "original_song");

            migrationBuilder.DropTable(
                name: "tag");

            migrationBuilder.DropTable(
                name: "track");

            migrationBuilder.DropTable(
                name: "user_profile");

            migrationBuilder.DropTable(
                name: "original_work");

            migrationBuilder.DropTable(
                name: "disc");

            migrationBuilder.DropTable(
                name: "lyrics");

            migrationBuilder.DropTable(
                name: "release");

            migrationBuilder.DropTable(
                name: "artwork");

            migrationBuilder.DropTable(
                name: "asset");
        }
    }
}
