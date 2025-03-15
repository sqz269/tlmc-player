using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using TlmcPlayerBackend.Models.MusicData;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddedLyrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LyricsId",
                table: "Tracks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Lyrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Variants = table.Column<List<LyricsVariant>>(type: "jsonb", nullable: false),
                    ReferenceUrl = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lyrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tracks_LyricsId",
                table: "Tracks",
                column: "LyricsId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tracks_Lyrics_LyricsId",
                table: "Tracks",
                column: "LyricsId",
                principalTable: "Lyrics",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tracks_Lyrics_LyricsId",
                table: "Tracks");

            migrationBuilder.DropTable(
                name: "Lyrics");

            migrationBuilder.DropIndex(
                name: "IX_Tracks_LyricsId",
                table: "Tracks");

            migrationBuilder.DropColumn(
                name: "LyricsId",
                table: "Tracks");
        }
    }
}
