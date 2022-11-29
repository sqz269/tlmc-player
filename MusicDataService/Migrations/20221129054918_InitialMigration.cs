using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using MusicDataService.Models;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class InitialMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Circles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Alias = table.Column<List<string>>(type: "text[]", nullable: false)
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
                    AlbumName = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReleaseConvention = table.Column<string>(type: "text", nullable: true),
                    CatalogNumber = table.Column<string>(type: "text", nullable: true),
                    NumberOfDiscs = table.Column<int>(type: "integer", nullable: false),
                    Website = table.Column<List<string>>(type: "text[]", nullable: true),
                    DataSource = table.Column<List<string>>(type: "text[]", nullable: true),
                    AlbumImageAssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Albums_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "text", nullable: false),
                    AssetPath = table.Column<string>(type: "text", nullable: false),
                    AssetMime = table.Column<string>(type: "text", nullable: true),
                    Large = table.Column<bool>(type: "boolean", nullable: false),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.AssetId);
                    table.ForeignKey(
                        name: "FK_Assets_Albums_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Albums",
                        principalColumn: "Id");
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
                    TrackFileAssetId = table.Column<Guid>(type: "uuid", nullable: true)
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
                        name: "FK_Tracks_Assets_TrackFileAssetId",
                        column: x => x.TrackFileAssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId");
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

            migrationBuilder.CreateIndex(
                name: "IX_AlbumCircle_AlbumsId",
                table: "AlbumCircle",
                column: "AlbumsId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_AlbumId",
                table: "Albums",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_AlbumImageAssetId",
                table: "Albums",
                column: "AlbumImageAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AlbumId",
                table: "Assets",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalTracks_AlbumId",
                table: "OriginalTracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalTrackTrack_TracksId",
                table: "OriginalTrackTrack",
                column: "TracksId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_AlbumId",
                table: "Tracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_TrackFileAssetId",
                table: "Tracks",
                column: "TrackFileAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_AlbumCircle_Albums_AlbumsId",
                table: "AlbumCircle",
                column: "AlbumsId",
                principalTable: "Albums",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Assets_AlbumImageAssetId",
                table: "Albums",
                column: "AlbumImageAssetId",
                principalTable: "Assets",
                principalColumn: "AssetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Albums_AlbumId",
                table: "Assets");

            migrationBuilder.DropTable(
                name: "AlbumCircle");

            migrationBuilder.DropTable(
                name: "OriginalTrackTrack");

            migrationBuilder.DropTable(
                name: "Circles");

            migrationBuilder.DropTable(
                name: "OriginalTracks");

            migrationBuilder.DropTable(
                name: "Tracks");

            migrationBuilder.DropTable(
                name: "OriginalAlbums");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.DropTable(
                name: "Assets");
        }
    }
}
