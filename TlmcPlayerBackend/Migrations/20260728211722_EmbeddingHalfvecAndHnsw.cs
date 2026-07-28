using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class EmbeddingHalfvecAndHnsw : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<HalfVector>(
                name: "EmbeddingMeanMax",
                table: "TrackEmbeddings",
                type: "halfvec(2048)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(2048)");

            migrationBuilder.CreateIndex(
                name: "IX_TrackEmbeddings_EmbeddingMean",
                table: "TrackEmbeddings",
                column: "EmbeddingMean")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_TrackEmbeddings_EmbeddingMeanMax",
                table: "TrackEmbeddings",
                column: "EmbeddingMeanMax")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "halfvec_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrackEmbeddings_EmbeddingMean",
                table: "TrackEmbeddings");

            migrationBuilder.DropIndex(
                name: "IX_TrackEmbeddings_EmbeddingMeanMax",
                table: "TrackEmbeddings");

            migrationBuilder.AlterColumn<Vector>(
                name: "EmbeddingMeanMax",
                table: "TrackEmbeddings",
                type: "vector(2048)",
                nullable: false,
                oldClrType: typeof(HalfVector),
                oldType: "halfvec(2048)");
        }
    }
}
