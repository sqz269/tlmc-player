# TLMC v6 schema proposal

Target: the fresh v6 deployment, before any of the 164,287 tracks are loaded. Nothing
here is a data migration — v6 starts empty, so this is "what should the initial
migration create", and the cost of each change is measured in ETL edits rather than
`UPDATE` statements.

Judgement calls are collected in §16 — everything substantive is now settled; two small
ones remain open. Everything not listed there I'd treat as settled unless you disagree.

The DDL below is the target shape. The backend is EF-first (migrations are generated
from `Models/`), so the real work is model changes plus one generated migration; the
SQL is here to be precise about constraints and indexes that are easy to lose in
translation.

The constructs this proposal leans on that aren't obviously portable were executed
against the running pgvector 0.8.6 / PG15 instance before writing them down: generated
sort columns over `jsonb`, `DEFERRABLE INITIALLY DEFERRED` unique constraints (including
an in-transaction renumber that transiently collides), and partial HNSW indexes on both
`vector` and `halfvec`. All three work. (`pg_trgm` GIN was verified too, but is no
longer needed now that search leaves Postgres — see §7. The partial-HNSW result is
likewise moot now that §9 keeps a single live model per database; it stands as a record
of what was tested.)

> **EF caveat:** EF Core cannot express `DEFERRABLE` constraints through the fluent
> API. Those need `migrationBuilder.Sql(...)` in the generated migration, and the model
> snapshot won't know about them — so they must not be reverse-engineered away by a
> later `dotnet ef migrations add`. (HNSW itself is not affected: the Npgsql provider
> expresses it with `HasMethod("hnsw")`/`HasOperators(...)`, which the current
> `AppDbContext` already uses — and with §9 dropping per-version partial indexes,
> nothing on the vector side needs raw SQL either.)

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
EF Core 9 already mints v7 for `Guid` keys it generates; the catalogue's ids are minted
as v4 in the Python aggregator (`uuid.uuid4()` in `id_assign_and_merge.py` — PushToDb
only parses what the intermediates hand it), so the catalogue is currently v4 and the
playlists are v7 by accident. v7's timestamp prefix gives B-tree
locality on bulk insert — inserts land near the right edge of the index instead of
scattering across it — which matters when one ETL run appends 164k tracks plus their
credits and 16.4M similarity rows.

- Python, where the catalogue ids are born: the `uuid6` package's `uuid7()`.
- .NET: `Guid.CreateVersion7()` (.NET 9) for anything the backend itself generates.

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
| `contributor` | `ctb_` | reserved — the identity layer is deferred (§5) |
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
`queue_item` (§8) gets the same treatment for the same reasons.

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

### Casing

There are **three** independent naming surfaces here, and they are easily conflated:

| Surface | Today | Controlled by |
| --- | --- | --- |
| C# properties | `Track.Name` | the language; not changing |
| Postgres identifiers | `"Tracks"."Name"` | EF's naming convention |
| jsonb document keys | `{"Default": "…"}` | the serializer, independently |

**Postgres identifiers.** EF maps property names straight through, so tables and columns
are PascalCase. Unquoted identifiers fold to lowercase in Postgres, so every raw query
has to double-quote everything. That isn't hypothetical here —
`CreateTrackFilterWhereStatement` writes `"ReleaseDate"`, `"CircleIds"`,
`"OriginalAlbumIds"`, `"OriginalTrackIds"`; `SampleRandomTrack`'s CTE quotes `"TrackId"`
and `"AlbumId"`; and every psql session against this database needs
`select … from "Tracks"` rather than `from tracks`.

`UseSnakeCaseNamingConvention()` from the `EFCore.NamingConventions` package produces
`tracks.name` and the quoting disappears. Worth knowing for the dependency call: that
package is maintained by Shay Rojansky, who also maintains Npgsql and the EF Core
Postgres provider — not a random third party.

The cost is rewriting identifiers in the raw-SQL builders, which are being rewritten
anyway for the release/disc split and the credit tables, plus different EF-generated
index and constraint names, which is irrelevant on a fresh database.

The honest counter-argument: EF gives PascalCase for free, and this adds a package for a
benefit that is mostly ergonomic. I still lean snake_case — it's the Postgres
convention, every tool and answer you search for assumes it, and you work in psql and
hand-written SQL often enough for the quoting to be a standing tax.

**jsonb keys — the part I previously mislabelled cosmetic.** The documents inside
`LocalizedField` and `Lyrics` currently carry PascalCase keys (`{"Default": …, "En": …}`),
because Npgsql maps the POCO property names through as-is. You can see it in the existing
translation of `a.Name.Default`, which becomes `"Name"->>'Default'`.

This one is *not* cosmetic, because the proposed sort columns bake the key into the table
definition:

```sql
name_sort text GENERATED ALWAYS AS (lower(name->>'Default')) STORED
```

Change jsonb key casing afterwards and that expression is silently wrong. A generated
column expression can't be altered in place — it's `DROP COLUMN`, re-add, and rebuild
every index over it. It also reaches the search projection and anything consuming the raw
jsonb shape through a DTO.

So decide this **before** the generated columns exist, and note that it moves
independently of the other two surfaces: switching the naming convention does *not*
change jsonb keys.

### Settled: snake_case everywhere except C#

Since every identifier is being rewritten anyway, one convention applies to all of them.
That includes the JSON on the wire — see §15, which counts a fourth surface.

| Surface | Convention | Configured by |
| --- | --- | --- |
| C# properties | PascalCase | unchanged; it is the language's convention |
| Postgres identifiers | `snake_case` | `UseSnakeCaseNamingConvention()` (`EFCore.NamingConventions`) |
| jsonb document keys | `snake_case` | `JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower`, handed to Npgsql's dynamic JSON |
| API JSON (§15) | `snake_case` | Newtonsoft `SnakeCaseNamingStrategy`, on the contract resolver **and** on `StringEnumConverter` |

One convention, one boundary — C# to everything else — and three places to configure it.

Extending it to the wire is what makes the whole thing hold together, because it
dissolves an exception that would otherwise be permanent. §15 explains that jsonb
returned to the client verbatim has its stored keys become the API contract; with a
camelCase API that forced `Lyrics` to be spelled differently from every other table in
the database. With a snake_case API the pass-through document is already correct, and
nobody has to remember a one-table carve-out that no API file mentions.

`default` is a reserved word in SQL but only ever appears as a string key here, so
`name->>'default'` is fine.

The Npgsql side is worth a note: jsonb key casing is controlled by the serializer
options passed to dynamic JSON, not by the EF naming convention. That also argues for
moving off `NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson()` — the deprecated
global-mapper API this project still calls — to `NpgsqlDataSourceBuilder`, which is
where those options are supplied.

---

## 2. Media addressing

**The change that matters most.** Today `Assets.Path`, `HlsPlaylist.HlsPlaylistPath`
and `HlsSegment.Path` hold absolute host paths. That has already cost real work:
commit `d873d96` exists only to repoint a mount because rows said `TLMC v2`; the
directories carrying `U+F028`/`U+F029` from SMB round-tripping are only a problem
because paths are identifiers; and the arbitrary-file-read hole fixed in `9fcc38c`
existed because a path in a row is a path the API will open. That last class is still
open, in fact: only `AssetController` routes through `AssetPathPolicy` — the HLS and
DASH controllers `PhysicalFile()` their stored paths with no containment check at all.

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

    -- '..' is rejected as a *path segment*, not as a substring: TLMC filenames
    -- legitimately contain consecutive dots, so a plain LIKE '%..%' would bounce
    -- real rows at load time. Backslashes and normalization are AssetPathPolicy's job.
    CONSTRAINT asset_storage_key_shape CHECK (
        storage_key <> ''
        AND storage_key NOT LIKE '/%'
        AND storage_key !~ '(^|/)\.\.(/|$)'
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

The API composes URLs from `media_key` plus a single convention constant that must stay
in step with `hls_assignment.py`. One caveat keeps `media_key` a stored column rather
than a derived one: `hls_assignment.py` renames colliding stems to `stem [ext]` (204
directories in the v6 tree), so the track directory is not derivable from metadata
alone — the finalizer manifest carries it (§12). 1.5M rows and two tables disappear,
and an ETL step whose only job was recording facts a convention already fixes
disappears with them.

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

Two details belong in that convention doc, because today they are implicit in code.
`dash-repackage.py` strips `hls/` from the `BaseURL` it emits (`.replace("hls/", "")`),
so the manifest's internal references do **not** match the on-disk `hls/<rung>/` layout
— today that only works because DASH segment requests are resolved through `HlsSegment`
rows, which are going away. Either encode the strip in the URL template or fix the
script to emit the true relpath. And the writer being deleted here is the *backend's*
`EtlDataLoader/MpegDashPlaylistProcessor` — the same-named processor in tlmc-etl is an
unreachable stub that writes nothing (§12).

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
    name_sort           text        GENERATED ALWAYS AS (lower(name->>'default')) STORED,
    release_date        date,
    release_convention  text,
    catalog_number      text,
    websites            text[]      NOT NULL DEFAULT '{}',
    data_sources        text[]      NOT NULL DEFAULT '{}',
    tlmc_root_reference text[]      NOT NULL DEFAULT '{}',
    artwork_id          uuid        REFERENCES artwork (id) ON DELETE SET NULL,  -- §6
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
    name_sort     text        GENERATED ALWAYS AS (lower(name->>'default')) STORED,
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
**nothing in the codebase ever queries them.** They are write-only: the ETL fills
`Staff`, the internal API appends the rest (with its dedup lines commented out), the
API echoes them, no `WHERE` touches them, and there is no index on any of them. The
only filter model that mentions them (`TrackFilter.Staff`) is itself dead code.

For this collection that's backwards. "Everything this vocalist sang on", "every
arrangement of this original", "more from this arranger" is how people actually navigate
Touhou doujin music. It's also why free text hurts: `nomico`, `Nomico` and `ノミコ` are
three unrelated values today.

A credit records two facts with very different epistemic status: **what the source
said** — verbatim, ordered, immutable — and **who we think that string is** — an
identity, revisable forever. v6 ships only the first. The second (the identity layer:
a `contributor` table, aliases, match metadata) is deferred outright; see below for why
and for what its eventual shape has to look like.

```sql
CREATE TYPE credit_role AS ENUM ('arranger', 'composer', 'vocalist', 'lyricist', 'performer', 'staff');

CREATE TABLE track_credit (
    track_id    uuid        NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    role        credit_role NOT NULL,
    ordinal     smallint    NOT NULL,

    -- Verbatim, never rewritten. This is what the API displays and what the
    -- search index sees, so presentation is always faithful to the source.
    credit_name text        NOT NULL,

    PRIMARY KEY (track_id, role, ordinal)
);

-- String-identity browse: "everything credited to exactly this name".
CREATE INDEX track_credit_name_idx ON track_credit (lower(credit_name));
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

### Why the identity layer is deferred, not designed away

The obvious objection to normalizing credits is that the source data does not deserve
it. These strings come off thwiki: inconsistent romanization, several people crammed
into one field, circle names appearing where a vocalist belongs, `feat.` constructions,
typos. Resolving them into foreign keys asserts a confidence nobody has, and a bad merge
baked into an FK is expensive to unpick.

An earlier draft answered that with a nullable `contributor_id` plus match metadata on
every credit row. The deeper problem is that identity is hard even with clean strings:
distinct people legitimately share a name (which makes a unique index on `lower(name)`
unrepresentable-by-design — a mistake that draft contained), aliases are legitimately
ambiguous, and resolving any of it is a curation project with no curator. So v6 ships
no `contributor` table at all, and nothing pretends otherwise.

The design is chosen so that adding the layer later is **purely additive**: two new
tables (`contributor`, `contributor_alias`), one nullable `contributor_id` plus match
metadata on `track_credit`, and not one existing row rewritten — `credit_name` stays
the immutable record either way. Lessons already learned, recorded for whoever builds
it: `contributor` uniqueness must be `(lower(name), disambiguation)` in the MusicBrainz
style, never bare `lower(name)`; aliases must not be globally unique (the same handle
can point at two people); and a `merged_into` self-reference makes merges reversible
without touching credit rows.

What works meanwhile, with nothing resolved and no identity anywhere:

- **Search works in full.** The engine consumes `credit_name` verbatim (§7), so
  free-text credit search — with Meilisearch's typo tolerance absorbing most of the
  romanization noise — is independent of identity work that may never happen.
- **Browse works by string identity.** Clicking a credit is
  `WHERE lower(credit_name) = lower(:name)` against `track_credit_name_idx` — a real,
  indexed browse page that is exactly as good as the data. `nomico`, `Nomico` and
  `ノミコ` stay three separate pages, which is honest rather than wrong.
- **Ingestion cannot be blocked by bad data.** Every scraped string is written as-is,
  split per role, ordered by `ordinal`. There is nothing to fail.

The revisit trigger, written down: build the identity layer when someone wants
contributor pages badly enough to curate identities — not before.

This replaces the raw `text[]` columns entirely — `credit_name` is a strictly better
raw record than the arrays were: ordered, per-role, and indexed.

---

## 6. Artwork

`Thumbnail` has five separate FK columns (`OriginalId`, `LargeId`, `MediumId`,
`SmallId`, `TinyId`), so adding a size is a migration, and every thumbnail read pulls
five joined `Asset` rows — all five are `AutoInclude()`d, feeding the multiple-collection
cartesian blow-up EF warns about at runtime (nothing configures `AsSplitQuery` today). `Album` additionally has *both* `Image`
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

Adding a 700px variant becomes an insert. `release.artwork_id` (§4) points here, and
variant selection is one indexed lookup.

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

Two things a parent-row watermark does *not* see, stated here so they're designed rather
than discovered. Edits to child rows (`track_credit`, `track_tag`) don't touch `track`,
so the triggers — or the writing repository, in the same transaction — must bump the
parent's `updated_at`, or exactly the corrections this column exists for will be missed.
And deletions leave no row to watermark: either remove documents from the index at
delete time in the repository, or accept that removals wait for the next full rebuild.

### Document shape

One document per track, fully denormalized — the engine should never need a join:

```
id, title (all four locales), release title, release date, catalog number,
circle names, credit_name strings (all roles),
original song + work titles, tag names, duration, has_lyrics
```

Note credits enter as the **raw `credit_name` strings** from §5 — with the identity
layer deferred there is nothing else they could enter as, and it keeps credit search
independent of identity work entirely.

### Settled: Meilisearch

164k documents is small for it; typo tolerance out of the box matters for romanized
Japanese, where users approximate spellings constantly; and it is one container against
Elasticsearch's JVM and cluster assumptions — concrete on a node already running
Keycloak, two Postgres instances and the API.

Because it is a projection and not a source of truth, its deployment is cheap to reason
about: one container, a modest volume, and losing it entirely costs a reindex rather
than data. Size the PVC for the index, not for durability, and do not back it up —
rebuild it.

The revisit condition, so it is written down rather than remembered: Elasticsearch earns
its operational weight if you later want real aggregation/analytics over the catalogue,
or kuromoji-grade Japanese analysis with hand-tuned dictionaries. Neither looks likely
for a browse UI.

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
often have I played this track" across playlists anyway — not that it answers anything
today: the only method that increments it is never called, so every stored value is
zero.

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
indexed aggregate, and `PlaylistType` loses `History` — and `Queue` too, see below. If
the aggregate ever gets hot,
add a `track_play_stat` rollup; at your scale it won't for a long while.

Playlists keep the constraints they're currently missing:

```sql
CREATE TYPE playlist_kind       AS ENUM ('normal', 'favorite');
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

-- One Favorite per user. This is the constraint whose absence lets the
-- check-then-insert bootstrap race into duplicates.
CREATE UNIQUE INDEX playlist_one_favorite_per_owner
    ON playlist (owner_id) WHERE kind = 'favorite';

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

Three deliberate changes. The PK is `(playlist_id, track_id)` rather than
`(track_id, playlist_id)` so the common "items of this playlist, in order" access is a
prefix scan — and it stays a *deduplicating* key on purpose: a playlist holding the same
track twice is disallowed today (the add path filters, the PK enforces), and that
remains the product behaviour for real playlists. Duplicates belong to the queue, which
is no longer a playlist at all — see below. And `NumberOfTracks` is gone. It's a
denormalized counter with no concurrency token that already drifts — a cascade-deleted
track removes items without touching it (not that anything reads it: no DTO carries
it). `COUNT(*)` against `playlist_item_position_unique` is cheap. If you want it back
for display, make it a trigger-maintained column, not application-maintained.

### The queue is not a playlist

`History` leaving `PlaylistType` for `play_event` was half of a cleanup; `Queue` is the
other half. A queue's invariants are the opposite of a playlist's on every axis that
matters: duplicates are natural (the same track queued twice is two entries, not an
error), there is exactly one per user, it is never public or shared or browsed, it
churns on every skip and "play next", and it wants a cursor. Keeping it as a
`playlist_kind` forces either the playlist constraints to be wrong for one kind or the
queue to be broken — the duplicate-track decision above is exactly that tension.

```sql
CREATE TABLE queue_item (
    id          bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    user_id     uuid        NOT NULL REFERENCES user_profile (id) ON DELETE CASCADE,
    position    integer     NOT NULL,
    track_id    uuid        NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    enqueued_at timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT queue_item_position_unique UNIQUE (user_id, position) DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT queue_item_position_positive CHECK (position >= 1)
);
```

Future metadata slots in here without polluting `playlist_item`: an enqueue `source`
(the `play_source` enum already exists), the originating context (which playlist, album
or similar-to anchor produced the entry), a played/consumed flag for resume. None of
those would ever belong on a playlist row.

Where the *cursor* lives — which entry is currently playing — is left open in §16:
a `user_profile` column if cross-device resume should survive a client wipe, or
client-side state if the server queue is just a sync target.

---

## 9. Embeddings and similarity

The pooled vectors in Postgres with HNSW are the right shape, and keeping the chunk
store on the filesystem is right. Two additions:

```sql
CREATE TABLE track_embedding (
    track_id           uuid          PRIMARY KEY REFERENCES track (id) ON DELETE CASCADE,
    embedding_mean     vector(1024)  NOT NULL,   -- 'mean' pooling
    embedding_meanmax  halfvec(2048) NOT NULL,   -- 'mean+max' pooling
    created_at         timestamptz   NOT NULL DEFAULT now()
);

CREATE INDEX track_embedding_mean_idx
    ON track_embedding USING hnsw (embedding_mean vector_cosine_ops);
CREATE INDEX track_embedding_meanmax_idx
    ON track_embedding USING hnsw (embedding_meanmax halfvec_cosine_ops);

-- Single-row provenance stamp, written in the same transaction as any embedding
-- or similar_track load. Cosine distance between different model versions is
-- meaningless; what prevents mixing vector spaces is this stamp plus the reload
-- discipline below -- not per-row version keys.
CREATE TABLE embedding_config (
    id          boolean     PRIMARY KEY DEFAULT true CHECK (id),  -- at most one row
    model       text        NOT NULL,  -- e.g. 'mert-v1-330m/win6s-hop4s/last4'
    loaded_at   timestamptz NOT NULL,
    track_count integer     NOT NULL
);
```

There is deliberately **no per-row `model_version`**. Exactly one model is ever live in
a given database, the operating mode is a full wipe-and-reload (each TLMC version is a
fresh catalogue with fresh ids, so embeddings are recomputed regardless), and a version
string wide enough to mean anything would sit in `similar_track`'s 16.4M-row primary
key — the same half-gigabyte text-key tax §1 rejects for ids. Dropping it also
dissolves the HNSW-with-filter problem outright: no version predicate means plain HNSW
indexes — nothing partial, no `hnsw.iterative_scan` tuning, no raw SQL on the vector
side of the migration.

What per-row versioning was protecting against is handled explicitly instead:

- **Mixed spaces / a half-loaded state.** A reload replaces `track_embedding`,
  `similar_track` and the `embedding_config` stamp in one transaction (staging tables
  and a swap, or `TRUNCATE` + `COPY`). A crashed load rolls back to the old state
  instead of leaving an unmarked mixture.
- **Rollback.** The previous run's shard CSVs stay on disk; reverting is a re-`COPY`
  measured in minutes, not a second live version.
- **"Which basis produced this?"** The API reads `embedding_config.model` and returns
  it on similarity responses — one canonical string written once by the loader from the
  run manifest, instead of a naming convention repeated 16.4M times.

On that canonical string: the pipeline's real parameters are a 6 s window with 2 s
*overlap* — a **4 s hop** — and the poolings on disk are named `mean` and `mean+max`
over the `last4` layer mix. An earlier draft's `'…/6s-2s/last4mean'` spelling encoded
the exact misreading the migration handoff flags as bug V4, and named one pooling when
the row carries both; the version string identifies model + chunking + layer mix, and
the columns carry the poolings.

The precomputed chamfer neighbours become a real table. The v5 numbers justify making
this the primary serving path: same-artist@10 of 26.2% against a 0.5% random baseline
(52×), same-album@10 of 6.9% against 0.02% (345×), and the chamfer rerank covered the
140,330-track v5 catalogue in 80 seconds at 1,744 anchors/s, on top of a ~6-minute
chunk-store build — an evening end to end, so re-running it per TLMC version is routine
rather than precious.

```sql
CREATE TABLE similar_track (
    anchor_track_id   uuid      NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    rank              smallint  NOT NULL,
    neighbor_track_id uuid      NOT NULL REFERENCES track (id) ON DELETE CASCADE,
    -- Symmetric chamfer similarity. Note the top of this distribution is
    -- compressed into roughly 0.986..0.994, and >= ~0.999 indicates a
    -- near-duplicate rip -- so the UI must rescale rather than show it raw.
    score             real      NOT NULL,

    PRIMARY KEY (anchor_track_id, rank),
    CONSTRAINT similar_track_no_self CHECK (anchor_track_id <> neighbor_track_id),
    CONSTRAINT similar_track_rank_positive CHECK (rank >= 1)
);

CREATE INDEX similar_track_neighbor_idx ON similar_track (neighbor_track_id);
```

### Settled: precomputed neighbours are the primary serving path

`GET /v1/tracks/{id}/similar` is a keyset read against `similar_track`'s primary key —
`WHERE anchor_track_id = :id ORDER BY rank` — which is an index scan returning rows
already in rank order. The diversity re-ranker then operates over
precomputed chamfer neighbours instead of ANN candidates, so over-fetching is just
reading further down the ranks rather than widening a vector search.

That makes similarity a first-class part of the API rather than a bolt-on, and it takes
the `ef_search` tuning from `8b97201` off the critical path entirely.

**The ANN tier does not go away — it becomes the fallback**, and it is not optional:

- Tracks ingested since the last precompute have no rows. New content is exactly what
  people look at, so this path will be exercised routinely, not rarely.
- Similarity from an arbitrary query vector (a seed upload, or an embedding computed on
  the fly) has no anchor to look up by definition.

So both paths stay live, and the API should say which one answered — a `source` field of
`precomputed` or `approximate` — because their quality is not the same and a client
showing "99% match" from the fallback is making a claim the pooled vectors do not
support.

Two further consequences worth designing for:

- **Return the model string** — read from `embedding_config`, not per row. Clients
  caching neighbour lists need to know when the basis changed, and it makes "why did my
  recommendations shift" answerable.
- **Switching models is a reload, not a flip.** New run → staging load → one-transaction
  swap that includes the `embedding_config` stamp. The previous run's shard CSVs on disk
  are the rollback path.

Coverage is worth exposing internally too — the count of tracks with no `similar_track`
rows is the metric that tells you the precompute has fallen behind ingestion.

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
| `Playlists` rows with `Type = Queue` | `queue_item` (§8) |
| `Playlists.NumberOfTracks` | `COUNT(*)`, or a trigger |
| `Tracks.Genre/Staff/Arrangement/Vocalist/Lyricist` | `track_credit.credit_name` (verbatim) + `track_tag` |

---

## 12. ETL implications

**One loader, not two.** Today the load path exists twice — tlmc-etl's
`Finalizer/PushToDb` and the backend's `EtlDataLoader`, which is where the functional
DASH/HLS path reconciliation actually lives. v6 keeps exactly one (`Finalizer/PushToDb`)
and the backend's copy retires along with the tables it existed to reconcile.

**`Finalizer/PushToDb`** — the bulk of the work.
- UUIDv7 lands in the Python aggregator (`id_assign_and_merge.py`), where the catalogue
  ids are actually minted; PushToDb parses what the intermediates hand it.
- Emit TypeIDs (`trk_…`, `rel_…`) in the JSON intermediates and log lines rather than
  bare uuids. The database still receives uuids; this is purely so a worklist, a
  journal entry or a failure message says what it is referring to. Given how much of
  this pipeline is debugged by reading intermediate files, this is probably where the
  prefixes earn their keep fastest.
- Write `release` + `disc` instead of the album/disc-0 pair.
- Stop emitting `HlsPlaylist`/`HlsSegment`; write `media_key` and `hls_bitrates` from
  the finalizer manifest.
- Write root-relative `storage_key` + `root` for assets, not absolute paths — and
  populate `mime` and `byte_size` at registration (both exist on the model today and
  are never set).
- Write credits verbatim into `track_credit.credit_name`, one row per role with
  `ordinal` preserving source order. There is nothing to resolve at load time (§5), so
  a load can never fail because a scraped name was ambiguous.
- **Media stops being a precondition for a row.** The loader currently skips an entire
  album when any one track is missing from the HLS manifest; in v6 that track row is
  written with `media_key` NULL. The v6 tree has 40 upstream-broken files, 105
  `.getxfer` partials and ~30 CJK-path publish failures — those tracks should exist,
  browse and search, just not play, and the API needs a defined shape for that state
  (`media_key: null`, no stream URLs). Same decision for the 101 legitimately
  zero-track albums: load them; a release with no playable media is still a catalogue
  fact.
- **Stop dropping collaboration circles.** The circle loader currently skips any circle
  with more than one `known_id`, so the 196 recorded collaborations get no row at all.
  `release_circle` with `ordinal` models multi-attribution fine; the loader just has to
  write it.
- Populate `track.duration` — it has never been written, even though the transcode
  stage already parses segment durations (and the startup ffprobe backfill is gone
  per §6).
- Push search documents to the engine after load (§7), and maintain `updated_at` so
  later runs can reindex incrementally instead of rebuilding all 164k documents.
- Also fix the two loader bugs already identified: clear the EF change tracker between
  batches (currently quadratic — as is the `Skip(i).Take(n)` batching itself), and
  assert vector length on load so a truncated `.bin` fails one row rather than a
  5,000-row batch mid-run with no resume path.

**`Postprocessor/HlsTranscode`** — no functional change, but `hls_finalizer.py`'s
manifest shrinks from per-segment to per-track: source path → track dir (the
`media_key`), rung list, `has_dash`. It cannot disappear entirely — the 204
collision-renamed directories (`stem [ext]`) mean the track dir is not derivable from
metadata alone, and the source-path key is exactly what PushToDb already joins on. The
layout convention becomes a contract shared with the backend, so it's worth writing it
down in one place both sides reference.

**`ExternalInfo/ThwikiInfoProvider`** — `commit_origina_album_and_track.py` now posts
uuids plus `external_key` rather than composite text ids (the discovery module of the
similar name only writes a CSV; it posts nothing). Its endpoints are `api/source/album`
and `api/source/album/{id}/track` — already `[InternalApiKey]`-gated server-side despite
the public-looking prefix — and the script currently sends **no** auth header, so the
`X-Internal-Api-Key` line (per `9fcc38c`) is needed here and in the other `PushChange`
scripts, none of which send it either. One gap on the receiving side:
`InternalController` throws `NotImplementedException` for track writes carrying
`Original` links, so the track↔original-song endpoint has to actually be implemented
before this pipeline can land. (Thwiki is also where the name variants live, if the §5
identity layer is ever built.)

**Lyrics push** — the stored `Lyrics` jsonb is pass-through (§15), so the payload casing
*is* the stored casing: the push script's document keys go snake_case with the rest of
the wire.

**New ETL step: embeddings + similarity.** A *re-run*, not a copy — the v5 shards are
keyed by v5 track ids and v6 mints fresh ones. MERT inference over the v6 catalogue,
`build_chunk_store.py`, `precompute_similar_tracks.py`, then `COPY` the new shard CSVs
(`anchor_id, neighbor_id, rank, score` — already the exact table shape) and stamp
`embedding_config`, all in one transaction. Two provenance notes: `make_embeddings.py`
should emit a manifest naming model/chunking/pooling next to the `.bin`s — today the
directory name is the only carrier, and that manifest is what fills
`embedding_config.model`. And the embeddings are computed from the HLS AAC rather than
the source FLAC; that's fine, but if the v6 ladder or encoder settings differ from
v5's, spot-check the eval numbers instead of assuming them.

---

## 13. Rollout order

1. Rewrite `Models/` and generate one initial migration. Keep migrations out of app
   startup — a Job or an explicit `dotnet ef database update`, with its own timeout
   (per `8b97201`). The ordinary b-tree indexes ride in this migration: at 164k rows
   they cost seconds, and splitting them out isn't worth fighting EF's snapshot over.
2. Load reference data (`original_work`, `original_song`, `circle` — including the 196
   collaborations the current loader drops).
3. Load catalogue (`release`, `disc`, `track`, `asset`, credits as verbatim
   `credit_name`; tracks without media load with `media_key` NULL).
4. Artwork variants, then `content_hash`.
5. Load `track_embedding` and `similar_track` and stamp `embedding_config` in one
   transaction. The only index builds worth special-casing live here: `similar_track`'s
   two indexes at 16.4M rows, and HNSW — if `COPY`-into-indexed measures slow, drop and
   rebuild them around the load; at 164k vectors either order is minutes, so measure
   once rather than architect around it.
6. Build the search index from the catalogue projection.

Steps 4 through 6 are independent of each other and of serving; the API is usable after
3, with artwork, similarity and search arriving as they complete.

---

## 14. Deliberately unchanged

`LocalizedField` as jsonb (right for storage; the generated `*_sort` columns fix the
query side). The `Lyrics` variant/line/ruby model — it's genuinely well designed for
this domain and I wouldn't touch it. Keycloak `sub` as `user_profile.id`. `Circle` and
its `CircleWebsite` child. The two-tier retrieval design. EF as the access layer.

---

## 15. Where the schema meets the API

This is not an API design document — envelopes, error shape, versioning and the
POST-for-a-read endpoints are a separate conversation. What belongs here is the places
where a schema decision above dictates something on the wire.

### The fourth casing surface

§1 counted three naming surfaces. There is a fourth: the JSON the API emits. Today it is
camelCase for properties and **PascalCase for enum values**, which is not even internally
consistent:

```json
{ "displayName": "…", "lastModified": "…", "visibility": "Private", "type": "Queue" }
```

`AddNewtonsoftJson()` applies a camelCase resolver by default, while the registered
`new StringEnumConverter()` has no naming strategy at all, so enum members serialize as
declared in C#.

Per §1 the wire goes snake_case with everything else:

```csharp
opt.SerializerSettings.ContractResolver = new DefaultContractResolver
{
    NamingStrategy = new SnakeCaseNamingStrategy()
};
opt.SerializerSettings.Converters.Add(
    new StringEnumConverter(new SnakeCaseNamingStrategy()));
```

giving `{"display_name": …, "visibility": "private", "type": "queue"}`. Note the enum
converter needs the strategy passed explicitly — the contract resolver does not reach
enum *values*, which is exactly how the current inconsistency arose.

snake_case JSON is well-precedented (Stripe, GitHub, Slack all do it), though camelCase
is more idiomatic for a TypeScript client, which will either access `track.release_date`
directly or map at its own boundary.

> **This is a breaking wire change.** v6 being a fresh deployment is what makes it
> affordable, but confirm nothing is pinned to the current camelCase spelling before
> committing — a frontend, a saved API client, anything generated from the existing
> OpenAPI document. This is the one decision in this proposal whose blast radius is
> outside these two repositories.

### Does the database's jsonb casing leak into the API?

Only if you pass the document through. There are two patterns and they answer the
question differently:

**Mapped** (what happens today): jsonb → POCO via Npgsql → DTO via AutoMapper → JSON via
Newtonsoft. Two independent serializers, so the stored keys and the wire keys are
decoupled and may differ freely. For `LocalizedField` they happen to coincide, because
every key is a single word (`default`, `en`, `zh`, `jp`); for a multi-word property they
would not (`reference_url` stored, `referenceUrl` on the wire), and that is harmless.

**Pass-through**: hand the stored jsonb to the client verbatim. Cheaper — no
deserialize-remap-reserialize per row — but the stored keys **become the public API
contract**.

The rule that follows: **jsonb you query into follows the SQL convention; jsonb you pass
through follows the API convention.**

Applied here:

- `LocalizedField` — **mapped**. Four small keys, and the schema queries into it for the
  generated `name_sort` column, so its keys should match the SQL convention.
- `Lyrics` — **pass-through** is worth it. Variants → lines → blocks → ruby is a deep
  document that the backend never inspects; it only relays it. Rebuilding four levels of
  POCO and re-serializing them on every request is pure overhead.

  With §1's decision this costs nothing extra: the stored keys are snake_case and so is
  the wire, so a document relayed verbatim is already in the right shape. Had the API
  stayed camelCase, `Lyrics` would have needed its jsonb spelled differently from every
  other table — a carve-out no API file mentions and someone would eventually "correct"
  into a contract break.

### Other consequences already implied above

- **Ids are strings on the wire** (§1) — route constraints, model binders and Swagger
  `MapType` all change.
- **Keyset pagination** on list endpoints means the response envelope carries a cursor
  rather than an offset, and `total` stops being free — make it a separate, cacheable
  call rather than computing it per page.
- **Credits are verbatim strings** (§5). The payload carries `name` and `role`; there
  is no `contributor` object until the identity layer exists, and when it does it
  arrives as a new optional field — clients that render `name` today are already
  forward-compatible with it.
- **Similarity scores need rescaling** (§9). Chamfer scores compress into roughly
  0.986–0.994, so `1 - distance` shown raw looks like everything is a 99% match. Rescale
  against the observed distribution before display.
- **Search returns ids plus highlight fragments**, hydrated from Postgres (§7). Letting
  Meilisearch return whole documents would make the API a thin proxy and let the search
  payload drift from the shape every other endpoint returns; hydrating costs a
  round-trip and keeps one rendering path. Highlights are the exception — they only
  exist in the engine, so they ride along with the ids.
- **Similarity is a first-class endpoint** (§9), and its response carries the model
  string from `embedding_config` plus a `source` of `precomputed` or `approximate`,
  because the fallback's quality is not the same and clients should not present it as
  though it were.

---

## 16. Open decisions

Settled:

| Decision | Outcome |
| --- | --- |
| Identifier scheme | TypeID over UUIDv7, stored as `uuid` (§1) |
| Casing | `snake_case` for SQL identifiers, jsonb keys and API JSON alike; C# stays PascalCase (§1, §15) |
| Ship DASH? | Yes, keep serving — but `DashPlaylists` still collapses to a column (§3) |
| Search implementation | External engine, not Postgres FTS (§7) |
| How far to normalize credits | Verbatim `credit_name` only; the identity layer is deferred and designed to be additive (§5) |
| Search engine | Meilisearch (§7) |
| Similarity serving path | Precomputed `similar_track` primary, ANN as required fallback; single live model, provenance in `embedding_config` (§9) |
| Queue storage | Its own `queue_item` table; `playlist_kind` loses `queue` and playlists stay deduplicating (§8) |
| `NumberOfTracks`-style counter | No — derive it; trigger-maintain only if a UI actually needs it (§8) |

(The earlier open question about auto-resolving `fuzzy` credit matches dissolved with
the identity layer — there is no resolution pass to configure.)

Still open:

| # | Decision | My recommendation |
| --- | --- | --- |
| 1 | Where does the queue cursor live? (§8) | A `user_profile` column if cross-device resume should survive a client wipe; otherwise client-side, with the server queue as a sync target |
| 2 | `disc.name` — plain `text`, or `LocalizedField` like every other display name? (§4) | Plain `text`; named discs are rare and rarely translated. Promote it later if that proves wrong |
