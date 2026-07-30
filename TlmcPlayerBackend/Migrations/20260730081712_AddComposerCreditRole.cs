using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TlmcPlayerBackend.Migrations
{
    /// <inheritdoc />
    public partial class AddComposerCreditRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:credit_role", "arranger,composer,lyricist,performer,staff,vocalist")
                .Annotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .Annotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .Annotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .Annotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:credit_role", "arranger,lyricist,performer,staff,vocalist")
                .OldAnnotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .OldAnnotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .OldAnnotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .OldAnnotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:credit_role", "arranger,lyricist,performer,staff,vocalist")
                .Annotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .Annotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .Annotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .Annotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .Annotation("Npgsql:PostgresExtension:vector", ",,")
                .OldAnnotation("Npgsql:Enum:credit_role", "arranger,composer,lyricist,performer,staff,vocalist")
                .OldAnnotation("Npgsql:Enum:play_source", "album,playlist,search,shuffle,similar,unknown")
                .OldAnnotation("Npgsql:Enum:playlist_kind", "favorite,normal")
                .OldAnnotation("Npgsql:Enum:playlist_visibility", "private,public,unlisted")
                .OldAnnotation("Npgsql:Enum:storage_root", "generated,library,thumbnail")
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
