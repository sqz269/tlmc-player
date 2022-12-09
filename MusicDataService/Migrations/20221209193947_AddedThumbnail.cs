using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class AddedThumbnail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ThumbnailId",
                table: "Albums",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Thumbnails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    LargeAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediumAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SmallAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TinyAssetId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thumbnails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_LargeAssetId",
                        column: x => x.LargeAssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_MediumAssetId",
                        column: x => x.MediumAssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_OriginalAssetId",
                        column: x => x.OriginalAssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_SmallAssetId",
                        column: x => x.SmallAssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Assets_TinyAssetId",
                        column: x => x.TinyAssetId,
                        principalTable: "Assets",
                        principalColumn: "AssetId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_ThumbnailId",
                table: "Albums",
                column: "ThumbnailId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_LargeAssetId",
                table: "Thumbnails",
                column: "LargeAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_MediumAssetId",
                table: "Thumbnails",
                column: "MediumAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_OriginalAssetId",
                table: "Thumbnails",
                column: "OriginalAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_SmallAssetId",
                table: "Thumbnails",
                column: "SmallAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_TinyAssetId",
                table: "Thumbnails",
                column: "TinyAssetId");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Thumbnails_ThumbnailId",
                table: "Albums",
                column: "ThumbnailId",
                principalTable: "Thumbnails",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Thumbnails_ThumbnailId",
                table: "Albums");

            migrationBuilder.DropTable(
                name: "Thumbnails");

            migrationBuilder.DropIndex(
                name: "IX_Albums_ThumbnailId",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "ThumbnailId",
                table: "Albums");
        }
    }
}
