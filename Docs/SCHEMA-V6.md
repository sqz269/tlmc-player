# TLMC v6 schema proposal

Target: the fresh v6 deployment, before any of the 164,267 tracks are loaded. Nothing
here is a data migration — v6 starts empty, so this is "what should the initial
migration create", and the cost of each change is measured in ETL edits rather than
`UPDATE` statements.

Judgement calls are collected in §15 — four are now settled (identifiers, DASH, search
implementation, credit normalization) and five remain open. Everything not listed there
I'd treat as settled unless you disagree.

The DDL below is the target shape. The backend is EF-first (migrations are generated
from `Models/`), so the real work is model changes plus one generated migration; the
SQL is here to be precise about constraints and indexes that are easy to lose in
translation.

The constructs this proposal leans on that aren't obviously portable were executed
against the running pgvector 0.8.6 / PG15 instance before writing them down: generated
sort columns over `jsonb`, `DEFERRABLE INITIALLY DEFERRED` unique constraints (including
an in-transaction renumber that transiently collides), and partial HNSW indexes on both
`vector` and `halfvec`. All three work. (`pg_trgm` GIN was verified too, but is no
longer needed now that search leaves Postgres — see §7.)

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
credits and 16.4M similarity rows.

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

**DASH stays** (correcting an earlier assumption of mine that it was vestigial — there
is a real `Postprocessor/DashRepackage` stage and a `MpegDashPlaylistProcessor` in
PushToDb).

It gets the same treatment as HLS rather than an exemption, because it has the same
property: `dash-repackage.py` writes `BaseURL` entries relative to the manifest's own
directory, so the `.mpd` is self-contained and its location is conventional. That means
one more column and no table:

```sql
-- on track, alongside media_key / hls_bitrates:
    has_dash boolean NOT NULL DEFAULT false,
```

So `DashPlaylists` still goes away while DASH serving stays — the table was recording a
path the convention already fixes, exactly as with HLS. `DashMusicAssetController`
composes the manifest URL from `media_key` instead of reading a row.

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

### Separating what was scraped from who we think it is

The obvious objection to normalizing credits is that the source data does not deserve
it. These strings come off thwiki: inconsistent romanization, several people crammed
into one field, circle names appearing where a vocalist belongs, `feat.` constructions,
typos. Resolving them into foreign keys asserts a confidence nobody has, and a bad merge
baked into an FK is expensive to unpick.

The fix is to stop treating those as one thing. A credit row records **two** facts with
very different epistemic status:

1. **What the source said** — verbatim, ordered, immutable. Raw data.
2. **Who we currently think that is** — a nullable FK, revisable forever.

```sql
CREATE TYPE credit_match_method AS ENUM ('unresolved', 'exact', 'alias', 'fuzzy', 'manual');

CREATE TABLE track_credit (
    track_id         uuid        NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    role             credit_role NOT NULL,
    ordinal          smallint    NOT NULL,

    -- Verbatim, never rewritten. This is what the API displays and what the search
    -- index sees, so presentation is always faithful to the source regardless of
    -- whether anything below is filled in.
    credit_name      text        NOT NULL,

    -- Our current opinion. NULL until resolved; ingestion never blocks on it.
    contributor_id   uuid        REFERENCES contributor (id) ON DELETE SET NULL,
    match_method     credit_match_method NOT NULL DEFAULT 'unresolved',
    match_confidence real,

    PRIMARY KEY (track_id, role, ordinal)
);

-- Browse: "everything this contributor sang on".
CREATE INDEX track_credit_contributor_idx
    ON track_credit (contributor_id, role, track_id) WHERE contributor_id IS NOT NULL;
-- Exact-string fallback while resolution is incomplete.
CREATE INDEX track_credit_name_idx ON track_credit (lower(credit_name));
-- The resolution worklist.
CREATE INDEX track_credit_unresolved_idx ON track_credit (role) WHERE contributor_id IS NULL;
```

`contributor` gains a redirect so merges are reversible without rewriting credit rows:

```sql
ALTER TABLE contributor ADD COLUMN merged_into uuid REFERENCES contributor (id);
```

What this buys:

- **Ingestion cannot be blocked by bad data.** Every scraped string is written; the FK
  is left NULL. A load never fails because a name was ambiguous.
- **Resolution is a separate, re-runnable pass.** Improve the matcher, re-run it, the
  FKs move. Nothing was lost, because `credit_name` is still there. This is the property
  the arrays were supposed to provide in my earlier draft, and it provides it better —
  ordered, per-role, and joined to the resolution in the same row.
- **You can be conservative.** Auto-resolve only `exact` and `alias`; leave `fuzzy`
  matches unresolved or flagged, and promote them to `manual` as you confirm them.
  `match_confidence` makes "show me everything the matcher guessed at" a query.
- **Partial resolution degrades gracefully.** An unresolved credit still displays and
  is still searchable; it just isn't browsable as an entity yet.

This replaces the raw `text[]` columns entirely — `credit_name` is a strictly better raw
record than the arrays were, so §11's transitional-arrays note no longer applies.

**It also matters that search is external (§7).** Because the search index consumes
`credit_name` directly, free-text credit search works from day one whether or not a
single credit is ever resolved. That reduces normalization from a prerequisite to an
enhancement: it buys faceting, "more from this vocalist", and reliable counts, and if
the resolution pass is only 60% accurate on the first run, the thing users notice most
still works correctly.

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

**Decided: search moves to a dedicated engine, not Postgres.** So there is no
`track_search` table, no `tsvector`, and no `pg_trgm` dependency. Postgres keeps
`name_sort` — that is for `ORDER BY` on list endpoints, which is a different job.

That makes the schema's obligation a different one: the database stays the source of
truth and must be able to **rebuild the index from scratch at any time**, and to do so
incrementally. Which means every indexed entity needs `updated_at`:

```sql
ALTER TABLE track   ADD COLUMN updated_at timestamptz NOT NULL DEFAULT now();
ALTER TABLE release ADD COLUMN updated_at timestamptz NOT NULL DEFAULT now();
-- ...and a trigger, or set it in the repository layer.

CREATE INDEX track_updated_at_idx ON track (updated_at);
```

Full reindex is a scan; incremental is `WHERE updated_at > :watermark`. Without this,
every credit correction means a full 164k-document rebuild.

### Document shape

One document per track, fully denormalized — the engine should never need a join:

```
id, title (all four locales), release title, release date, catalog number,
circle names, credit_name strings (all roles, unresolved included),
original song + work titles, tag names, duration, has_lyrics
```

Note credits enter as the **raw `credit_name` strings** from §5, not as resolved
contributors. That is what makes credit search work before normalization does.

### Meilisearch or Elasticsearch

I'd take **Meilisearch** here. 164k documents is small; typo tolerance out of the box
matters a lot for romanized Japanese, where users approximate spellings constantly; and
it is one binary against Elasticsearch's JVM and cluster assumptions. That last point is
concrete — this node already runs Keycloak, two Postgres instances and the API, and
Elastic wants a couple of GB of heap before it does anything useful.

Take Elasticsearch instead if you expect to need real aggregation/analytics later, or
you want kuromoji-grade Japanese analysis with hand-tuned dictionaries. Neither looks
likely for a music catalogue browse UI.

### The CJK trap — configure this explicitly or it will be subtly wrong

Meilisearch tokenizes through [charabia](https://github.com/meilisearch/charabia), which
has proper Japanese (lindera) and Chinese (jieba) segmenters. But which one it picks is
decided by `whatlang` language detection, and
[whatlang classifies text as Mandarin unless at least ~5% of characters are hiragana or
katakana](https://github.com/meilisearch/product/discussions/532).

A large share of Touhou titles are pure kanji. Those get **Chinese segmentation applied
to Japanese text** — silently, with no error, producing quietly poor recall on exactly
the titles most likely to be searched in their original script.

The fix exists but must be turned on: Meilisearch 1.10 added
[`localizedAttributes`](https://www.meilisearch.com/docs/reference/api/settings) as an
index setting and `locales` as a search parameter, and `locales` takes precedence over
detection. So declare the locale per attribute (`jpn` for the Japanese title field,
`eng` for romanized) rather than letting detection guess, and pass `locales` on queries
where the UI knows the script. Verify against whatever version you deploy; this is a
1.10+ feature.

This is the single thing I would test first with real data before committing to
Meilisearch — index a few hundred kanji-only titles and check recall with and without
`localizedAttributes` set.

### Operational notes

- **Reindex ownership.** The ETL pushes after a catalogue load; the backend pushes on
  mutation. Both go through one projection function so the document shape cannot drift
  between them.
- **The engine is not the source of truth.** Losing it entirely should cost a reindex,
  never data.
- **Failure mode.** If the engine is down, search should 503 cleanly — but browse,
  playback and playlists must not depend on it. Keep list endpoints served from
  Postgres so an engine outage degrades one feature rather than the site.

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
| `DashPlaylists` | `track.media_key` + `track.has_dash` (DASH serving stays) |
| `Thumbnails` (5 FK columns) | `artwork` + `artwork_variant` |
| `Albums` (dual-purpose) | `release` + `disc` |
| `PlaylistItems.TimesPlayed` | `play_event` |
| `Playlists.NumberOfTracks` | `COUNT(*)`, or a trigger |
| `Tracks.Genre/Staff/Arrangement/Vocalist/Lyricist` | `track_credit.credit_name` (verbatim) + `track_tag` |

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
- Write credits verbatim into `track_credit.credit_name` with `contributor_id` NULL.
  Resolution is a separate pass (§5), not part of ingestion — a load must never fail
  because a scraped name was ambiguous.
- Push search documents to the engine after load (§7), and maintain `updated_at` so
  later runs can reindex incrementally instead of rebuilding all 164k documents.
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

1. Settle the five remaining decisions in §15.
2. Rewrite `Models/` and generate one initial migration. Keep migrations out of app
   startup — a Job or an explicit `dotnet ef database update`, with its own timeout
   (per `8b97201`).
3. Load reference data (`original_work`, `original_song`, `circle`).
4. Load catalogue (`release`, `disc`, `track`, `asset`, credits as verbatim
   `credit_name` with `contributor_id` left NULL).
5. Build indexes **after** the bulk load, not before — index maintenance during a 164k
   insert is much slower than one build afterwards, and it's why the migration timeout
   matters.
6. Artwork variants, then `content_hash`.
7. Load `track_embedding`, build the HNSW indexes, load `similar_track`.
8. Run the credit resolution pass, populating `contributor` and setting
   `track_credit.contributor_id`. Re-runnable, and deliberately last — nothing upstream
   depends on it.
9. Build the search index from the catalogue projection.

Steps 6 through 9 are independent of each other and of serving; the API is usable after
5, with search and contributor browse arriving as they complete. If the resolution pass
in 8 turns out disappointing, it can be re-run after improving the matcher without
touching anything else — that is the whole point of §5's split.

---

## 14. Deliberately unchanged

`LocalizedField` as jsonb (right for storage; the generated `*_sort` columns fix the
query side). The `Lyrics` variant/line/ruby model — it's genuinely well designed for
this domain and I wouldn't touch it. Keycloak `sub` as `user_profile.id`. `Circle` and
its `CircleWebsite` child. The two-tier retrieval design. EF as the access layer.

---

## 15. Open decisions

Settled:

| Decision | Outcome |
| --- | --- |
| Identifier scheme | TypeID over UUIDv7, stored as `uuid` (§1) |
| Ship DASH? | Yes, keep serving — but `DashPlaylists` still collapses to a column (§3) |
| Search implementation | External engine, not Postgres FTS (§7) |
| How far to normalize credits | Verbatim `credit_name` + revisable nullable FK (§5) |

Still open:

| # | Decision | My recommendation |
| --- | --- | --- |
| 1 | Identifier casing (snake_case vs EF PascalCase) | snake_case, since v6 is fresh and you write raw SQL |
| 2 | Meilisearch or Elasticsearch (§7) | Meilisearch — 164k docs is small, typo tolerance suits romanized titles, and one binary beats a JVM on this node |
| 3 | Auto-resolve `fuzzy` credit matches, or leave them for manual review? (§5) | Leave unresolved; promote to `manual` as confirmed. Wrong merges are worse than missing ones |
| 4 | `similar_track` as primary serving path | Yes; ANN becomes the fallback |
| 5 | Keep a `NumberOfTracks`-style counter? | No; derive it, or use a trigger if the UI needs it |
