# TLMC v6 schema proposal

Target: the fresh v6 deployment, before any of the 164,267 tracks are loaded. Nothing
here is a data migration — v6 starts empty, so this is "what should the initial
migration create", and the cost of each change is measured in ETL edits rather than
`UPDATE` statements.

Five items are marked **DECISION** because they are judgement calls I should not make
unilaterally. Everything else I'd treat as settled unless you disagree. (§1's
identifier scheme was one of them and is now settled: TypeID over UUIDv7.)

The DDL below is the target shape. The backend is EF-first (migrations are generated
from `Models/`), so the real work is model changes plus one generated migration; the
SQL is here to be precise about constraints and indexes that are easy to lose in
translation.

The four constructs this proposal leans on that aren't obviously portable were executed
against the running pgvector 0.8.6 / PG15 instance before writing them down: generated
sort columns over `jsonb`, `DEFERRABLE INITIALLY DEFERRED` unique constraints (including
an in-transaction renumber that transiently collides), partial HNSW indexes on both
`vector` and `halfvec`, and `pg_trgm` GIN. All four work.

> **EF caveat:** EF Core cannot express `DEFERRABLE` constraints or partial HNSW indexes
> through the fluent API. Those two need `migrationBuilder.Sql(...)` in the generated
> migration, and the model snapshot won't know about them — so they must not be
> reverse-engineered away by a later `dotnet ef migrations add`.

---

## Principles

1. **A path is not an identity.** Storage location is configuration; rows hold keys.
2. **Don't store what a convention determines.** Derived paths are computed, not
   persisted.
3. **Let the database enforce invariants** the application currently only hopes for.
4. **Anything a user browses by must be queryable** — which means normalized and
   indexed, not a `text[]`.
5. **Version anything a model produces**, so re-running inference can't silently mix
   vector spaces.
6. **Identifiers are legible at the boundary and narrow in storage** — a prefixed
   string everywhere a human reads one, 16 bytes everywhere it is joined.

---

## 1. Identity and keys

Two separate decisions, answered differently: **what Postgres stores** and **what the
API exposes**.

### Storage: `uuid` holding a UUIDv7

Every primary key stays a 16-byte `uuid` column. Standardise the value on **UUIDv7**.
EF Core 9 already mints v7 for `Guid` keys it generates; the ETL mints v4
(`Guid.NewGuid()` in PushToDb, `uuid.uuid4()` in the Python), so the catalogue is
currently v4 and the playlists are v7 by accident. v7's timestamp prefix gives B-tree
locality on bulk insert — inserts land near the right edge of the index instead of
scattering across it — which matters when one ETL run appends 164k tracks plus their
credits, search rows and 16.4M similarity rows.

- .NET: `Guid.CreateVersion7()` (.NET 9) in place of `Guid.NewGuid()`.
- Python: the `uuid6` package's `uuid7()`.

### Presentation: TypeID

The API, logs and ETL intermediates use [TypeID](https://github.com/jetify-com/typeid):
a type prefix, an underscore, then the same 128-bit UUIDv7 rendered as 26 characters of
Crockford base32.

```
trk_01jqx3v9k2ekhr8s7mnfz4b6cd
     └── base32 of the UUIDv7 ──┘
```

The [spec](https://github.com/jetify-com/typeid/tree/main/spec) is explicit that the
canonical storage form is the underlying UUID, which is exactly the split we want:
`trk_…` wherever a human or a client sees an identifier, 16 bytes wherever it is stored
or joined.

Storing the prefixed string instead would be expensive here, because this schema is
FK-dense. `similar_track` alone is ~16.4M rows carrying two id columns each: 32 bytes
of key per row as `uuid`, ~62 bytes as text — roughly **490 MB of extra heap in one
table**, before the primary key index and the neighbour index both widen too. The same
tax applies to `playlist_item`, `track_credit`, `track_tag`, `play_event` and
`track_original_song`. Text keys also make every comparison collation-dependent unless
declared `COLLATE "C"`, which is an easy thing to forget and an invisible cost once
forgotten.

Base32 of a v7 preserves byte order, so TypeIDs still sort by creation time.

### Prefix vocabulary

| Entity | Prefix | Note |
| --- | --- | --- |
| `release` | `rel_` | |
| `disc` | `dsc_` | |
| `track` | `trk_` | a track in this collection (an arrangement, usually) |
| `asset` | `file_` | a stored file |
| `artwork` | `art_` | a cover image and its variants |
| `circle` | `cir_` | |
| `contributor` | `ctb_` | |
| `tag` | `tag_` | |
| `original_work` | `work_` | a Touhou game/album the music originates from |
| `original_song` | `song_` | an original Touhou song |
| `playlist` | `pls_` | |
| `lyrics` | `lyr_` | |
| `user_profile` | `usr_` | |

`file_` rather than `ast_` on purpose: `ast_` and `art_` differ by one character and
would be routinely misread, which defeats the point. The `work_`/`song_`/`trk_` split
is worth noting — it makes "original song" and "the doujin track arranging it" visibly
different things, which they constantly are in this domain and currently are not.

`play_event` keeps its `bigint` identity key and gets no prefix; it is high-volume
internal data and nothing addresses an individual play. If the API ever needs to (say,
deleting one history entry), switch that column to `uuid` + `ply_` at the same time.

### Strongly-typed ids

The prefix fixes *human* confusion. It does not stop the compiler accepting a release
id where a track id belongs — both are `Guid`. Since every signature is being touched
anyway, wrap them:

```csharp
public readonly record struct TrackId(Guid Value)
{
    public const string Prefix = "trk";
    public override string ToString() => TypeIdFormatter.Format(Prefix, Value);
    public static TrackId Parse(string s) => new(TypeIdFormatter.Parse(Prefix, s));
}
```

`GetTrack(ReleaseId id)` then fails to compile rather than 404ing at runtime, and
parsing rejects `rel_…` where a `TrackId` is expected during model binding, before the
request reaches a repository.

`TypeIdFormatter` above is a placeholder. The reference implementations are Go and
TypeScript, so the .NET package options are third-party and should be vetted before
being taken as a dependency — the encoding is Crockford base32 over 16 bytes with a
fixed 26-character output, which is short enough to implement and unit-test directly
against the spec's test vectors if nothing suitable holds up.

EF keeps the column as `uuid` through a convention-level converter, so no DDL changes:

```csharp
protected override void ConfigureConventions(ModelConfigurationBuilder builder)
{
    builder.Properties<TrackId>().HaveConversion<TrackIdConverter>();   // TrackId <-> Guid
    // ...one per id type
}
```

Three wiring details that are easy to miss:

- **Serialization must target Newtonsoft, not System.Text.Json.** This project calls
  `AddNewtonsoftJson()`, so the id converters have to be `Newtonsoft.Json.JsonConverter`
  or ids will serialize as `{"value":"..."}` objects.
- **Route constraints change.** `{id:Guid}` no longer matches. Either drop to `{id}`
  with a model binder, or register a custom route constraint that validates the prefix —
  the latter gives a 404 at routing for a malformed id instead of a 400 deeper in.
- **Swagger needs `MapType<TrackId>`** returning a string schema, otherwise the OpenAPI
  document describes ids as objects and generated clients break.

**DECISION — identifier casing.** Current tables are EF-default PascalCase, so all raw
SQL needs `"Quoted"` identifiers. Since v6 is fresh and `TrackRepo` writes real SQL, I
lean toward `UseSnakeCaseNamingConvention()`. Purely cosmetic; it does mean rewriting
the raw-SQL filter builder's identifiers. The DDL below is written snake_case; say the
word and I'll flip it.

---

## 2. Media addressing

**The change that matters most.** Today `Assets.Path`, `HlsPlaylist.HlsPlaylistPath`
and `HlsSegment.Path` hold absolute host paths. That has already cost real work:
commit `d873d96` exists only to repoint a mount because rows said `TLMC v2`; the 791
directories carrying `U+F028`/`U+F029` from SMB round-tripping are only a problem
because paths are identifiers; and the arbitrary-file-read hole fixed in `9fcc38c`
existed because a path in a row is a path the API will open.

Rows store a **root-relative storage key** plus which root it belongs to. The root is
config, exactly like the identity mount is today, but the invariant lives in the schema
instead of in a `hostPath`.

```sql
CREATE TYPE storage_root AS ENUM ('library', 'thumbnail', 'generated');

CREATE TABLE asset (
    id            uuid        PRIMARY KEY,
    root          storage_root NOT NULL,
    -- Root-relative, forward-slashed, no leading slash. Resolution is
    -- Storage:Roots:<root> + '/' + storage_key, validated by AssetPathPolicy.
    storage_key   text        NOT NULL,
    name          text        NOT NULL,
    mime          text,
    byte_size     bigint      NOT NULL DEFAULT 0,
    -- Lowercase hex sha256 of the bytes. Enables dedup across TLMC's many
    -- duplicate rips, and gives strong ETags for free (media is immutable).
    content_hash  char(64),
    created_at    timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT asset_storage_key_shape CHECK (
        storage_key <> '' AND storage_key NOT LIKE '/%' AND storage_key NOT LIKE '%..%'
    ),
    CONSTRAINT asset_root_key_unique UNIQUE (root, storage_key)
);

CREATE INDEX asset_content_hash_idx ON asset (content_hash) WHERE content_hash IS NOT NULL;
```

The `CHECK` makes traversal unrepresentable rather than merely rejected at the edge,
and `UNIQUE (root, storage_key)` means the same file cannot be registered twice under
different ids — which is how you currently end up with orphaned asset rows.

`content_hash` is nullable so the ETL can backfill it asynchronously; hashing 164k
FLACs is not something to block ingestion on.

---

## 3. HLS: delete both tables

Your ladder is four rungs (`128k`, `192k`, `256k`, `320k`) and the single-file layout
emits one `stream.m4s` per rung. Per track that's 1 master + 4 media playlists + 4
segments, so **~821k `HlsPlaylist` rows and ~657k `HlsSegment` rows** for 164k tracks.
Every one of them is mechanically derivable from `hls_assignment.py`'s layout:

```
<track_dir>/playlist.m3u8                  master
<track_dir>/hls/<rung>/playlist.m3u8       media
<track_dir>/hls/<rung>/stream.m4s          media data (init segment as a byte range)
```

So `HlsPlaylist`, `HlsSegment` and `DashPlaylists` all collapse into two columns:

```sql
-- on track:
    media_key      text,           -- root-relative <track_dir>, 'library' root
    hls_bitrates   smallint[] NOT NULL DEFAULT '{}',   -- e.g. {128,192,256,320}
```

The API composes URLs from a single convention constant that must stay in step with
`hls_assignment.py`. 1.5M rows and two tables disappear, and an ETL step whose only job
was recording facts a convention already fixes disappears with them.

Keep a real table only if rungs will vary per track, or you want per-segment byte-range
metadata in the database. Neither is true today.

**DECISION — DASH.** `DashMusicAssetController` is live but I expect `DashPlaylists` is
empty and DASH was never produced. If you're not shipping DASH, drop the table and the
controller; if you are, it gets the same treatment as HLS (a key plus a convention).

---

## 4. Release / Disc / Track

`Album` is currently two entities in one table, disambiguated by the sentinel
`NumberOfDiscs > 1 AND DiscNumber = 0`, which leaks into `AlbumRepo` and `CircleRepo`
as a magic predicate. A disc row carries `CatalogNumber`, `ReleaseDate`, an
`AlbumArtist` collection and a `Thumbnail` that are all meaningless on a disc, plus a
`ParentAlbum`/`ChildAlbums` self-join to hold it together.

```sql
CREATE TABLE release (
    id                  uuid        PRIMARY KEY,
    name                jsonb       NOT NULL,   -- LocalizedField
    -- Generated sort/search key: jsonb is the wrong thing to ORDER BY.
    name_sort           text        GENERATED ALWAYS AS (lower(name->>'Default')) STORED,
    release_date        date,
    release_convention  text,
    catalog_number      text,
    websites            text[]      NOT NULL DEFAULT '{}',
    data_sources        text[]      NOT NULL DEFAULT '{}',
    tlmc_root_reference text[]      NOT NULL DEFAULT '{}',
    artwork_asset_id    uuid        REFERENCES asset (id) ON DELETE SET NULL,
    created_at          timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX release_name_sort_idx      ON release (name_sort);
CREATE INDEX release_release_date_idx   ON release (release_date);
CREATE INDEX release_catalog_number_idx ON release (catalog_number)
    WHERE catalog_number IS NOT NULL;

CREATE TABLE disc (
    id          uuid    PRIMARY KEY,
    release_id  uuid    NOT NULL REFERENCES release (id) ON DELETE CASCADE,
    disc_number smallint NOT NULL,
    name        text,

    CONSTRAINT disc_number_unique_per_release UNIQUE (release_id, disc_number),
    CONSTRAINT disc_number_positive CHECK (disc_number >= 1)
);

CREATE TABLE track (
    id            uuid        PRIMARY KEY,
    disc_id       uuid        NOT NULL REFERENCES disc (id) ON DELETE CASCADE,
    track_number  smallint    NOT NULL,
    name          jsonb       NOT NULL,
    name_sort     text        GENERATED ALWAYS AS (lower(name->>'Default')) STORED,
    duration      interval,
    original_non_touhou boolean,

    -- media, per section 3
    media_key     text,
    hls_bitrates  smallint[]  NOT NULL DEFAULT '{}',
    source_asset_id uuid      REFERENCES asset (id) ON DELETE SET NULL,

    lyrics_id     uuid        REFERENCES lyrics (id) ON DELETE SET NULL,
    created_at    timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT track_number_unique_per_disc UNIQUE (disc_id, track_number)
);

CREATE INDEX track_disc_id_idx   ON track (disc_id);
CREATE INDEX track_name_sort_idx ON track (name_sort);

-- Releases are attributed to circles; this replaces "AlbumCircle".
CREATE TABLE release_circle (
    release_id uuid     NOT NULL REFERENCES release (id) ON DELETE CASCADE,
    circle_id  uuid     NOT NULL REFERENCES circle (id) ON DELETE CASCADE,
    ordinal    smallint NOT NULL DEFAULT 0,
    PRIMARY KEY (release_id, circle_id)
);

CREATE INDEX release_circle_circle_id_idx ON release_circle (circle_id);
```

A single-disc release gets exactly one `disc` row. The sentinel, the self-join, and the
`NumberOfDiscs`/`DiscNumber` pair all go away, and `UNIQUE (disc_id, track_number)`
makes duplicate track numbering impossible instead of merely unlikely.

Note `AlbumCircle` currently lacks an index on the `AlbumArtistId` side, so
"albums by this circle" — a core browse path — has no supporting index. Fixed above.

---

## 5. Contributors and credits

`Genre`, `Staff`, `Arrangement`, `Vocalist`, `Lyricist` are `text[]` on `Tracks` and
**nothing in the codebase ever queries them.** They are write-only: the ETL fills them,
the API echoes them, no `WHERE` touches them, and there is no index on any of them.

For this collection that's backwards. "Everything this vocalist sang on", "every
arrangement of this original", "more from this arranger" is how people actually navigate
Touhou doujin music. It's also why free text hurts: `nomico`, `Nomico` and `ノミコ` are
three unrelated values today.

```sql
CREATE TYPE credit_role AS ENUM ('arranger', 'vocalist', 'lyricist', 'performer', 'staff');

CREATE TABLE contributor (
    id         uuid  PRIMARY KEY,
    name       text  NOT NULL,
    name_sort  text  GENERATED ALWAYS AS (lower(name)) STORED,
    -- Set when this contributor is (or fronts) a known circle, so credits and
    -- release attribution can be reconciled without merging the two tables.
    circle_id  uuid  REFERENCES circle (id) ON DELETE SET NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX contributor_name_sort_key ON contributor (name_sort);

-- Romanisations, kana, stage names. The variants you will inevitably accumulate.
CREATE TABLE contributor_alias (
    contributor_id uuid NOT NULL REFERENCES contributor (id) ON DELETE CASCADE,
    alias          text NOT NULL,
    alias_sort     text GENERATED ALWAYS AS (lower(alias)) STORED,
    PRIMARY KEY (contributor_id, alias)
);

CREATE UNIQUE INDEX contributor_alias_sort_key ON contributor_alias (alias_sort);

CREATE TABLE track_credit (
    track_id       uuid        NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    contributor_id uuid        NOT NULL REFERENCES contributor (id) ON DELETE CASCADE,
    role           credit_role NOT NULL,
    ordinal        smallint    NOT NULL DEFAULT 0,
    PRIMARY KEY (track_id, contributor_id, role)
);

-- The browse path: "all tracks where X is a vocalist".
CREATE INDEX track_credit_lookup_idx ON track_credit (contributor_id, role, track_id);
```

Genre is a vocabulary rather than a credit, so it gets its own pair:

```sql
CREATE TABLE tag (
    id        uuid PRIMARY KEY,
    name      text NOT NULL,
    name_sort text GENERATED ALWAYS AS (lower(name)) STORED
);
CREATE UNIQUE INDEX tag_name_sort_key ON tag (name_sort);

CREATE TABLE track_tag (
    track_id uuid NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    tag_id   uuid NOT NULL REFERENCES tag (id) ON DELETE CASCADE,
    PRIMARY KEY (track_id, tag_id)
);
CREATE INDEX track_tag_tag_id_idx ON track_tag (tag_id);
```

**DECISION — how far to normalize.** Three options, in increasing cost:

1. **Full normalization** (above). Unlocks browse-by-contributor properly. Cost: the
   ETL must resolve names to contributor ids, which means an alias/merge table and
   accepting that the first pass will create near-duplicates you clean up over time.
2. **Keep the arrays, add GIN indexes.** `CREATE INDEX ... USING gin (vocalist)` makes
   `vocalist @> ARRAY['nomico']` fast. Cheap, no ETL change, but exact-string only and
   no way to merge variants or count reliably.
3. **Both, temporarily.** Normalize *and* keep the arrays as the ETL's raw record, so
   you can re-derive contributors after improving the matching without re-reading
   thwiki. My recommendation — the arrays are small and it makes the normalization
   reversible.

I'd take (3), then drop the arrays once the contributor table looks right.

---

## 6. Artwork

`Thumbnail` has five separate FK columns (`OriginalId`, `LargeId`, `MediumId`,
`SmallId`, `TinyId`), so adding a size is a migration, and every thumbnail read pulls
five joined `Asset` rows — which is a large part of the cartesian blow-up behind the
`QuerySplittingBehavior` warning in your logs. `Album` additionally has *both* `Image`
(source) and `Thumbnail` (derived), and the derived set is generated by `UpdateDb` at
**application startup**, which is why deploys stall.

```sql
CREATE TABLE artwork (
    id               uuid  PRIMARY KEY,
    source_asset_id  uuid  NOT NULL REFERENCES asset (id) ON DELETE CASCADE,
    -- Dominant colours, for UI theming. Was Thumbnail.Colors.
    colors           text[] NOT NULL DEFAULT '{}'
);

CREATE TABLE artwork_variant (
    artwork_id uuid     NOT NULL REFERENCES artwork (id) ON DELETE CASCADE,
    -- Edge length in px; 0 means the unresized original.
    size_px    smallint NOT NULL,
    asset_id   uuid     NOT NULL REFERENCES asset (id) ON DELETE CASCADE,
    PRIMARY KEY (artwork_id, size_px)
);
```

Adding a 700px variant becomes an insert. `release.artwork_asset_id` becomes
`release.artwork_id`, and variant selection is one indexed lookup.

Thumbnail generation moves out of `Program.cs` startup into either the ETL or a hosted
background service. Same for the ffprobe duration backfill — a deploy should not block
on hours of media crunching, and with more than one replica they both run twice.

---

## 7. Search

There is no search endpoint at all; the `/api/search` route in `ingress.yaml` pointed
at nothing. What exists is `Regex.IsMatch(a.Name.Default, filter.Title)`, which Postgres
runs as `~*` over `"Name"->>'Default'`: a sequential scan on every call, unindexable,
and a `PostgresException` if a user types `(`.

Search spans tables (title + release + credits + original works), so a generated column
can't express it. Since the catalogue is written once per TLMC release, an
ETL-maintained document table is the honest design:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE track_search (
    track_id  uuid     PRIMARY KEY REFERENCES track (id) ON DELETE CASCADE,
    -- Weighted: A title, B release, C credits, D originals.
    document  tsvector NOT NULL,
    -- Flattened plain text for trigram/fuzzy matching on romanisations.
    haystack  text     NOT NULL
);

CREATE INDEX track_search_document_idx ON track_search USING gin (document);
CREATE INDEX track_search_haystack_idx ON track_search USING gin (haystack gin_trgm_ops);
```

**DECISION — locale handling.** `LocalizedField` carries `Default`/`En`/`Zh`/`Jp`. I'd
flatten all four into one `document` with the same weight class, because users search
in whichever script they happen to know and a Japanese title should be findable by its
romanization. The alternative is per-locale documents and an explicit language
parameter. Flattening is simpler and I think better here, but it's your call.

Note the config choice matters: `simple` avoids English stemming mangling romanized
Japanese. CJK won't segment properly with any built-in config — the trigram index is
what carries CJK substring search, which is why both indexes exist.

---

## 8. Plays and playlists

`History` is a `PlaylistType`, so append-only event data lives in a structure whose
invariant is a dense 1..N ordering. That's why the delete path has to renumber every
surviving row (see `7d2b526`), and `TimesPlayed` on a *playlist item* can't answer "how
often have I played this track" across playlists anyway.

```sql
CREATE TYPE play_source AS ENUM ('playlist', 'album', 'shuffle', 'similar', 'search', 'unknown');

CREATE TABLE play_event (
    id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id     uuid        NOT NULL REFERENCES user_profile (id) ON DELETE CASCADE,
    track_id    uuid        NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    played_at   timestamptz NOT NULL DEFAULT now(),
    source      play_source NOT NULL DEFAULT 'unknown',
    ms_played   integer
);

-- The history query.
CREATE INDEX play_event_user_recent_idx ON play_event (user_id, played_at DESC);
-- Play counts, "most played".
CREATE INDEX play_event_user_track_idx  ON play_event (user_id, track_id);
```

History becomes `SELECT ... ORDER BY played_at DESC LIMIT n`, play counts become an
indexed aggregate, and `PlaylistType` loses `History`. If the aggregate ever gets hot,
add a `track_play_stat` rollup; at your scale it won't for a long while.

Playlists keep the constraints they're currently missing:

```sql
CREATE TYPE playlist_kind       AS ENUM ('normal', 'favorite', 'queue');
CREATE TYPE playlist_visibility AS ENUM ('public', 'private', 'unlisted');

CREATE TABLE playlist (
    id            uuid                PRIMARY KEY,
    owner_id      uuid                NOT NULL REFERENCES user_profile (id) ON DELETE CASCADE,
    name          text                NOT NULL,
    kind          playlist_kind       NOT NULL DEFAULT 'normal',
    visibility    playlist_visibility NOT NULL DEFAULT 'private',
    created_at    timestamptz         NOT NULL DEFAULT now(),
    last_modified timestamptz         NOT NULL DEFAULT now(),

    CONSTRAINT playlist_name_length CHECK (char_length(name) BETWEEN 1 AND 200)
);

-- One Favorite and one Queue per user. This is the constraint whose absence lets the
-- check-then-insert bootstrap race into duplicates.
CREATE UNIQUE INDEX playlist_one_special_per_owner
    ON playlist (owner_id, kind) WHERE kind <> 'normal';

CREATE INDEX playlist_owner_idx ON playlist (owner_id);
CREATE INDEX playlist_public_idx ON playlist (visibility) WHERE visibility = 'public';

CREATE TABLE playlist_item (
    playlist_id uuid        NOT NULL REFERENCES playlist (id) ON DELETE CASCADE,
    track_id    uuid        NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    position    integer     NOT NULL,
    added_at    timestamptz NOT NULL DEFAULT now(),

    PRIMARY KEY (playlist_id, track_id),
    -- DEFERRABLE so a renumbering pass can shuffle positions inside one
    -- transaction without tripping the constraint mid-update. Non-deferrable would
    -- make compaction require a temporary offset.
    CONSTRAINT playlist_item_position_unique UNIQUE (playlist_id, position) DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT playlist_item_position_positive CHECK (position >= 1)
);

CREATE INDEX playlist_item_track_idx ON playlist_item (track_id);
```

Two deliberate changes: the PK is `(playlist_id, track_id)` rather than
`(track_id, playlist_id)` so the common "items of this playlist, in order" access is a
prefix scan; and `NumberOfTracks` is gone. It's a denormalized counter with no
concurrency token that already drifts — a cascade-deleted track removes items without
touching it. `COUNT(*)` against `playlist_item_position_unique` is cheap. If you want it
back for display, make it a trigger-maintained column, not application-maintained.

---

## 9. Embeddings and similarity

The pooled vectors in Postgres with HNSW are the right shape, and keeping the chunk
store on the filesystem is right. Two additions:

```sql
CREATE TABLE track_embedding (
    track_id      uuid          NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    -- e.g. 'mert-v1-330m/6s-2s/last4mean'. Cosine distance between different
    -- model versions is meaningless, so mixing them in one index is a silent
    -- correctness bug rather than a slow query.
    model_version text          NOT NULL,
    embedding_mean     vector(1024) NOT NULL,
    embedding_meanmax  halfvec(2048) NOT NULL,
    created_at    timestamptz   NOT NULL DEFAULT now(),

    PRIMARY KEY (track_id, model_version)
);
```

**Index caveat worth knowing before you build it:** HNSW doesn't compose with a
`WHERE model_version = ...` filter — the index scan happens first and the filter
discards afterwards, so a filtered query silently under-returns exactly the way the
unraised `ef_search` did. Two workable options: keep only one version live at a time
(simplest, and re-inference means a rebuild), or create **partial indexes per version**:

```sql
CREATE INDEX track_embedding_mean_v1_idx ON track_embedding
    USING hnsw (embedding_mean vector_cosine_ops)
    WHERE model_version = 'mert-v1-330m/6s-2s/last4mean';

CREATE INDEX track_embedding_meanmax_v1_idx ON track_embedding
    USING hnsw (embedding_meanmax halfvec_cosine_ops)
    WHERE model_version = 'mert-v1-330m/6s-2s/last4mean';
```

The precomputed chamfer neighbours become a real table. Your v5 numbers justify making
this the primary serving path: same-artist@10 of 26.2% against a 52× random baseline,
same-album@10 of 6.9% against 345×, and the full 140k-track run in 80 seconds.

```sql
CREATE TABLE similar_track (
    anchor_track_id   uuid      NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    model_version     text      NOT NULL,
    rank              smallint  NOT NULL,
    neighbor_track_id uuid      NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    -- Symmetric chamfer similarity. Note the top of this distribution is
    -- compressed into roughly 0.986..0.994, and >= ~0.999 indicates a
    -- near-duplicate rip -- so the UI must rescale rather than show it raw.
    score             real      NOT NULL,

    PRIMARY KEY (anchor_track_id, model_version, rank),
    CONSTRAINT similar_track_no_self CHECK (anchor_track_id <> neighbor_track_id),
    CONSTRAINT similar_track_rank_positive CHECK (rank >= 1)
);

CREATE INDEX similar_track_neighbor_idx ON similar_track (neighbor_track_id);
```

**DECISION — serving path.** If `similar_track` is primary, `GET /track/{id}/similar`
becomes a keyset read against the PK and the HNSW tier is only a fallback for tracks
without a precomputed row (newly ingested, or arbitrary query vectors). That makes the
`ef_search` work from `8b97201` off the critical path, and the diversity re-ranker
operates on precomputed neighbours instead of ANN candidates. I recommend it. The
alternative — ANN as primary — keeps one code path but permanently accepts pooled-vector
recall quality when you've already measured that chamfer is much better.

`CHECK (anchor <> neighbor)` encodes at the schema level the self-exclusion bug fixed
in `8b97201`.

---

## 10. Reference data (original works)

`OriginalAlbum.Id` and `OriginalTrack.Id` are `text` primary keys shaped
`{csvSourceId}-{trackIndex}`. They're brittle (the index component comes from scraped
thwiki text), they're interpolated into raw SQL in `CreateTrackFilterWhereStatement`
where validation is the only thing preventing injection, and a natural key that can be
re-derived differently is a poor PK.

```sql
CREATE TABLE original_work (          -- was OriginalAlbums
    id            uuid  PRIMARY KEY,
    external_key  text  NOT NULL,     -- the old '{csvSourceId}' natural key
    work_type     text  NOT NULL,
    full_name     jsonb NOT NULL,
    short_name    jsonb NOT NULL,
    external_ref  text,

    CONSTRAINT original_work_external_key_unique UNIQUE (external_key)
);

CREATE TABLE original_song (          -- was OriginalTracks
    id               uuid  PRIMARY KEY,
    original_work_id uuid  NOT NULL REFERENCES original_work (id) ON DELETE CASCADE,
    external_key     text  NOT NULL,  -- the old '{csvSourceId}-{index}'
    title            jsonb NOT NULL,
    track_index      smallint,
    external_ref     text,

    CONSTRAINT original_song_external_key_unique UNIQUE (external_key)
);

CREATE INDEX original_song_work_idx ON original_song (original_work_id);

CREATE TABLE track_original_song (    -- was OriginalTrackTrack
    track_id         uuid NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    original_song_id uuid NOT NULL REFERENCES original_song (id) ON DELETE CASCADE,
    PRIMARY KEY (track_id, original_song_id)
);

CREATE INDEX track_original_song_reverse_idx ON track_original_song (original_song_id, track_id);
```

Surrogate uuid PKs mean the filter API takes uuids, which kills the raw-SQL
interpolation problem outright — and the reverse index makes "every arrangement of
*U.N. Owen Was Her?*" a proper indexed lookup, which is one of the most obviously
desirable queries in this whole domain and is currently unsupported.

---

## 11. Tables that go away

| Dropped | Replaced by |
| --- | --- |
| `HlsPlaylist`, `HlsSegment` | `track.media_key` + `track.hls_bitrates` |
| `DashPlaylists` | same, if DASH ships at all (**DECISION**) |
| `Thumbnails` (5 FK columns) | `artwork` + `artwork_variant` |
| `Albums` (dual-purpose) | `release` + `disc` |
| `PlaylistItems.TimesPlayed` | `play_event` |
| `Playlists.NumberOfTracks` | `COUNT(*)`, or a trigger |
| `Tracks.Genre/Staff/Arrangement/Vocalist/Lyricist` | `track_credit`, `track_tag` (kept transitionally — see §5) |

---

## 12. ETL implications

**`Finalizer/PushToDb`** — the bulk of the work.
- Mint UUIDv7 (`Guid.CreateVersion7()`).
- Emit TypeIDs (`trk_…`, `rel_…`) in the JSON intermediates and log lines rather than
  bare uuids. The database still receives uuids; this is purely so a worklist, a
  journal entry or a failure message says what it is referring to. Given how much of
  this pipeline is debugged by reading intermediate files, this is probably where the
  prefixes earn their keep fastest.
- Write `release` + `disc` instead of the album/disc-0 pair.
- Stop emitting `HlsPlaylist`/`HlsSegment`; write `media_key` and `hls_bitrates`.
- Write root-relative `storage_key` + `root` for assets, not absolute paths.
- Resolve credit strings to `contributor` rows, and populate `track_credit`/`track_tag`.
- Populate `track_search`.
- Also fix the two loader bugs already identified: clear the EF change tracker between
  batches (currently quadratic), and assert vector length on load so a truncated `.bin`
  fails one row rather than a 5,000-row batch mid-run with no resume path.

**`Postprocessor/HlsTranscode`** — no functional change, but `hls_finalizer.py` stops
needing to emit a segment manifest for the DB. The layout convention becomes a contract
shared with the backend, so it's worth writing it down in one place both sides
reference.

**`ExternalInfo/ThwikiInfoProvider`** — `original_track_discovery` / `commit_origina_album_and_track`
now post uuids plus `external_key` rather than composite text ids. This is also where
contributor alias resolution belongs, since thwiki is where the name variants come from.
These pushes go through `api/internal`, which now needs the `X-Internal-Api-Key` header
(one line, per `9fcc38c`).

**New ETL step: embeddings + similarity.** Load `track_embedding` with an explicit
`model_version`, then populate `similar_track` from `precompute_similar_tracks.py`'s
shard CSVs. That's a straight `COPY` of the existing `(anchor_id, neighbor_id, rank, score)`
output plus a version column — the script already emits exactly this shape.

---

## 13. Rollout order

1. Settle the six **DECISION** points.
2. Rewrite `Models/` and generate one initial migration. Keep migrations out of app
   startup — a Job or an explicit `dotnet ef database update`, with its own timeout
   (per `8b97201`).
3. Load reference data (`original_work`, `original_song`, `circle`).
4. Load catalogue (`release`, `disc`, `track`, `asset`, `contributor`, credits).
5. Build indexes **after** the bulk load, not before — index maintenance during a 164k
   insert is much slower than one build afterwards, and it's why the migration timeout
   matters.
6. Backfill `track_search`, then artwork variants, then `content_hash`.
7. Load `track_embedding`, build the HNSW indexes, load `similar_track`.

Steps 6 and 7 are independent of each other and of serving; the API is usable after 5.

---

## 14. Deliberately unchanged

`LocalizedField` as jsonb (right for storage; the generated `*_sort` columns fix the
query side). The `Lyrics` variant/line/ruby model — it's genuinely well designed for
this domain and I wouldn't touch it. Keycloak `sub` as `user_profile.id`. `Circle` and
its `CircleWebsite` child. The two-tier retrieval design. EF as the access layer.

---

## 15. Open decisions

| # | Decision | My recommendation |
| --- | --- | --- |
| 0 | ~~Identifier scheme~~ | **Settled: TypeID over UUIDv7, stored as `uuid` (§1)** |
| 1 | Identifier casing (snake_case vs EF PascalCase) | snake_case, since v6 is fresh and you write raw SQL |
| 2 | Ship DASH at all? | Drop it unless a client needs it |
| 3 | How far to normalize credits (§5) | Option 3: normalize *and* keep raw arrays transitionally |
| 4 | Search: flatten locales vs per-locale documents | Flatten, weight by field not language |
| 5 | `similar_track` as primary serving path | Yes; ANN becomes the fallback |
| 6 | Keep a `NumberOfTracks`-style counter? | No; derive it, or use a trigger if the UI needs it |
