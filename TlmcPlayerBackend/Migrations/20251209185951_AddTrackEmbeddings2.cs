using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackEmbeddings2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackEmbedding_Tracks_TrackId",
                table: "TrackEmbedding");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackEmbedding",
                table: "TrackEmbedding");

            migrationBuilder.RenameTable(
                name: "TrackEmbedding",
                newName: "TrackEmbeddings");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackEmbeddings",
                table: "TrackEmbeddings",
                column: "TrackId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackEmbeddings_Tracks_TrackId",
                table: "TrackEmbeddings",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TrackEmbeddings_Tracks_TrackId",
                table: "TrackEmbeddings");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TrackEmbeddings",
                table: "TrackEmbeddings");

            migrationBuilder.RenameTable(
                name: "TrackEmbeddings",
                newName: "TrackEmbedding");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TrackEmbedding",
                table: "TrackEmbedding",
                column: "TrackId");

            migrationBuilder.AddForeignKey(
                name: "FK_TrackEmbedding_Tracks_TrackId",
                table: "TrackEmbedding",
                column: "TrackId",
                principalTable: "Tracks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
