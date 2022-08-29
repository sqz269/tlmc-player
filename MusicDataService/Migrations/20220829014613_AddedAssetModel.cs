using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class AddedAssetModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TrackFileAssetId",
                table: "Tracks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AlbumImageAssetId",
                table: "Albums",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    AssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetName = table.Column<string>(type: "text", nullable: false),
                    AssetPath = table.Column<string>(type: "text", nullable: false),
                    AssetMime = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_TrackFileAssetId",
                table: "Tracks",
                column: "TrackFileAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Albums_AlbumImageAssetId",
                table: "Albums",
                column: "AlbumImageAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_AlbumId",
                table: "Assets",
                column: "AlbumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Assets_AlbumImageAssetId",
                table: "Albums",
                column: "AlbumImageAssetId",
                principalTable: "Assets",
                principalColumn: "AssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Assets_TrackFileAssetId",
                table: "Tracks",
                column: "TrackFileAssetId",
                principalTable: "Assets",
                principalColumn: "AssetId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Assets_AlbumImageAssetId",
                table: "Albums");

            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Assets_TrackFileAssetId",
                table: "Tracks");

            migrationBuilder.DropTable(
                name: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_TrackFileAssetId",
                table: "Tracks");

            migrationBuilder.DropIndex(
                name: "IX_Albums_AlbumImageAssetId",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "TrackFileAssetId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "AlbumImageAssetId",
                table: "Albums");
        }
    }
}
