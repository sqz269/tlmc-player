# Subsonic-compatible API surface (proposal)

Status: **phase 1 implemented** (`TlmcPlayerBackend/Subsonic/`, sections 1–8 and
the section 13 phase-1 row); phases 2–3 remain proposed.

This proposes an optional OpenSubsonic-compatible facade under `/rest/*`, so that
existing Subsonic ecosystem clients (Symfonium, Kopuz, Castafiore, Tempus,
Amperfy, Aonsoku, …) can play the collection without any first-party client
work. The facade is a *projection* of the native API's data — it owns no tables
except API keys, and the first-party web/device clients keep using the native
`/api/*` surface, which remains the only place domain concepts (circles as
circles, original songs, ruby lyrics, similarity) are fully expressed.

Reference spec: <https://opensubsonic.netlify.app/docs/> — a backwards-compatible
superset of Subsonic 1.16.1. Capability discovery is per-endpoint and
per-extension, so the facade can be partial and honest about it: clients probe
`getOpenSubsonicExtensions` and handle error 30 (“not implemented”) elsewhere.

---

## 1. Shape of the divergence

What the Subsonic protocol assumes vs. what the v6 backend does, and how each
gap closes:

| Concern | Subsonic expects | v6 has | Resolution |
| --- | --- | --- | --- |
| Envelope | `subsonic-response` wrapper, XML default, camelCase JSON via `f=json` | bare DTOs, snake_case | own serializer for `/rest` only (§3) |
| Auth | `u`/`t`/`s` (md5 of recoverable password) | Keycloak OIDC bearer | OpenSubsonic `apiKeyAuthentication` (§4); legacy params answer 41/42 |
| IDs | opaque strings | `trk_…`/`rel_…`/`cir_…` TypeIDs | pass through verbatim; the prefix doubles as the type discriminator (§2) |
| Artist | first-class person/group entity | Circle only; per-person credits are verbatim strings | Artist ≡ Circle (§2); credits do not surface in phase 1 |
| Streaming | one file per song, byte-range seek | HLS/DASH manifests, no single-file endpoint | `stream` serves an AAC rung as progressive fMP4; lossless optional, off by default (§5) |
| Cover art | `getCoverArt?id=&size=` one hop | `artwork_id` → variants → `/api/asset/{id}` | facade resolves variants server-side (§6) |
| Search | `search3`, empty query = full sync | Meilisearch track index (SEARCH.md) | songs from Meili, artists/albums from Postgres, empty query from Postgres (§7) |
| Paging | `size`/`offset`, size ≤ 500 | keyset cursors, no totals | facade-only offset queries; `ORDER BY name_sort, id` is stable (§8) |

Nothing above requires touching the native API, the schema (beyond one API-key
table), or the ETL.

## 2. Entity and id mapping

TypeIDs are opaque strings to a Subsonic client, so native ids are used
unchanged. Every facade endpoint that receives an `id` dispatches on the prefix
(`cir` → artist, `rel` → album, `trk` → song), which keeps `getCoverArt` and
`stream` trivially type-safe (`Ids/EntityIds.cs`).

| Subsonic entity | v6 entity | Field notes |
| --- | --- | --- |
| `musicFolder` | — | single synthetic folder `1` / “TLMC” |
| `artist` | `Circle` | `name` = `name->>'default'`; `albumCount` from `release_circle`; no `artistImageUrl` (circles carry no artwork) |
| `album` (ID3) | `Release` | `artist`/`artistId` = ordinal-0 circle; other circles joined into `displayArtist` (OpenSubsonic field); `year` = `release_date` year; `songCount`/`duration` aggregated; `genre` omitted (phase 2 maps tags) |
| `song` | `Track` | `title` = `name->>'default'`; `track` = `track_number`; `discNumber` = disc's `disc_number` (Subsonic albums are flat — multi-disc releases flatten in disc/track order, which `ReleaseRepo.GetRelease` already produces); `parent`/`albumId` = release id; `coverArt` = release id; `suffix`/`contentType`/`bitRate` reflect what `stream` would serve (§5) |
| `playlist` | `Playlist` | native visibility maps to Subsonic `public`; the Favorite playlist is *hidden* from `getPlaylists` and surfaced as starred instead (§9) |

Localized names: the facade serves `default` only. Subsonic has no concept of a
localized name; picking per-request via `Accept-Language` would make ids appear
to rename between clients, which breaks client-side caches.

## 3. Envelope, formats, routing

A dedicated result type rather than MVC content negotiation, because `/rest`
must not inherit the global snake_case Newtonsoft contract (`Program.cs`) and
Subsonic XML is attribute-heavy:

- `Subsonic/SubsonicResult.cs` — takes a payload DTO + the request's `f` value.
  - `f=xml` (default): `XmlSerializer` over DTOs annotated with
    `[XmlAttribute]`/`[XmlElement]`, root `<subsonic-response xmlns="http://subsonic.org/restapi" status="ok" version="1.16.1" …>`.
  - `f=json` / `f=jsonp&callback=`: a private `JsonSerializer` with camelCase
    strategy, wrapped as `{"subsonic-response": {…}}`.
  - Every response carries the OpenSubsonic identification fields:
    `type: "tlmc"`, `serverVersion` (assembly informational version),
    `openSubsonic: true`.
- Errors are protocol-level, not HTTP-level: HTTP 200 with
  `status: "failed"` + `error {code, message}`. Codes used: 0 (generic),
  10 (missing parameter), 30 (endpoint not implemented), 40/42/44 (auth, §4),
  50 (not authorized), 70 (not found).
- Routes: one controller area `[Route("rest")]`, each method mapped twice —
  `getAlbum` and `getAlbum.view` — since pre-1.14 clients append `.view`.
  All endpoints accept GET; POST with form bodies is also accepted and the
  `formPost` extension advertised (it is free under MVC binding).

## 4. Authentication: API keys

Legacy Subsonic auth requires the server to recover the user's plaintext
password (`t = md5(password + salt)`), which is a non-starter next to Keycloak.
The facade therefore implements only the OpenSubsonic `apiKeyAuthentication`
extension:

- New table `api_key`: `id (pk)`, `user_id → user_profile`, `name`,
  `key_hash` (sha256), `key_prefix` (first 8 chars, for display), `created_at`,
  `last_used_at`. The key itself (`tlmc_<32 bytes base62>`) is shown once at
  creation.
- Native management endpoints (Keycloak-authorized, so the first-party clients
  can host a “connect an app” settings page):
  - `POST /api/user/api-keys` → creates, returns the key once
  - `GET /api/user/api-keys` → list (prefix, name, last used)
  - `DELETE /api/user/api-keys/{id}`
- `/rest/*` resolves `?apiKey=` → constant-time hash compare → `UserId`
  (same pattern as `Utils/Extensions/InternalApiKey.cs`). Requests carrying
  `t`/`s` or `p` answer error 42 (“auth mechanism not supported”) — except on
  an anonymous instance, below; a bad key answers 44; both `apiKey` and legacy
  params together answer 43.
- Config `Subsonic:AllowAnonymous` (default `false`): when set, requests with
  no credentials get the read-only surface (browse, stream, cover art, search
  — all of which are anonymous on the native API anyway), and user-scoped
  endpoints answer 50. This matches the native API's authenticated-vs-anonymous
  posture and lets a public instance serve *sonic clients without accounts.
  Because practically every client — including all hosted web clients — can
  only log in with `u`/`t`/`s`, an anonymous instance also *accepts* legacy
  credentials (any username, any password) and grants them exactly the
  anonymous surface: there is no identity behind them, so user-scoped
  endpoints still answer 50. A keyed (non-anonymous) instance refuses legacy
  credentials with 42 as above.

`ping` authenticates like everything else — clients use it to validate
credentials at setup time.

## 5. Streaming and download

A Subsonic client cannot consume the HLS/DASH manifests: `stream` returns
audio bytes, not a playlist handoff, and no *sonic client implements manifest
playback for music. Serving "HLS instead of FLAC" is nevertheless possible
because of how the rungs are stored — each `<media_key>/hls/<rung>/stream.m4s`
is a single self-contained fMP4 with the init segment at byte 0, i.e. a valid
progressive `audio/mp4` stream with known length, so byte-range seeking, exact
`Content-Length` and the usual caching validators all work when the file is
served as-is. **The facade therefore streams only the pre-transcoded AAC rungs
and never touches the source FLAC by default.**

Selection logic (`stream?id=trk_…&maxBitRate=&format=`):

1. No `maxBitRate` (or `0`): the highest rung in `track.hls_bitrates`
   (normally 320).
2. `maxBitRate` set: the highest rung ≤ the value, floored at the lowest rung.
3. `format` is a preference, not a contract (per spec): every value (`raw`,
   `aac`, `mp3`, `flac`, …) gets the rung selected above — no request-time
   transcoding exists, and a rung file served unmodified is `raw` in the sense
   that matters.
4. `media_key IS NULL` answers 70.

Song DTOs advertise what `stream` will serve: `suffix: "m4a"`,
`contentType: "audio/mp4"`, `bitRate` = the default rung. Serving mechanics
are identical to `MediaController.GetAsset` (`StorageRootResolver` +
`PhysicalFile(..., enableRangeProcessing: true)`).

**Compatibility caveat — now load-bearing:** fragmented MP4 over progressive
HTTP plays fine in ExoPlayer, mpv and browsers, but with AAC as the *only*
path a strict demuxer somewhere in the ecosystem has no fallback. The
hardening step is therefore pre-planned rather than hypothetical: a lazy
`ffmpeg -c copy` remux to a moov-up-front `.m4a`, cached under the `Generated`
storage root keyed by track + rung (FFMpegCore is already a dependency; a
stream copy is I/O-bound and cheap). If the client smoke pass (§13) shows any
target client failing on fMP4, the remux ships in phase 1 instead of phase 3.

`download?id=trk_…` serves the same highest-rung file with a
`Content-Disposition` filename from the track title (`.m4a`). Album downloads
(`id=rel_…`) answer 30 in phase 1.

**Lossless stays first-party-only.** Withholding the source FLAC caps
per-stream egress at 320 kb/s (vs. roughly 1 Mb/s for FLAC through the
Cloudflare tunnel), keeps the lossless masters off the third-party surface
entirely, and makes the facade stream byte-identical content to what the
first-party HLS/DASH clients play. A deployment that *wants* to hand lossless
to Subsonic clients can set `Subsonic:ServeLossless` (default `false`), which
switches case 1 and `format` ∈ {`raw`, `flac`} to the source FLAC via
`track.source_asset_id`, with `download` following suit.

No `timeOffset` support (ranges make it unnecessary for audio) and no
`transcodeOffset` extension.

## 6. Cover art

`getCoverArt?id=&size=` accepts `rel_…` (and `trk_…`, resolved to its release).
The facade picks from `artwork_variant` server-side: smallest variant with
`size_px ≥ size`, else the original (`size_px = 0`), then serves the asset file
exactly like `/api/asset/{id}` — ETag included, since clients cache cover art
aggressively. No image resizing at request time; the variant ladder already
exists for exactly this.

## 7. Search

`search3?query=&artistCount=&artistOffset=&albumCount=&albumOffset=&songCount=&songOffset=`

- **Songs**: Meilisearch, exactly as `SearchController` does — query, then
  hydrate ids via `TrackRepo.GetWithContext`. The index is track-only
  (SEARCH.md), which is the hard part of search; reuse it as-is.
- **Artists / albums**: Postgres `ILIKE` over `circle.name->>'default'` +
  aliases, and `release.name->>'default'`, ordered by `name_sort`, offset
  paged. At catalogue scale (thousands of circles, tens of thousands of
  releases) this needs no engine; a `pg_trgm` index is the escalation path if
  it ever does.
- **Empty query** is a protocol idiom, not a degenerate case: clients
  (notably Symfonium and other offline-sync clients) call `search3` with
  `query=""` to enumerate the whole library. Serve it from Postgres with
  deterministic `ORDER BY name_sort, id` offset paging — never from Meili,
  whose index is a disposable projection.
- `search2` (folder-flavored) aliases to the same implementation.
- If Meili is down, songs degrade to the same `ILIKE` path over
  `track.name_sort` rather than answering 503 — Subsonic clients treat a
  failed search as “no results”, and a browse-only degradation is the behavior
  SEARCH.md already promises for the native surface.

## 8. Facade query layer

Subsonic paging is `size`/`offset` and several of its list shapes (artist
index, album lists by type) have no native endpoint. Rather than contorting the
cursor-based repos, the facade gets one read-only query class
(`Subsonic/SubsonicQueries.cs`, `AppDbContext` direct, mirroring how
`MediaController` reads), providing:

- `getArtists`: all circles grouped by first character of `name_sort`
  (non-Latin initials group under `#`), with album counts — one query, cached
  per instance for a few minutes if it shows up in profiles.
- `getAlbumList2?type=`:
  | type | source |
  | --- | --- |
  | `alphabeticalByName` | `ORDER BY name_sort, id` offset page |
  | `newest` | `ORDER BY release_date DESC NULLS LAST, id DESC` (matches `ReleaseSort.Date`) |
  | `byYear` (`fromYear`/`toYear`) | `release_date` range |
  | `random` | seeded sample, same technique as `TrackRepo.GetRandom` |
  | `recent`, `frequent` | phase 3: aggregates over `play_event` |
  | `byGenre` | phase 2: releases having tracks with the tag |
- `getRandomSongs` → `TrackRepo.GetRandom` verbatim.

Sort keys use existing indexes (`name_sort` is a generated, indexed column);
`OFFSET` at ≤ 500-row pages over these orderings is fine at this scale.

## 9. User-scoped endpoints

| Subsonic | Native mapping |
| --- | --- |
| `getPlaylists` / `getPlaylist` | `PlaylistRepo`, excluding the Favorite-kind playlist |
| `createPlaylist` / `updatePlaylist` / `deletePlaylist` | existing create/rename/add/remove/move operations; `updatePlaylist`'s index-based `songIndexToRemove` translates to track ids via the current item list |
| `star` / `unstar` (`id=trk_…`) | add/remove on the Favorite playlist (the native favorite model). Album/artist starring answers 30 — there is nothing to attach it to |
| `getStarred2` | Favorite playlist items as starred songs |
| `scrobble?id=&submission=` | `submission=true` → `PlayEventRepo` insert with `source = unknown` and `ms_played` = track duration (the protocol carries no played-time); `submission=false` (now-playing) is a no-op success |
| `getLyricsBySongId` (songLyrics ext.) | project the native lyrics document (`TrackRepo.GetLyrics`): pick the default variant, one `line` per native line with its timestamp, ruby annotations dropped, one `structuredLyrics` entry per language block. The lossy projection is acceptable here — full fidelity stays native-only |

`getUser` returns a synthetic read-only user (no admin/settings roles);
`changePassword`/user management answer 30 — identity lives in Keycloak.

## 10. Similarity and discovery

The embedding stack (MERT track embeddings, precomputed chamfer neighbors in
`similar_track`, pgvector ANN fallback, `Utils/DiversityReranker.cs`) backs the
protocol's discovery endpoints with actual audio similarity, where the
ecosystem convention is Last.fm genre adjacency — most servers fake these
endpoints; this one doesn't have to.

| Subsonic | Mapping |
| --- | --- |
| `getSimilarSongs2?id=trk_…&count=` | `SimilarityRepo` verbatim: precomputed neighbors in rank order, ANN fallback for tracks without rows. Diversification **on**: clients call this to build instant mixes and radio, which is exactly what the reranker's same-release/same-circle penalty exists for |
| `getSimilarSongs2?id=rel_…` | union of the release's tracks' neighbor lists, best score first, minus the release's own tracks, then diversified |
| `getSimilarSongs2?id=cir_…` | same, anchored on a sample of the circle's tracks (cap the anchor set, ~50), minus the circle's own tracks |
| `getSimilarSongs` | alias of the above (folder/ID3 distinction doesn't affect the result shape) |
| `getArtistInfo2` → `similarArtist` | derived from the neighbor graph without circle-level embeddings: the anchors' neighbors grouped by *their* circle, weighted by score and rank, top N. One aggregate query over `similar_track`; materialize offline if profiles say so |
| `getTopSongs?artist=` | not similarity but adjacent: `play_event` counts per track within the named circle, disc/track order as the cold-start fallback |

Two impedance mismatches dissolve at the protocol boundary rather than
needing translation:

- Subsonic responses carry **no similarity scores** — an ordered song list is
  the whole contract. The compressed 0.986–0.994 score range the native UI
  must rescale (SCHEMA-V6.md) simply does not cross; only rank order survives.
- The native `source: precomputed|approximate` and `model` provenance fields
  have no protocol slot either. Clients cannot tell the ANN fallback from the
  precomputed graph, so index staleness degrades quality, never correctness.

`count` clamps to the native similarity limit. Tracks, releases or circles
with no embedded media answer an empty list, not an error — clients treat
empty similar-song lists as routine.

## 11. Explicitly out of scope

Podcasts, internet radio, jukebox, shares, chat, bookmarks, video (`getVideos`,
`hls.m3u8` — ironically), ratings (`setRating` answers 30; only the boolean
star maps to anything real), `getArtistInfo2` biography text (the
`similarArtist` part is covered in §10; the biography could later serve circle
websites/aliases), music folder browsing by *actual* folders
(`getIndexes`/`getMusicDirectory` are implemented as thin aliases of the ID3
views in phase 3 only if a wanted client requires them — modern clients browse
by ID3).

## 12. Project layout, config, deployment

```
TlmcPlayerBackend/Subsonic/
  SubsonicController.cs      one controller, [Route("rest")], thin methods
  SubsonicResult.cs          envelope + XML/JSON/JSONP serialization
  SubsonicAuth.cs            apiKey resolution filter → SubsonicUser
  SubsonicQueries.cs         offset-paged read queries (§8)
  SubsonicMapper.cs          v6 DTOs/rows → Subsonic DTOs
  Dtos/                      XmlSerializer-annotated response types
Models/UserProfile/ApiKey.cs + migration
Controllers/UserProfile/ApiKeyController.cs   native key management (§4)
```

- `Subsonic:Enabled` (default **false**) gates controller registration, same
  pattern as `Search:Enabled` — the facade is a feature, not a default.
- Ingress: route `/rest` to the backend service (`K8s/ingress.yaml`), plus the
  Cloudflare tunnel config. CORS is already permissive globally.
- Swagger: `/rest` is excluded from the v1 OpenAPI document — the contract is
  the Subsonic spec, and generated clients must not bind to it — and published
  instead as a separate browsing-aid document at `/swagger/subsonic/swagger.json`
  (`SubsonicSwagger.cs`: one GET operation per method, no `.view` aliases, no
  Bearer requirement, deliberately schemaless responses).

## 13. Phasing and estimate

| Phase | Endpoints | Outcome | Estimate |
| --- | --- | --- | --- |
| 1 — browse & play | `ping`, `getLicense`, `getOpenSubsonicExtensions`, `getMusicFolders`, `getArtists`, `getArtist`, `getAlbum`, `getSong`, `getAlbumList2` (4 types), `getRandomSongs`, `getCoverArt`, `stream`, `download`, `search3`/`search2` + API-key table & native management endpoints | any Subsonic client browses, searches, and plays | ~3–4 focused days, half of which is envelope/XML plumbing and the auth filter |
| 2 — user features | playlist CRUD, `star`/`unstar`/`getStarred2`, `scrobble`, `getLyricsBySongId`, `getSimilarSongs2` for track ids (a straight `SimilarityRepo` call), `getGenres`/`getSongsByGenre` (tags) | daily-driver parity incl. instant mix | ~2–3 days |
| 3 — polish | `getSimilarSongs2` release/circle anchors, `similarArtist` in `getArtistInfo2`, `getTopSongs`, `recent`/`frequent` album lists, `getIndexes`/`getMusicDirectory` aliases, fMP4→m4a remux cache if needed | discovery features, legacy-client coverage | as needed |

Verification: golden-file tests for the envelope (one XML + one JSON per
endpoint shape — the wrapper, not the data, is where Subsonic compatibility
bugs live), plus a smoke pass with three real clients spanning the ecosystem's
temperament: Symfonium (strict, ID3, offline sync via empty `search3`),
Aonsoku (web, lenient), and Tempus or Amperfy (mobile, legacy-leaning).
Navidrome's implementation is the de-facto reference when the spec is
ambiguous.
