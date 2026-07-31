# Recommendation & feedback-loop machinery (proposal)

Status: **proposed**. Nothing in this document is implemented; it plans the
work that begins where the Subsonic phase-2 surface (scrobble → `play_event`,
per-user `recent`/`frequent`, OIDC → API keys) left off.

This proposes personal recommendation surfaces and the feedback machinery that
makes them measurable. It is written for *this* deployment's shape: a
single-digit number of real users, an anonymous public surface that must stay
untouched, and — unusually — a dense content signal for every one of ~164k
tracks. That shape inverts the industry playbook, and the design leans into
the inversion rather than fighting it.

---

## 1. Assets and the inversion

What already exists, and what each contributes:

| Asset | Where | Role here |
| --- | --- | --- |
| Pooled MERT embeddings, `vector(1024)` per track | `track_embedding.embedding_mean` (pgvector) | the content signal every candidate generator queries |
| Precomputed similarity + ANN fallback + `DiversityReranker` | `similar_track` / `similar_release` / `similar_circle`, `SimilarityRepo` | neighbor lookups and the diversity pass are already built |
| 48 named sound families | `track_map_cluster`, k-means over PCA features | the interpretable axis: affinity histograms, bandit arms, mix seeds |
| 2-D library map | `track_map`, the explore page | geometry for exploration: "territory" is literal |
| Arrangement lineage | `track_original_song` ↔ `original_song` | an item-item graph unique to this corpus (same tune, different sound) |
| Credits | `track_credit` (`credit_role`: arranger, vocalist, …) | person-level following |
| Play events with identity | `play_event` (user, track, played_at, source, ms_played) | the behavioral signal — as of scrobble, it finally accumulates |
| Identity + per-device keys | Keycloak OIDC → `api_key` | all personal surfaces hang off this; anonymous stays anonymous |

The inversion: collaborative filtering — the default machinery everywhere
else — needs a user base we will never have. But CF exists to *approximate*
item similarity from behavior because most services cannot compute it from
content. We can. So behavior data is not the similarity source here; it is
the **preference and feedback** source, and the embeddings carry the rest.
Every design below follows from that: content-first candidates, behavior as
weighting, per-session adaptation over trained models, everything
interpretable and measurable at a scale of tens of events, not millions.

## 2. Design principles

1. **Content generates, behavior ranks.** Candidates come from embedding
   space; play history decides which direction of embedding space matters.
2. **Interpretable over learned.** Centroids, histograms, Rocchio updates and
   Beta counters — things one user's data can actually move, and that can be
   printed and understood when they misbehave. No trained rankers.
3. **Attribution before intelligence.** No surface ships without its
   impressions being logged; an unmeasurable recommender is a vibes generator.
4. **The anonymous surface is frozen.** Every personal feature keys off the
   resolved `UserId`; signed-out behavior — including the facade's answers —
   does not change.
5. **Native API for rich signal, Subsonic for reach.** Third-party clients
   stay binary scrobblers; the first-party fork reports the rich version
   through `/api/*`. The facade is never extended beyond the protocol.

## 3. Phase 0 — signal plumbing (the foundation everything joins against)

The current signal is "played, at some time, source unknown". Three changes:

**3a. Rich play reporting from the fork.** When signed in, Aonsoku reports
plays natively — `POST /api/user/history` already exists and accepts
`ms_played` and `source` — instead of calling `/rest/scrobble`. The player
already knows real listened time (it drives the seekbar); report it on
track-change and on unload. Signed-out or third-party clients keep the
Subsonic path unchanged. One client reports through exactly one path, so no
dedup heuristics are needed.

**3b. Source vocabulary.** `play_source` grows values: `radio`, `recommended`,
`map`, `queue`. (Postgres enum: `ALTER TYPE ... ADD VALUE` migrations — they
are append-only, which is fine; the enum is not reordered.) The fork tags
every playback origin: album page → `album`, similar-radio → `radio`, home
row → `recommended`, map click/lasso → `map`, search result → `search`.

**3c. Impression log.** New table:

```
rec_impression
  id          bigint identity pk
  user_id     uuid → user_profile
  surface     text          -- 'home.because', 'home.rediscover', 'radio', 'mix.family', 'map.frontier'
  track_id    uuid → track
  context     jsonb         -- surface-specific: anchor id, session id, family, batch
  served_at   timestamptz
```

Attribution rule (a view, not a table): an impression converts if the same
`(user, track)` has a `play_event` within 48h of `served_at`; it completes if
that event's `ms_played / duration ≥ 0.8`. Skip = a matching event with
ratio `< 0.25`. Those two thresholds are constants in one place; everything
downstream (bandits, evaluation) reads the view.

Verification: play a track from each surface in the fork, confirm one
`play_event` row each with correct source and plausible `ms_played`, and that
the attribution view joins them to their impressions.

## 4. Phase 1 — taste model and personalized home rows

**Taste centroids.** Per user: take the last ~2,000 play events, weight by
completion ratio and a 60-day half-life decay, k-means (k ≤ 3, chosen by
silhouette) over the corresponding `embedding_mean` vectors, l2-normalize the
centroids. Listeners are multimodal — one centroid averages denpa and piano
into a meaningless midpoint; three keep the modes separate. Computed
on-demand with a short cache (a few hundred vector fetches per user is
nothing at this scale); no nightly job until one is needed.

**Family affinity histogram.** One SQL aggregate: play-weighted counts per
`track_map.cluster`, normalized. Serves as the interpretable profile
("40% eurobeat, 20% vocal rock"), the bandit prior (§6), and a UI element.

**Home rows**, served by a native `GET /api/user/recommendations/home`:

| Row | Generator |
| --- | --- |
| Because you played *X* | anchor = recent high-completion play, sampled; candidates from `similar_track` precomputed ranks; exclude recently played |
| Rediscover | high historical affinity (many completions), no play in 90+ days |
| More arrangements of tunes you love | arrangement lineage: original songs behind the user's top completions → unheard arrangements, ordered by embedding distance to the loved one |
| New in your territory | tracks added recently whose embedding is within distance θ of any taste centroid |
| Your *family* mix (per top-2 affinity families) | within-family kNN around the nearest taste centroid |

Every row draws through the existing `DiversityReranker` (same-release /
same-circle penalties) and logs impressions with its surface tag. The
arrangement-lineage row is the one no other music service can build; it goes
in phase 1 because it is also the cheapest — two joins and one distance sort.

The fork renders these as home sections when signed in (native client, same
pattern as the map page). Subsonic clients see none of this — deliberately.

## 5. Phase 2 — session-adaptive radio (Rocchio relevance feedback)

The similar-radio the explore page already has is stateless: the anchor never
moves. Classical relevance feedback fixes that with arithmetic, not training:

```
q ← α·q₀ + β·mean(embeddings of completed this session) − γ·mean(embeddings of skipped this session)
    α=0.6  β=0.3  γ=0.1   (l2-normalize after)
```

Endpoint: `GET /api/music/radio/next` — **stateless**. The client sends the
anchor plus this session's completed and skipped track ids (bounded lists);
the server computes `q`, runs the ANN query, excludes the session's played
set, diversity-reranks, returns the next batch. Statelessness keeps the
server horizontal and makes the algorithm trivially replayable in tests:
same inputs, same playlist.

The fork's radio player keeps the two lists, calls for the next batch every
N tracks or after a skip burst, and tags plays `source=radio`. The audible
effect — skip two vocal tracks and the stream drifts instrumental — is the
first place the feedback loop becomes *perceptible*, which matters for trust
in everything else.

## 6. Phase 3 — Thompson-sampled daily mixes

Explore/exploit over the 48 families, per user:

- Arm state: `Beta(1 + completions_f, 1 + skips_f)` per family `f`, computed
  from the attribution view (nothing stored); the affinity histogram seeds
  pseudo-counts so a new signer-in starts from taste, not uniform.
- Mix generation (on demand, or when the home row is first requested each
  day): for each of ~30 slots, sample every arm, take the argmax family,
  draw a candidate near the taste centroid within that family, novelty-filter
  (no plays in 30 days), diversity-rerank the result.
- Materialized as a real playlist ("Daily Mix — 2026-08-01") through the
  existing playlist machinery, replaced on regeneration. Playlists survive in
  every client — including Subsonic ones, which quietly extends one personal
  surface to Amperfy/Symfonium without touching the protocol.

Thompson sampling is the right bandit here because it behaves sanely with
tens of observations, degrades to the prior gracefully, and its state is two
integers per family — printable, debuggable, resettable.

## 7. Phase 4 — map-native discovery

- **Your territory**: the map page overlays a personal KDE contour — the
  circle-contour estimator (`map-contour.ts`) run over the user's played
  track ids, in a reserved slot color. Zero backend work: history ids come
  from the existing endpoint, coordinates are already client-side.
- **Frontier tour**: grid cells adjacent to the user's territory contour with
  zero personal plays, ranked by library density; pick each cell's most
  central track; serve as a queue ("tour the border of your taste"). Plays
  tag `source=map`, so the tour's completion rate is measurable like any row.

## 8. Phase 5 — measurement, or the loop actually closing

- Per-surface funnel from the attribution view: impressions → plays →
  completions, weekly, per user. A dashboard is optional; a SQL view is not.
- Offline harness in the ETL repo: leave-last-N-out replay — hide each user's
  last N completions, ask each generator for candidates, measure recall@k
  against the hidden set, with plain `similar_track`-from-last-anchor as the
  baseline every phase must beat to justify its complexity.
- Kill criteria, stated up front: a surface that converts worse than the
  baseline for four consecutive weeks gets removed or reworked, not tuned in
  place forever. Small instances accumulate dead features faster than data.

## 9. Schema and surface summary

New: `rec_impression` (+ attribution view), `play_source` enum values,
`/api/user/recommendations/home`, `/api/music/radio/next`, optional
`/api/user/recommendations/frontier`. Derived-not-stored: taste centroids
(cached), affinity histograms, bandit counters. Untouched: everything
anonymous, the whole `/rest` facade except that daily-mix playlists appear in
`getPlaylists` naturally once playlist CRUD lands there.

## 10. Open decisions

1. **Playlist CRUD on the facade** predates phase 3's mixes reaching
   Subsonic clients — sequence it before or accept native-only mixes at first.
2. **Decay/threshold constants** (60-day half-life, 0.8/0.25 completion
   bands, Rocchio α/β/γ) are opinions until phase 5 data exists; they live as
   named constants with the expectation of revision.
3. **Multi-user privacy defaults**: territory overlays and mixes are
   per-user and private; whether any *global* taste surface (e.g. "popular on
   this server") ever shows to anonymous visitors stays a deliberate opt-in.
4. **Skip semantics on seek-heavy listening** (previewing a release by
   skimming) can look like mass skips; the 0.25 threshold plus a per-session
   skip-burst guard in the radio endpoint is the first answer, revisited with
   data.

## 11. Order and effort

| Phase | Contents | Effort | Ships value alone? |
| --- | --- | --- | --- |
| 0 — plumbing | native rich reporting from the fork, enum values, `rec_impression` + view | ~1 day | invisible but everything joins against it |
| 1 — taste + home | centroids, histogram, five rows, fork home sections | ~2–3 days | yes — the first personal morning-open moment |
| 2 — Rocchio radio | stateless `/radio/next`, fork wiring | ~1–2 days | yes — the loop becomes audible |
| 3 — Thompson mixes | bandit view, generator, playlist materialization | ~1–2 days | yes — and reaches Subsonic clients via playlists |
| 4 — map discovery | territory overlay, frontier tour | ~1–2 days | yes — the map becomes personal |
| 5 — measurement | funnel view, offline harness, kill criteria | ~1 day | it's what makes 1–4 honest |

Phases are independent after 0; the recommended order is as numbered — each
one's output is the next one's best debugging tool.
