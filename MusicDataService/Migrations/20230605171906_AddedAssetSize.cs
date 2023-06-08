using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class AddedAssetSize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Large",
                table: "Assets");

            migrationBuilder.AddColumn<long>(
                name: "Size",
                table: "Assets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Size",
                table: "Assets");

            migrationBuilder.AddColumn<bool>(
                name: "Large",
                table: "Assets",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
