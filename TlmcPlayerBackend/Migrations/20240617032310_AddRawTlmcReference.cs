using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddRawTlmcReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TlmcReference",
                table: "Circles",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "TlmcRootReference",
                table: "Albums",
                type: "text[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TlmcReference",
                table: "Circles");

            migrationBuilder.DropColumn(
                name: "TlmcRootReference",
                table: "Albums");
        }
    }
}
