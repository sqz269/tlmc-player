using System;
using MusicDataService.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MusicDataService.Migrations
{
    public partial class CircleMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Circles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Established",
                table: "Circles",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Circles",
                type: "text",
                nullable: false,
                defaultValue: CircleStatus.Unset);

            migrationBuilder.CreateTable(
                name: "CircleWebsites",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
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

            migrationBuilder.CreateIndex(
                name: "IX_CircleWebsites_CircleId",
                table: "CircleWebsites",
                column: "CircleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CircleWebsites");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "Established",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Circles");
        }
    }
}
