﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MusicDataService.Data;
using MusicDataService.Models;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MusicDataService.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20221216233850_AddedThumbColor")]
    partial class AddedThumbColor
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "6.0.7")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("AlbumCircle", b =>
                {
                    b.Property<Guid>("AlbumArtistId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("AlbumsId")
                        .HasColumnType("uuid");

                    b.HasKey("AlbumArtistId", "AlbumsId");

                    b.HasIndex("AlbumsId");

                    b.ToTable("AlbumCircle");
                });

            modelBuilder.Entity("MusicDataService.Models.Album", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

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

                    b.Property<Guid?>("ThumbnailId")
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Website")
                        .HasColumnType("text[]");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("AlbumImageAssetId");

                    b.HasIndex("ThumbnailId");

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

            modelBuilder.Entity("MusicDataService.Models.Circle", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Alias")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Circles");
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

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.ToTable("OriginalTracks");
                });

            modelBuilder.Entity("MusicDataService.Models.Thumbnail", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Colors")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<Guid>("LargeAssetId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("MediumAssetId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("OriginalAssetId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("SmallAssetId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("TinyAssetId")
                        .HasColumnType("uuid");

                    b.HasKey("Id");

                    b.HasIndex("LargeAssetId");

                    b.HasIndex("MediumAssetId");

                    b.HasIndex("OriginalAssetId");

                    b.HasIndex("SmallAssetId");

                    b.HasIndex("TinyAssetId");

                    b.ToTable("Thumbnails");
                });

            modelBuilder.Entity("MusicDataService.Models.Track", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<Guid>("AlbumId")
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Arrangement")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<int>("Disc")
                        .HasColumnType("integer");

                    b.Property<TimeSpan?>("Duration")
                        .HasColumnType("interval");

                    b.Property<List<string>>("Genre")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<int>("Index")
                        .HasColumnType("integer");

                    b.Property<List<string>>("Lyricist")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<LocalizedField>("Name")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<bool?>("OriginalNonTouhou")
                        .HasColumnType("boolean");

                    b.Property<List<string>>("Staff")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.Property<Guid?>("TrackFileAssetId")
                        .HasColumnType("uuid");

                    b.Property<List<string>>("Vocalist")
                        .IsRequired()
                        .HasColumnType("text[]");

                    b.HasKey("Id");

                    b.HasIndex("AlbumId");

                    b.HasIndex("TrackFileAssetId");

                    b.ToTable("Tracks");
                });

            modelBuilder.Entity("OriginalTrackTrack", b =>
                {
                    b.Property<string>("OriginalId")
                        .HasColumnType("text");

                    b.Property<Guid>("TracksId")
                        .HasColumnType("uuid");

                    b.HasKey("OriginalId", "TracksId");

                    b.HasIndex("TracksId");

                    b.ToTable("OriginalTrackTrack");
                });

            modelBuilder.Entity("AlbumCircle", b =>
                {
                    b.HasOne("MusicDataService.Models.Circle", null)
                        .WithMany()
                        .HasForeignKey("AlbumArtistId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Album", null)
                        .WithMany()
                        .HasForeignKey("AlbumsId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MusicDataService.Models.Album", b =>
                {
                    b.HasOne("MusicDataService.Models.Album", null)
                        .WithMany("LinkedAlbums")
                        .HasForeignKey("AlbumId");

                    b.HasOne("MusicDataService.Models.Asset", "AlbumImage")
                        .WithMany()
                        .HasForeignKey("AlbumImageAssetId");

                    b.HasOne("MusicDataService.Models.Thumbnail", "Thumbnail")
                        .WithMany()
                        .HasForeignKey("ThumbnailId");

                    b.Navigation("AlbumImage");

                    b.Navigation("Thumbnail");
                });

            modelBuilder.Entity("MusicDataService.Models.Asset", b =>
                {
                    b.HasOne("MusicDataService.Models.Album", null)
                        .WithMany("OtherFiles")
                        .HasForeignKey("AlbumId");
                });

            modelBuilder.Entity("MusicDataService.Models.OriginalTrack", b =>
                {
                    b.HasOne("MusicDataService.Models.OriginalAlbum", "Album")
                        .WithMany("Tracks")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Album");
                });

            modelBuilder.Entity("MusicDataService.Models.Thumbnail", b =>
                {
                    b.HasOne("MusicDataService.Models.Asset", "Large")
                        .WithMany()
                        .HasForeignKey("LargeAssetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Asset", "Medium")
                        .WithMany()
                        .HasForeignKey("MediumAssetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Asset", "Original")
                        .WithMany()
                        .HasForeignKey("OriginalAssetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Asset", "Small")
                        .WithMany()
                        .HasForeignKey("SmallAssetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Asset", "Tiny")
                        .WithMany()
                        .HasForeignKey("TinyAssetId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Large");

                    b.Navigation("Medium");

                    b.Navigation("Original");

                    b.Navigation("Small");

                    b.Navigation("Tiny");
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

            modelBuilder.Entity("OriginalTrackTrack", b =>
                {
                    b.HasOne("MusicDataService.Models.OriginalTrack", null)
                        .WithMany()
                        .HasForeignKey("OriginalId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("MusicDataService.Models.Track", null)
                        .WithMany()
                        .HasForeignKey("TracksId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("MusicDataService.Models.Album", b =>
                {
                    b.Navigation("LinkedAlbums");

                    b.Navigation("OtherFiles");

                    b.Navigation("Tracks");
                });

            modelBuilder.Entity("MusicDataService.Models.OriginalAlbum", b =>
                {
                    b.Navigation("Tracks");
                });
#pragma warning restore 612, 618
        }
    }
}
