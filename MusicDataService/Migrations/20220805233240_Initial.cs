using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using MusicDataService.Models;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AlbumName = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ReleaseDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleaseConvention = table.Column<string>(type: "text", nullable: true),
                    CatalogNumber = table.Column<string>(type: "text", nullable: true),
                    NumberOfDiscs = table.Column<int>(type: "integer", nullable: false),
                    Website = table.Column<string>(type: "text", nullable: true),
                    AlbumArtist = table.Column<List<string>>(type: "text[]", nullable: false),
                    DataSource = table.Column<List<string>>(type: "text[]", nullable: true),
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
                name: "Tracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Disc = table.Column<int>(type: "integer", nullable: false),
                    Genre = table.Column<List<string>>(type: "text[]", nullable: true),
                    Arrangement = table.Column<List<string>>(type: "text[]", nullable: true),
                    Vocalist = table.Column<List<string>>(type: "text[]", nullable: true),
                    Lyricist = table.Column<List<string>>(type: "text[]", nullable: true),
                    OriginalNonTouhou = table.Column<bool>(type: "boolean", nullable: true),
                    AlbumId = table.Column<Guid>(type: "uuid", nullable: false)
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
                });

            migrationBuilder.CreateTable(
                name: "OriginalTracks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<LocalizedField>(type: "jsonb", nullable: false),
                    ExternalReference = table.Column<string>(type: "text", nullable: true),
                    AlbumId = table.Column<string>(type: "text", nullable: false),
                    TrackId = table.Column<Guid>(type: "uuid", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_OriginalTracks_Tracks_TrackId",
                        column: x => x.TrackId,
                        principalTable: "Tracks",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_AlbumId",
                table: "Albums",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalTracks_AlbumId",
                table: "OriginalTracks",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_OriginalTracks_TrackId",
                table: "OriginalTracks",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_AlbumId",
                table: "Tracks",
                column: "AlbumId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OriginalTracks");

            migrationBuilder.DropTable(
                name: "OriginalAlbums");

            migrationBuilder.DropTable(
                name: "Tracks");

            migrationBuilder.DropTable(
                name: "Albums");
        }
    }
}
