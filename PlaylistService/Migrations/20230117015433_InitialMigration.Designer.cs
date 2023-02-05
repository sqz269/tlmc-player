﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using PlaylistService.Data;

#nullable disable

namespace PlaylistService.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20230117015433_InitialMigration")]
    partial class InitialMigration
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.2")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("PlaylistService.Model.Playlist", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("uuid");

                    b.Property<DateTime>("LastModified")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("NumberOfTracks")
                        .HasColumnType("integer");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<Guid>("UserId")
                        .HasColumnType("uuid");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("Visibility")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("Playlists");
                });

            modelBuilder.Entity("PlaylistService.Model.PlaylistItem", b =>
                {
                    b.Property<Guid>("TrackId")
                        .HasColumnType("uuid");

                    b.Property<Guid>("PlaylistId")
                        .HasColumnType("uuid");

                    b.Property<DateTime>("DateAdded")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("Index")
                        .HasColumnType("integer");

                    b.Property<int>("TimesPlayed")
                        .HasColumnType("integer");

                    b.HasKey("TrackId", "PlaylistId");

                    b.HasIndex("PlaylistId");

                    b.ToTable("PlaylistItems");
                });

            modelBuilder.Entity("PlaylistService.Model.PlaylistItem", b =>
                {
                    b.HasOne("PlaylistService.Model.Playlist", "Playlist")
                        .WithMany("Tracks")
                        .HasForeignKey("PlaylistId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Playlist");
                });

            modelBuilder.Entity("PlaylistService.Model.Playlist", b =>
                {
                    b.Navigation("Tracks");
                });
#pragma warning restore 612, 618
        }
    }
}