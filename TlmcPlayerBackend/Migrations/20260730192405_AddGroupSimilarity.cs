using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddGroupSimilarity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "similar_circle",
                columns: table => new
                {
                    anchor_circle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    neighbor_circle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank_style = table.Column<short>(type: "smallint", nullable: true),
                    rank_raw = table.Column<short>(type: "smallint", nullable: true),
                    rank_kde = table.Column<short>(type: "smallint", nullable: true),
                    score_style = table.Column<float>(type: "real", nullable: false),
                    score_raw = table.Column<float>(type: "real", nullable: false),
                    score_kde = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_similar_circle", x => new { x.anchor_circle_id, x.neighbor_circle_id });
                    table.CheckConstraint("similar_circle_no_self", "anchor_circle_id <> neighbor_circle_id");
                    table.CheckConstraint("similar_circle_some_rank", "rank_style IS NOT NULL OR rank_raw IS NOT NULL OR rank_kde IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_similar_circle_circle_anchor_circle_id",
                        column: x => x.anchor_circle_id,
                        principalTable: "circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_similar_circle_circle_neighbor_circle_id",
                        column: x => x.neighbor_circle_id,
                        principalTable: "circle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "similar_release",
                columns: table => new
                {
                    anchor_release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    neighbor_release_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank_style = table.Column<short>(type: "smallint", nullable: true),
                    rank_raw = table.Column<short>(type: "smallint", nullable: true),
                    rank_kde = table.Column<short>(type: "smallint", nullable: true),
                    score_style = table.Column<float>(type: "real", nullable: false),
                    score_raw = table.Column<float>(type: "real", nullable: false),
                    score_kde = table.Column<float>(type: "real", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_similar_release", x => new { x.anchor_release_id, x.neighbor_release_id });
                    table.CheckConstraint("similar_release_no_self", "anchor_release_id <> neighbor_release_id");
                    table.CheckConstraint("similar_release_some_rank", "rank_style IS NOT NULL OR rank_raw IS NOT NULL OR rank_kde IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_similar_release_release_anchor_release_id",
                        column: x => x.anchor_release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_similar_release_release_neighbor_release_id",
                        column: x => x.neighbor_release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_similar_circle_anchor_circle_id_rank_kde",
                table: "similar_circle",
                columns: new[] { "anchor_circle_id", "rank_kde" },
                unique: true,
                filter: "rank_kde IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_similar_circle_anchor_circle_id_rank_raw",
                table: "similar_circle",
                columns: new[] { "anchor_circle_id", "rank_raw" },
                unique: true,
                filter: "rank_raw IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_similar_circle_anchor_circle_id_rank_style",
                table: "similar_circle",
                columns: new[] { "anchor_circle_id", "rank_style" },
                unique: true,
                filter: "rank_style IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_similar_circle_neighbor_circle_id",
                table: "similar_circle",
                column: "neighbor_circle_id");

            migrationBuilder.CreateIndex(
                name: "ix_similar_release_anchor_release_id_rank_kde",
                table: "similar_release",
                columns: new[] { "anchor_release_id", "rank_kde" },
                unique: true,
                filter: "rank_kde IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_similar_release_anchor_release_id_rank_raw",
                table: "similar_release",
                columns: new[] { "anchor_release_id", "rank_raw" },
                unique: true,
                filter: "rank_raw IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_similar_release_anchor_release_id_rank_style",
                table: "similar_release",
                columns: new[] { "anchor_release_id", "rank_style" },
                unique: true,
                filter: "rank_style IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_similar_release_neighbor_release_id",
                table: "similar_release",
                column: "neighbor_release_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "similar_circle");

            migrationBuilder.DropTable(
                name: "similar_release");
        }
    }
}
