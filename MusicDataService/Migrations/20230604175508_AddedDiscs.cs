using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class AddedDiscs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Albums_AlbumId",
                table: "Albums");

            migrationBuilder.RenameColumn(
                name: "AlbumId",
                table: "Albums",
                newName: "ParentAlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_Albums_AlbumId",
                table: "Albums",
                newName: "IX_Albums_ParentAlbumId");

            migrationBuilder.AddColumn<string>(
                name: "DiscName",
                table: "Albums",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DiscNumber",
                table: "Albums",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Albums_ParentAlbumId",
                table: "Albums",
                column: "ParentAlbumId",
                principalTable: "Albums",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Albums_Albums_ParentAlbumId",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "DiscName",
                table: "Albums");

            migrationBuilder.DropColumn(
                name: "DiscNumber",
                table: "Albums");

            migrationBuilder.RenameColumn(
                name: "ParentAlbumId",
                table: "Albums",
                newName: "AlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_Albums_ParentAlbumId",
                table: "Albums",
                newName: "IX_Albums_AlbumId");

            migrationBuilder.AddForeignKey(
                name: "FK_Albums_Albums_AlbumId",
                table: "Albums",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "Id");
        }
    }
}
