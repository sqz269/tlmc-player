# Search integration (Meilisearch)

Implements SCHEMA-V6.md §7. This file is the operational contract: what the index
contains, who writes it, and what to do when it is wrong or gone. The design
rationale (why Meilisearch, why not Postgres FTS, why the CJK settings exist)
lives in the schema doc and is not repeated here.

## Shape of the integration

```
Postgres (source of truth)
   │
   │  TrackSearchProjection — the ONE projection function
   ▼
Meilisearch index `tracks`          one document per track, fully denormalized
   ▲                    │
   │ writes             │  ids + highlight fragments
   │                    ▼
   backend        GET /api/search — hydrates ids from Postgres
```

Everything that writes documents goes through
`TlmcPlayerBackend/Search/TrackSearchProjection.cs`. The ETL does **not** build
documents itself — after a load it calls the internal reindex endpoint, so the
document shape cannot drift between writers.

| Component | File |
| --- | --- |
| Document shape + index settings contract | `Search/TrackSearchDocument.cs` |
| Projection (rows → documents) | `Search/TrackSearchProjection.cs` |
| Minimal REST client | `Search/MeiliClient.cs` |
| Rebuild / incremental / push / query | `Search/SearchIndexService.cs` |
| Settings applied at startup | `Search/SearchIndexBootstrap.cs` |
| Public endpoint | `Controllers/MusicData/SearchController.cs` |
| Reindex endpoint | `Controllers/MusicData/InternalController.cs` |

Why a hand-rolled client: the official .NET SDK (0.20.0) exposes neither the
`localizedAttributes` index setting nor the `locales` search parameter — the two
features §7 calls load-bearing for CJK correctness. Five stable REST endpoints
did not justify a dependency that cannot express the setting the design hinges on.

## The document

One per track, id is the TypeID (`trk_…`). Localized text is split into suffixed
fields (`title_default`, `title_en`, `title_zh`, `title_jp`) because the locale
declarations match on attribute patterns — the suffix is what makes `*_jp → jpn`
expressible. Field list and relevance order: `SearchIndexContract` in
`Search/TrackSearchDocument.cs`.

The CJK declarations (the part that must never be "simplified away"):

| Pattern | Locale | Why |
| --- | --- | --- |
| `*_jp`, `*_default` | `jpn` | default spellings are the original, overwhelmingly Japanese, titles; pure-kanji ones are otherwise detected as Mandarin and segmented with jieba |
| `*_zh` | `cmn` | |
| `*_en` | `eng` | |
| `circle_names`, `credits`, `tags`, `catalog_number` | `jpn`, `eng` | mixed romanized/Japanese; constraining to these two makes Mandarin unguessable while detection still separates the scripts that actually occur |

Verify on real data with `Tools/meili-cjk-probe/probe.py` (values for `--url` /
`--key` are in `K8s/local/.generated-credentials` after a deploy). It indexes
pure-kanji titles with and without the declarations and reports recall for both;
run it against any new Meilisearch version before upgrading the pin.

## Who writes when

| Writer | Path | Granularity |
| --- | --- | --- |
| ETL, after a catalogue load | `POST /api/internal/search/reindex` (`X-Internal-Api-Key`) | full, or `?since=<load start>` |
| Backend, on internal single-track writes (lyrics, originals) | inline push | one document, best-effort |
| Startup | settings only | — |

Reindex semantics:

- **No `since`**: full rebuild into `tracks_rebuild`, atomic swap, staging index
  deleted. Deleted tracks cannot survive it; searches keep answering throughout.
- **`since=<timestamp>`**: upserts tracks with `updated_at > since` straight into
  the live index. Deletions are *not* seen (no row left to watermark — §7);
  removal waits for the next full rebuild, which is acceptable because nothing
  deletes tracks outside a full reload today.
- The endpoint is synchronous on purpose — the ETL wants to know the index is
  consistent before declaring a load done. Full rebuild of 164k tracks is
  minutes; set the client timeout accordingly.

The watermark is honest because every write path that changes document content
bumps `track.updated_at` in the same transaction — including the child-row cases
the column cannot see by itself: lyrics and original-links writes bump it in
`InternalController`/`OriginalRepo`, and original work/song *title* edits bump
every arranging track (`OriginalRepo.UpsertWork/UpsertSong`). Circle upserts
deliberately do not: they match on name, and nothing else they change is in the
document.

Inline pushes are best-effort by design — the mutation has committed and must not
fail because the engine is down. A missed push is repaired by the next
incremental reindex, precisely because the bump above already happened.

## Failure modes

- **Engine down / not configured** → `GET /api/search` answers 503 with a
  problem body. Browse, playback and playlists never touch the engine.
- **Engine data lost** → not an incident. Redeploy, then
  `POST /api/internal/search/reindex`. The PVC is sized for the index and is
  deliberately not backed up.
- **Settings drift** (index recreated by hand, engine upgraded) → restart the
  backend (bootstrap reapplies settings) or run a reindex, which also reapplies
  them.

## Query surface

`GET /api/search?q=…&limit=20&offset=0[&locales=jpn]`

- `locales` (repeatable; `jpn`/`eng`/`cmn`) is the query-side half of the CJK
  fix: it overrides detection, which matters for short pure-kanji queries. Pass
  it when the client knows the script (IME state, UI language); omit otherwise.
- Response items are the standard track-with-context shape hydrated from
  Postgres, plus `highlights`: document-field → `<em>`-marked fragment(s), only
  for fields the query matched. `estimated_total_hits` is the engine's estimate,
  not an exact count.

## Deployment

- Local cluster: `K8s/local/15-meilisearch.yaml` (pinned `getmeili/meilisearch:v1.51`
  — `localizedAttributes` needs ≥ 1.10, and index format is not
  forward-compatible across minors, so bumping the pin means a rebuild).
  `deploy.sh` generates the `meilisearch-credentials` secret and hands the master
  key to the backend; NodePort 30700 exists for the probe and inspection, gated
  by the same key. The master key doubles as the backend's API key because the
  backend is the engine's only client — switch to derived keys if that changes.
- Compose (dev): `meilisearch` service in dev mode, keyless, dashboard on :7700.
- The backend with no `Meilisearch__Url` runs fine minus search — the option is
  the feature flag.
