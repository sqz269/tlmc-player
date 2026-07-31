using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackMapCircle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "circle_id",
                table: "track_map",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_track_map_circle_id",
                table: "track_map",
                column: "circle_id");

            migrationBuilder.AddForeignKey(
                name: "fk_track_map_circle_circle_id",
                table: "track_map",
                column: "circle_id",
                principalTable: "circle",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_track_map_circle_circle_id",
                table: "track_map");

            migrationBuilder.DropIndex(
                name: "ix_track_map_circle_id",
                table: "track_map");

            migrationBuilder.DropColumn(
                name: "circle_id",
                table: "track_map");
        }
    }
}
