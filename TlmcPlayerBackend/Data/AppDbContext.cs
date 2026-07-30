using Microsoft.EntityFrameworkCore;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Models.MusicData;
using TlmcPlayerBackend.Models.Playlist;
using TlmcPlayerBackend.Models.UserProfile;

namespace TlmcPlayerBackend.Data;

/// <summary>
/// The v6 schema (Docs/SCHEMA-V6.md). Table names are the doc's singular snake_case
/// DDL names, set explicitly so they cannot drift with pluralization conventions.
/// The DEFERRABLE position uniques on playlist_item/queue_item cannot be expressed
/// here — they live as raw SQL in the initial migration and must not be
/// reverse-engineered away by a later `dotnet ef migrations add`.
/// </summary>
public class AppDbContext : DbContext
{
    public DbSet<Release> Releases { get; set; }
    public DbSet<Disc> Discs { get; set; }
    public DbSet<Track> Tracks { get; set; }
    public DbSet<ReleaseCircle> ReleaseCircles { get; set; }
    public DbSet<Circle> Circles { get; set; }
    public DbSet<CircleWebsite> CircleWebsites { get; set; }
    public DbSet<TrackCredit> TrackCredits { get; set; }
    public DbSet<Tag> Tags { get; set; }
    public DbSet<TrackTag> TrackTags { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<Artwork> Artworks { get; set; }
    public DbSet<ArtworkVariant> ArtworkVariants { get; set; }
    public DbSet<OriginalWork> OriginalWorks { get; set; }
    public DbSet<OriginalSong> OriginalSongs { get; set; }
    public DbSet<TrackOriginalSong> TrackOriginalSongs { get; set; }
    public DbSet<Lyrics> Lyrics { get; set; }

    public DbSet<UserProfile> UserProfiles { get; set; }

    public DbSet<Playlist> Playlists { get; set; }
    public DbSet<PlaylistItem> PlaylistItems { get; set; }
    public DbSet<QueueItem> QueueItems { get; set; }
    public DbSet<PlayEvent> PlayEvents { get; set; }

    public DbSet<TrackEmbedding> TrackEmbeddings { get; set; }
    public DbSet<SimilarTrack> SimilarTracks { get; set; }
    public DbSet<SimilarRelease> SimilarReleases { get; set; }
    public DbSet<SimilarCircle> SimilarCircles { get; set; }
    public DbSet<EmbeddingConfig> EmbeddingConfigs { get; set; }

    public AppDbContext(DbContextOptions<AppDbContext> opt) : base(opt)
    {
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        EntityIdConventions.Apply(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        // -- Release / Disc / Track ------------------------------------------------

        var release = modelBuilder.Entity<Release>();
        release.ToTable("release");
        release.Property(r => r.NameSort)
            .HasComputedColumnSql("lower(name->>'default')", stored: true);
        release.Property(r => r.CreatedAt).HasDefaultValueSql("now()");
        release.Property(r => r.UpdatedAt).HasDefaultValueSql("now()");
        release.HasIndex(r => r.NameSort);
        release.HasIndex(r => r.ReleaseDate);
        release.HasIndex(r => r.CatalogNumber).HasFilter("catalog_number IS NOT NULL");
        release.HasIndex(r => r.UpdatedAt);
        release.HasOne(r => r.Artwork)
            .WithMany()
            .HasForeignKey(r => r.ArtworkId)
            .OnDelete(DeleteBehavior.SetNull);

        var disc = modelBuilder.Entity<Disc>();
        disc.ToTable("disc", t =>
            t.HasCheckConstraint("disc_number_positive", "disc_number >= 1"));
        disc.HasIndex(d => new { d.ReleaseId, d.DiscNumber }).IsUnique();
        disc.HasOne(d => d.Release)
            .WithMany(r => r.Discs)
            .HasForeignKey(d => d.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        var track = modelBuilder.Entity<Track>();
        track.ToTable("track");
        track.Property(t => t.NameSort)
            .HasComputedColumnSql("lower(name->>'default')", stored: true);
        track.Property(t => t.CreatedAt).HasDefaultValueSql("now()");
        track.Property(t => t.UpdatedAt).HasDefaultValueSql("now()");
        track.HasIndex(t => new { t.DiscId, t.TrackNumber }).IsUnique();
        track.HasIndex(t => t.NameSort);
        track.HasIndex(t => t.UpdatedAt);
        track.HasOne(t => t.Disc)
            .WithMany(d => d.Tracks)
            .HasForeignKey(t => t.DiscId)
            .OnDelete(DeleteBehavior.Cascade);
        track.HasOne(t => t.SourceAsset)
            .WithMany()
            .HasForeignKey(t => t.SourceAssetId)
            .OnDelete(DeleteBehavior.SetNull);
        track.HasOne(t => t.Lyrics)
            .WithMany()
            .HasForeignKey(t => t.LyricsId)
            .OnDelete(DeleteBehavior.SetNull);

        var releaseCircle = modelBuilder.Entity<ReleaseCircle>();
        releaseCircle.ToTable("release_circle");
        releaseCircle.HasKey(rc => new { rc.ReleaseId, rc.CircleId });
        releaseCircle.HasIndex(rc => rc.CircleId);
        releaseCircle.HasOne(rc => rc.Release)
            .WithMany(r => r.Circles)
            .HasForeignKey(rc => rc.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
        releaseCircle.HasOne(rc => rc.Circle)
            .WithMany(c => c.Releases)
            .HasForeignKey(rc => rc.CircleId)
            .OnDelete(DeleteBehavior.Cascade);

        // -- Credits and tags ------------------------------------------------------

        var trackCredit = modelBuilder.Entity<TrackCredit>();
        trackCredit.ToTable("track_credit");
        trackCredit.HasKey(tc => new { tc.TrackId, tc.Role, tc.Ordinal });
        trackCredit.Property(tc => tc.CreditNameSort)
            .HasComputedColumnSql("lower(credit_name)", stored: true);
        trackCredit.HasIndex(tc => tc.CreditNameSort);
        trackCredit.HasOne(tc => tc.Track)
            .WithMany(t => t.Credits)
            .HasForeignKey(tc => tc.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        var tag = modelBuilder.Entity<Tag>();
        tag.ToTable("tag");
        tag.Property(t => t.NameSort)
            .HasComputedColumnSql("lower(name)", stored: true);
        tag.HasIndex(t => t.NameSort).IsUnique();

        var trackTag = modelBuilder.Entity<TrackTag>();
        trackTag.ToTable("track_tag");
        trackTag.HasKey(tt => new { tt.TrackId, tt.TagId });
        trackTag.HasIndex(tt => tt.TagId);
        trackTag.HasOne(tt => tt.Track)
            .WithMany(t => t.Tags)
            .HasForeignKey(tt => tt.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
        trackTag.HasOne(tt => tt.Tag)
            .WithMany(t => t.Tracks)
            .HasForeignKey(tt => tt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // -- Assets and artwork ----------------------------------------------------

        var asset = modelBuilder.Entity<Asset>();
        // '..' is rejected as a path segment, not a substring: TLMC filenames
        // legitimately contain consecutive dots.
        asset.ToTable("asset", t =>
            t.HasCheckConstraint("asset_storage_key_shape",
                @"storage_key <> '' AND storage_key NOT LIKE '/%' AND storage_key !~ '(^|/)\.\.(/|$)'"));
        asset.HasIndex(a => new { a.Root, a.StorageKey }).IsUnique();
        asset.HasIndex(a => a.ContentHash).HasFilter("content_hash IS NOT NULL");
        asset.Property(a => a.CreatedAt).HasDefaultValueSql("now()");

        var artwork = modelBuilder.Entity<Artwork>();
        artwork.ToTable("artwork");
        artwork.HasOne(a => a.SourceAsset)
            .WithMany()
            .HasForeignKey(a => a.SourceAssetId)
            .OnDelete(DeleteBehavior.Cascade);
        artwork.Navigation(a => a.Variants).AutoInclude();

        var artworkVariant = modelBuilder.Entity<ArtworkVariant>();
        artworkVariant.ToTable("artwork_variant");
        artworkVariant.HasKey(v => new { v.ArtworkId, v.SizePx });
        artworkVariant.HasOne(v => v.Artwork)
            .WithMany(a => a.Variants)
            .HasForeignKey(v => v.ArtworkId)
            .OnDelete(DeleteBehavior.Cascade);
        artworkVariant.HasOne(v => v.Asset)
            .WithMany()
            .HasForeignKey(v => v.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        // -- Reference data (original works) --------------------------------------

        var originalWork = modelBuilder.Entity<OriginalWork>();
        originalWork.ToTable("original_work");
        originalWork.HasIndex(w => w.ExternalKey).IsUnique();

        var originalSong = modelBuilder.Entity<OriginalSong>();
        originalSong.ToTable("original_song");
        originalSong.HasIndex(s => s.ExternalKey).IsUnique();
        originalSong.HasOne(s => s.OriginalWork)
            .WithMany(w => w.Songs)
            .HasForeignKey(s => s.OriginalWorkId)
            .OnDelete(DeleteBehavior.Cascade);

        var trackOriginalSong = modelBuilder.Entity<TrackOriginalSong>();
        trackOriginalSong.ToTable("track_original_song");
        trackOriginalSong.HasKey(ts => new { ts.TrackId, ts.OriginalSongId });
        // "Every arrangement of this original" — the reverse browse path.
        trackOriginalSong.HasIndex(ts => new { ts.OriginalSongId, ts.TrackId });
        trackOriginalSong.HasOne(ts => ts.Track)
            .WithMany(t => t.OriginalSongs)
            .HasForeignKey(ts => ts.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
        trackOriginalSong.HasOne(ts => ts.OriginalSong)
            .WithMany(s => s.Arrangements)
            .HasForeignKey(ts => ts.OriginalSongId)
            .OnDelete(DeleteBehavior.Cascade);

        // -- Circles and lyrics (deliberately unchanged from v5) -------------------

        var circle = modelBuilder.Entity<Circle>();
        circle.ToTable("circle");
        circle.Property(c => c.Status)
            .HasConversion(v => v.ToString(),
                v => Enum.Parse<CircleStatus>(v));
        circle.Navigation(c => c.Website).AutoInclude();

        var circleWebsite = modelBuilder.Entity<CircleWebsite>();
        circleWebsite.ToTable("circle_website");
        // The only plain-Guid PK in the model; ids are always client-set. Without
        // this, EF's convention marks the key generated and the graph heuristic
        // turns navigation-added rows into 0-row UPDATEs (concurrency crash).
        circleWebsite.Property(w => w.Id).ValueGeneratedNever();
        circleWebsite.HasOne(w => w.Circle)
            .WithMany(c => c.Website)
            .HasForeignKey(w => w.CircleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Lyrics>().ToTable("lyrics");

        // -- Users, playlists, queue, plays ---------------------------------------

        modelBuilder.Entity<UserProfile>().ToTable("user_profile");

        var playlist = modelBuilder.Entity<Playlist>();
        playlist.ToTable("playlist", t =>
            t.HasCheckConstraint("playlist_name_length", "char_length(name) BETWEEN 1 AND 200"));
        playlist.Property(p => p.CreatedAt).HasDefaultValueSql("now()");
        playlist.Property(p => p.LastModified).HasDefaultValueSql("now()");
        playlist.HasIndex(p => p.OwnerId);
        // One Favorite per user; the constraint whose absence lets the
        // check-then-insert bootstrap race into duplicates.
        playlist.HasIndex(p => p.OwnerId)
            .IsUnique()
            .HasFilter("kind = 'favorite'")
            .HasDatabaseName("playlist_one_favorite_per_owner");
        playlist.HasIndex(p => p.Visibility)
            .HasFilter("visibility = 'public'");
        playlist.HasOne(p => p.Owner)
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        var playlistItem = modelBuilder.Entity<PlaylistItem>();
        playlistItem.ToTable("playlist_item", t =>
            t.HasCheckConstraint("playlist_item_position_positive", "position >= 1"));
        playlistItem.HasKey(i => new { i.PlaylistId, i.TrackId });
        playlistItem.HasIndex(i => i.TrackId);
        playlistItem.Property(i => i.AddedAt).HasDefaultValueSql("now()");
        playlistItem.HasOne(i => i.Playlist)
            .WithMany(p => p.Items)
            .HasForeignKey(i => i.PlaylistId)
            .OnDelete(DeleteBehavior.Cascade);
        playlistItem.HasOne(i => i.Track)
            .WithMany()
            .HasForeignKey(i => i.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        var queueItem = modelBuilder.Entity<QueueItem>();
        queueItem.ToTable("queue_item", t =>
            t.HasCheckConstraint("queue_item_position_positive", "position >= 1"));
        queueItem.Property(q => q.Id).UseIdentityAlwaysColumn();
        queueItem.HasIndex(q => q.UserId);
        queueItem.Property(q => q.EnqueuedAt).HasDefaultValueSql("now()");
        queueItem.HasOne(q => q.User)
            .WithMany()
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        queueItem.HasOne(q => q.Track)
            .WithMany()
            .HasForeignKey(q => q.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        var playEvent = modelBuilder.Entity<PlayEvent>();
        playEvent.ToTable("play_event");
        playEvent.Property(e => e.Id).UseIdentityAlwaysColumn();
        playEvent.Property(e => e.PlayedAt).HasDefaultValueSql("now()");
        // The history query.
        playEvent.HasIndex(e => new { e.UserId, e.PlayedAt }).IsDescending(false, true);
        // Play counts, "most played".
        playEvent.HasIndex(e => new { e.UserId, e.TrackId });
        playEvent.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        playEvent.HasOne(e => e.Track)
            .WithMany()
            .HasForeignKey(e => e.TrackId)
            .OnDelete(DeleteBehavior.Cascade);

        // -- Embeddings and similarity --------------------------------------------

        var trackEmbedding = modelBuilder.Entity<TrackEmbedding>();
        trackEmbedding.ToTable("track_embedding");
        trackEmbedding.HasKey(e => e.TrackId);
        trackEmbedding.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
        trackEmbedding.HasOne(e => e.Track)
            .WithOne()
            .HasForeignKey<TrackEmbedding>(e => e.TrackId)
            .OnDelete(DeleteBehavior.Cascade);
        trackEmbedding.HasIndex(e => e.EmbeddingMean)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
        trackEmbedding.HasIndex(e => e.EmbeddingMeanMax)
            .HasMethod("hnsw")
            .HasOperators("halfvec_cosine_ops");

        var similarTrack = modelBuilder.Entity<SimilarTrack>();
        similarTrack.ToTable("similar_track", t =>
        {
            t.HasCheckConstraint("similar_track_no_self", "anchor_track_id <> neighbor_track_id");
            t.HasCheckConstraint("similar_track_rank_positive", "rank >= 1");
        });
        similarTrack.HasKey(s => new { s.AnchorTrackId, s.Rank });
        similarTrack.HasIndex(s => s.NeighborTrackId);
        similarTrack.HasOne(s => s.AnchorTrack)
            .WithMany()
            .HasForeignKey(s => s.AnchorTrackId)
            .OnDelete(DeleteBehavior.Cascade);
        similarTrack.HasOne(s => s.NeighborTrack)
            .WithMany()
            .HasForeignKey(s => s.NeighborTrackId)
            .OnDelete(DeleteBehavior.Cascade);

        // The group tables are flavor unions: one row per (anchor, neighbor) with a
        // nullable rank per ordering. PK is the pair; each flavor gets a partial
        // unique index so a rank-ordered scan is as cheap as similar_track's.
        var similarRelease = modelBuilder.Entity<SimilarRelease>();
        similarRelease.ToTable("similar_release", t =>
        {
            t.HasCheckConstraint("similar_release_no_self",
                "anchor_release_id <> neighbor_release_id");
            t.HasCheckConstraint("similar_release_some_rank",
                "rank_style IS NOT NULL OR rank_raw IS NOT NULL OR rank_kde IS NOT NULL");
        });
        similarRelease.HasKey(s => new { s.AnchorReleaseId, s.NeighborReleaseId });
        similarRelease.HasIndex(s => new { s.AnchorReleaseId, s.RankStyle })
            .IsUnique().HasFilter("rank_style IS NOT NULL");
        similarRelease.HasIndex(s => new { s.AnchorReleaseId, s.RankRaw })
            .IsUnique().HasFilter("rank_raw IS NOT NULL");
        similarRelease.HasIndex(s => new { s.AnchorReleaseId, s.RankKde })
            .IsUnique().HasFilter("rank_kde IS NOT NULL");
        similarRelease.HasIndex(s => s.NeighborReleaseId);
        similarRelease.HasOne(s => s.AnchorRelease)
            .WithMany()
            .HasForeignKey(s => s.AnchorReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
        similarRelease.HasOne(s => s.NeighborRelease)
            .WithMany()
            .HasForeignKey(s => s.NeighborReleaseId)
            .OnDelete(DeleteBehavior.Cascade);

        var similarCircle = modelBuilder.Entity<SimilarCircle>();
        similarCircle.ToTable("similar_circle", t =>
        {
            t.HasCheckConstraint("similar_circle_no_self",
                "anchor_circle_id <> neighbor_circle_id");
            t.HasCheckConstraint("similar_circle_some_rank",
                "rank_style IS NOT NULL OR rank_raw IS NOT NULL OR rank_kde IS NOT NULL");
        });
        similarCircle.HasKey(s => new { s.AnchorCircleId, s.NeighborCircleId });
        similarCircle.HasIndex(s => new { s.AnchorCircleId, s.RankStyle })
            .IsUnique().HasFilter("rank_style IS NOT NULL");
        similarCircle.HasIndex(s => new { s.AnchorCircleId, s.RankRaw })
            .IsUnique().HasFilter("rank_raw IS NOT NULL");
        similarCircle.HasIndex(s => new { s.AnchorCircleId, s.RankKde })
            .IsUnique().HasFilter("rank_kde IS NOT NULL");
        similarCircle.HasIndex(s => s.NeighborCircleId);
        similarCircle.HasOne(s => s.AnchorCircle)
            .WithMany()
            .HasForeignKey(s => s.AnchorCircleId)
            .OnDelete(DeleteBehavior.Cascade);
        similarCircle.HasOne(s => s.NeighborCircle)
            .WithMany()
            .HasForeignKey(s => s.NeighborCircleId)
            .OnDelete(DeleteBehavior.Cascade);

        var embeddingConfig = modelBuilder.Entity<EmbeddingConfig>();
        embeddingConfig.ToTable("embedding_config", t =>
            t.HasCheckConstraint("embedding_config_single_row", "id"));
        embeddingConfig.HasKey(c => c.Id);
        embeddingConfig.Property(c => c.Id).HasDefaultValue(true).ValueGeneratedNever();

        base.OnModelCreating(modelBuilder);
    }
}
