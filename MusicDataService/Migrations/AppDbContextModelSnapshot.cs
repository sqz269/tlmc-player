﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MusicDataService.Data;
using MusicDataService.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MusicDataService.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("MusicDataService.Models.Album", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<List<string>>("AlbumArtist")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<Guid?>("AlbumId")
                        .HasColumnType("uuid");

                    b.Property<Guid?>("AlbumImageAssetId")
                        .HasColumnType("uuid");

                    b.Property<LocalizedField>("AlbumName")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("CatalogNumber")
                        .HasColumnType("text");

                    b.Property<List<string>>("DataSource")
                        .HasColumnType("text[]");

                    b.Property<int?>("NumberOfDiscs")
                        .IsRequired()
                        .HasColumnType("integer");

                    b.Property<string>("ReleaseConvention")
                        .HasColumnType("text");

                    b.Property<DateTime?>("ReleaseDate")
                        .HasColumnType("date");

                    b.Property<string>("Website")
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("AlbumImageAssetId");

                    b.ToTable("Albums");
                });

            modelBuilder.Entity("MusicDataService.Models.Asset", b =>
                {
                    b.Property<Guid>("AssetId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid?>("AlbumId")
                        .HasColumnType("uuid");

                    b.Property<string>("AssetMime")
                        .HasColumnType("text");

                    b.Property<string>("AssetName")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("AssetPath")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<bool>("Large")
                        .HasColumnType("boolean");

                    b.HasKey("AssetId");

                    b.HasIndex("AlbumId");

                    b.ToTable("Assets");
                });

            modelBuilder.Entity("MusicDataService.Models.OriginalAlbum", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("ExternalReference")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<LocalizedField>("FullName")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<LocalizedField>("ShortName")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("OriginalAlbums");
                });

            modelBuilder.Entity("MusicDataService.Models.OriginalTrack", b =>
                {
                    b.Property<string>("Id")
                        .HasColumnType("text");

                    b.Property<string>("AlbumId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ExternalReference")
                        .HasColumnType("text");

                    b.Property<LocalizedField>("Title")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<Guid?>("TrackId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("TrackId");

                    b.ToTable("OriginalTracks");
                });

            modelBuilder.Entity("MusicDataService.Models.Track", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AlbumId")
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Arrangement")
                        .HasColumnType("text[]");

                    b.Property<int?>("Disc")
                        .IsRequired()
                        .HasColumnType("integer");

                    b.Property<List<string>>("Genre")
                        .HasColumnType("text[]");

                    b.Property<int?>("Index")
                        .IsRequired()
                        .HasColumnType("integer");

                    b.Property<List<string>>("Lyricist")
                        .HasColumnType("text[]");

                    b.Property<LocalizedField>("Name")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<bool?>("OriginalNonTouhou")
                        .HasColumnType("boolean");

                    b.Property<List<string>>("Staff")
                        .HasColumnType("text[]");

                    b.Property<Guid?>("TrackFileAssetId")
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Vocalist")
                        .HasColumnType("text[]");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("TrackFileAssetId");

                    b.ToTable("Tracks");
                });

            modelBuilder.Entity("MusicDataService.Models.Album", b =>
                {
                    b.HasOne("MusicDataService.Models.Album", null)
                        .WithMany("LinkedAlbums")
                        .HasForeignKey("AlbumId");

                    b.HasOne("MusicDataService.Models.Asset", "AlbumImage")
                        .WithMany()
                        .HasForeignKey("AlbumImageAssetId");

                    b.Navigation("AlbumImage");
                });

            modelBuilder.Entity("MusicDataService.Models.Asset", b =>
                {
                    b.HasOne("MusicDataService.Models.Album", null)
                        .WithMany("OtherImages")
                        .HasForeignKey("AlbumId");
                });

            modelBuilder.Entity("MusicDataService.Models.OriginalTrack", b =>
                {
                    b.HasOne("MusicDataService.Models.OriginalAlbum", "Album")
                        .WithMany("Tracks")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Track", null)
                        .WithMany("Original")
                        .HasForeignKey("TrackId");

                    b.Navigation("Album");
                });

            modelBuilder.Entity("MusicDataService.Models.Track", b =>
                {
                    b.HasOne("MusicDataService.Models.Album", "Album")
                        .WithMany("Tracks")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Asset", "TrackFile")
                        .WithMany()
                        .HasForeignKey("TrackFileAssetId");

                    b.Navigation("Album");

                    b.Navigation("TrackFile");
                });

            modelBuilder.Entity("MusicDataService.Models.Album", b =>
                {
                    b.Navigation("LinkedAlbums");

                    b.Navigation("OtherImages");

                    b.Navigation("Tracks");
                });

            modelBuilder.Entity("MusicDataService.Models.OriginalAlbum", b =>
                {
                    b.Navigation("Tracks");
                });

            modelBuilder.Entity("MusicDataService.Models.Track", b =>
                {
                    b.Navigation("Original");
                });
#pragma warning restore 612, 618
        }
    }
}
