using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class AddedHlsModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "HlsSegment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    HlsSegmentPath = table.Column<string>(type: "text", nullable: false),
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
                name: "IX_HlsPlaylist_TrackId",
                table: "HlsPlaylist",
                column: "TrackId");

            migrationBuilder.CreateIndex(
                name: "IX_HlsSegment_HlsPlaylistId",
                table: "HlsSegment",
                column: "HlsPlaylistId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HlsSegment");

            migrationBuilder.DropTable(
                name: "HlsPlaylist");
        }
    }
}
