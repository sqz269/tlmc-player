using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using TlmcPlayerBackend.Models.MusicData;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class InitialMigration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Established = table.Column<DateTime>(type: "date", nullable: true),
                    Country = table.Column<string>(type: "text", nullable: true),
                    Alias = table.Column<List<string>>(type: "text[]", nullable: false),
                    DataSource = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Circles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OriginalAlbums",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ShortName = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ExternalReference = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginalAlbums", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DateJoined = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CircleWebsites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Invalid = table.Column<bool>(type: "boolean", nullable: false),
                    CircleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CircleWebsites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CircleWebsites_Circles_CircleId",
                        column: x => x.CircleId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OriginalTracks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ExternalReference = table.Column<string>(type: "text", nullable: true),
                    AlbumId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginalTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OriginalTracks_OriginalAlbums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "OriginalAlbums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Playlists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    NumberOfTracks = table.Column<int>(type: "integer", nullable: false),
                    LastModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Playlists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Playlists_UserProfiles_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "UserProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AlbumCircle",
                columns: table => new
                {
                    AlbumArtistId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlbumCircle", x => new { x.AlbumArtistId, x.AlbumsId });
                    table.ForeignKey(
                        name: "FK_AlbumCircle_Circles_AlbumArtistId",
                        column: x => x.AlbumArtistId,
                        principalTable: "Circles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReleaseConvention = table.Column<string>(type: "text", nullable: true),
                    CatalogNumber = table.Column<string>(type: "text", nullable: true),
                    NumberOfDiscs = table.Column<int>(type: "integer", nullable: false),
                    DiscNumber = table.Column<int>(type: "integer", nullable: false),
                    DiscName = table.Column<string>(type: "text", nullable: true),
                    Website = table.Column<List<string>>(type: "text[]", nullable: true),
                    DataSource = table.Column<List<string>>(type: "text[]", nullable: true),
                    ParentAlbumId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ThumbnailId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_Albums_ParentAlbumId",
                        column: x => x.ParentAlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    Mime = table.Column<string>(type: "text", nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Assets_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Thumbnails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalId = table.Column<Guid>(type: "uuid", nullable: false),
                    LargeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumId = table.Column<Guid>(type: "uuid", nullable: false),
                    SmallId = table.Column<Guid>(type: "uuid", nullable: false),
                    TinyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Colors = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thumbnails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_LargeId",
                        column: x => x.LargeId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_MediumId",
                        column: x => x.MediumId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_OriginalId",
                        column: x => x.OriginalId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_SmallId",
                        column: x => x.SmallId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_TinyId",
                        column: x => x.TinyId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Disc = table.Column<int>(type: "integer", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Genre = table.Column<List<string>>(type: "text[]", nullable: false),
                    Staff = table.Column<List<string>>(type: "text[]", nullable: false),
                    Arrangement = table.Column<List<string>>(type: "text[]", nullable: false),
                    Vocalist = table.Column<List<string>>(type: "text[]", nullable: false),
                    Lyricist = table.Column<List<string>>(type: "text[]", nullable: false),
                    OriginalNonTouhou = table.Column<bool>(type: "boolean", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false),
                    TrackFileId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tracks_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Tracks_Assets_TrackFileId",
                        column: x => x.TrackFileId,
                        principalTable: "Assets",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HlsPlaylist",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Bitrate = table.Column<int>(type: "integer", nullable: true),
                    HlsPlaylistPath = table.Column<string>(type: "text", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HlsPlaylist", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HlsPlaylist_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OriginalTrackTrack",
                columns: table => new
                {
                    OriginalId = table.Column<string>(type: "text", nullable: false),
                    TracksId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OriginalTrackTrack", x => new { x.OriginalId, x.TracksId });
                    table.ForeignKey(
                        name: "FK_OriginalTrackTrack_OriginalTracks_OriginalId",
                        column: x => x.OriginalId,
                        principalTable: "OriginalTracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OriginalTrackTrack_Tracks_TracksId",
                        column: x => x.TracksId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlaylistItems",
                columns: table => new
                {
                    TrackId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlaylistId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    TimesPlayed = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaylistItems", x => new { x.TrackId, x.PlaylistId });
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Playlists_PlaylistId",
                        column: x => x.PlaylistId,
                        principalTable: "Playlists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlaylistItems_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HlsSegment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    HlsPlaylistId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HlsSegment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HlsSegment_HlsPlaylist_HlsPlaylistId",
                        column: x => x.HlsPlaylistId,
                        principalTable: "HlsPlaylist",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlbumCircle_AlbumsId",
                table: "AlbumCircle",
                column: "AlbumsId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ImageId",
                table: "Albums",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ParentAlbumId",
                table: "Albums",
                column: "ParentAlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ThumbnailId",
                table: "Albums",
                column: "ThumbnailId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AlbumId",
                table: "Assets",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_CircleWebsites_CircleId",
                table: "CircleWebsites",
                column: "CircleId");

            migrationBuilder.CreateIndex(
                name: "IX_HlsPlaylist_TrackId",
                table: "HlsPlaylist",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_HlsSegment_HlsPlaylistId",
                table: "HlsSegment",
                column: "HlsPlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalTrackTrack_TracksId",
                table: "OriginalTrackTrack",
                column: "TracksId");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalTracks_AlbumId",
                table: "OriginalTracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_PlaylistItems_PlaylistId",
                table: "PlaylistItems",
                column: "PlaylistId");

            migrationBuilder.CreateIndex(
                name: "IX_Playlists_OwnerId",
                table: "Playlists",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_LargeId",
                table: "Thumbnails",
                column: "LargeId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_MediumId",
                table: "Thumbnails",
                column: "MediumId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_OriginalId",
                table: "Thumbnails",
                column: "OriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_SmallId",
                table: "Thumbnails",
                column: "SmallId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_TinyId",
                table: "Thumbnails",
                column: "TinyId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_AlbumId",
                table: "Tracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_TrackFileId",
                table: "Tracks",
                column: "TrackFileId");

            migrationBuilder.CreateIndex(
                name: "IX_UserProfiles_DisplayName",
                table: "UserProfiles",
                column: "DisplayName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumCircle_Albums_AlbumsId",
                table: "AlbumCircle",
                column: "AlbumsId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Assets_ImageId",
                table: "Albums",
                column: "ImageId",
                principalTable: "Assets",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Thumbnails_ThumbnailId",
                table: "Albums",
                column: "ThumbnailId",
                principalTable: "Thumbnails",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Albums_AlbumId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AlbumCircle");

            migrationBuilder.DropTable(
                name: "CircleWebsites");

            migrationBuilder.DropTable(
                name: "HlsSegment");

            migrationBuilder.DropTable(
                name: "OriginalTrackTrack");

            migrationBuilder.DropTable(
                name: "PlaylistItems");

            migrationBuilder.DropTable(
                name: "Circles");

            migrationBuilder.DropTable(
                name: "HlsPlaylist");

            migrationBuilder.DropTable(
                name: "OriginalTracks");

            migrationBuilder.DropTable(
                name: "Playlists");

            migrationBuilder.DropTable(
                name: "Tracks");

            migrationBuilder.DropTable(
                name: "OriginalAlbums");

            migrationBuilder.DropTable(
                name: "UserProfiles");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.DropTable(
                name: "Thumbnails");

            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
