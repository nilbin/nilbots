# Game-mode and competition architecture

Status: **active implementation plan; not shipped**, 2026-07-27.

This plan generalizes the experimental actor-match path without changing the
official rules 0.1–0.5 product or rewriting the opened `frontline-alpha-1`
evidence. It is the code-facing architecture for parallel modes, variable
participant/team topology, data-driven forms and transitions, and isolated
ranked ladders.

The objective is not to make arbitrary game design expressible as data. The
objective is narrower and safer:

> Existing typed mechanics are tunable through immutable data. A genuinely
> new semantic adds one closed typed capability without changing the common
> bot, replay, result, or competition envelopes.

Implementation checkpoint:

- Package A compatibility shields are complete for the checked-in official
  replay-v1 fixture, every official rules/map/match fingerprint, the
  Frontline-alpha fingerprints, and a real end-to-end alpha replay-v2 run.
- Package B's mode/victory/format/topology and persistence-free
  playlist/ladder/rating definitions are implemented but unused.
- Package C's isolated map-generation model is implemented; its resolved
  rules and match-contract writers are still pending.
- No new definition is routed into a session, API, ladder, SDK, replay, or
  viewer.

## 1. Product terminology

These names have one meaning in code, APIs, documentation, and UI:

- **Game mode** — the semantic family that owns objective state, scoring, and
  completion, such as Frontline or Deathmatch.
- **Ruleset** — one immutable revision of gameplay mechanics and numeric
  tuning. A ruleset identifies form, action, transition, lifecycle, combat,
  perception, scoring, and victory definitions.
- **Match format** — the policy that maps submitted participants onto scoring
  teams and stable unit capacity, such as head-to-head, FFA-4, or 2v2.
- **Map** — immutable geometry plus named spawn/objective/placement regions.
  A map declares capabilities; it does not contain imperative game scripts.
- **Playlist version** — one immutable curated combination of ruleset, allowed
  match format, map pool, series scheduler, matchmaking policy, and admission
  requirements.
- **Ladder** — an opaque rating population attached to one playlist version
  and season. A ruleset ID is not a ladder ID.
- **Series** — a scheduled collection of matches whose aggregate result may
  update a ladder. The current `MatchSet` name remains a compatibility term
  while persistence is migrated.
- **Match contract** — the complete resolved ruleset, map, format, exact
  topology, and capability versions delivered before tick zero and stored in
  the replay.

FFA Deathmatch is therefore Deathmatch mode plus an FFA match format. It is
not a second engine mode and it does not need an `IsFfa` switch.

The exact participant count is part of match topology. It is never inferred
from a body's current `allies` collection, which can vary with liveness,
visibility, or team-perception policy.

## 2. Compatibility generations

The architecture begins by preserving, not widening, existing contracts:

| Generation | Rules contract | Match contract | Replay | Status |
|---|---:|---:|---:|---|
| Official duel | historical | historical | 1 | frozen and shipped |
| `frontline-alpha-1` | 2 | 1 | 2 | frozen local experiment |
| generic actor match | 3 | 2 | 3 | new experimental path |

Official `MatchEngine`, `MatchSession`, `Replay`, `ReplaySerializer`, runtime
protocol/configuration 0.1, replay hashes, and public rules remain untouched.
The Frontline-alpha rules/map/match fingerprints and replay-v2 actor run are
characterization fixtures.

The generic generation receives new schema numbers. Old serializers branch on
the stored schema and remain byte-exact; a new field is never smuggled into an
old fingerprint or replay. A future generic Frontline ruleset is a new named
arm, not a reinterpretation of `frontline-alpha-1`.

## 3. Resolved model

Catalog inheritance and experiment overrides are authoring conveniences only.
Before a match begins, the resolver materializes one flat immutable contract:

```text
ResolvedMatchContract
  ruleset
    gameMode
    victory + score channels
    lifecycle/spawn policy
    forms[]
    movementProfiles[]
    attackProfiles[]
    actions[]
    sameLifeTransitions[]
    replicationTransitions[]
    combat/perception/collision/tick rules
  matchFormat
  map
  exact topology
  capability versions
```

Rules, map, format, topology, and aggregate match fingerprints are separate.
The rules fingerprint does not change between 1v1 and FFA-4 Deathmatch; the
format/topology and aggregate fingerprints do.

Every official playlist names an explicitly validated combination. The
platform does not promise that every ruleset × format × map cross-product is
legal, balanced, or ranked.

## 4. Game modes, scores, victory, and results

`GameModeDefinition` is a closed tagged definition. Initially:

- `frontline` owns moving-front state and base-breach scoring;
- `deathmatch` owns kill/death score state.

Adding another mode adds a typed definition and mode kernel. It does not add
another nullable property beside `Frontline`.

Victory is a typed part of the mode definition. The first variants are:

- breach completion plus territorial timeout ranking;
- optional score-limit completion plus max-tick score ranking.

Dynamic score state is generic and keyed:

```text
teamId + scoreChannelId + exact integer value
```

Terminal results use canonical team standings:

```text
completionReasonId
endTick
teams[] { teamId, rank, scores[], outcome }
units[] { teamId, unitId, final lifecycle/form/health facts }
modeResult { typed mode-specific terminal facts }
```

Ranks support ties and any team count. `WinnerTeamId` and `WinnerSlot` may be
derived compatibility fields, but neither is the authoritative result model.
Integers that may exceed JavaScript's safe range use canonical decimal strings
on replay/API boundaries.

Deathmatch initially awards one kill point to the team of the exact damage
source that removes the final health point. Simultaneous mutual destruction
may score for both teams. Lifecycle retirement by replication is not a death
and awards no kill.

## 5. Formats and exact topology

`MatchFormatDefinition` describes topology policy, not concrete identities:

- `head-to-head`: two scoring teams and one participant per team;
- `free-for-all`: one scoring team per submitted participant;
- `teams`: an equal declared participant count across each scoring team.

Those variants cover 1v1, FFA-N, and equal-size NvN without optional shape
fields. If asymmetric teams become a real product requirement, they receive a
new typed format variant rather than changing the meaning of `teams`.

The resolved `PublicMatchTopology` remains the identity source:

```text
scoring team
  submitted participant/controller
    stable unit slot
      zero or one active runtime life
```

One submitted artifact controlling several clones is one participant with
several stable unit slots. A 2v2 team is two participants sharing one scoring
team. Those are different relationships and stay different in contracts,
replays, datasets, and ladders.

All dynamic entity creation is bounded by predeclared stable unit capacity.
Collections are keyed and canonically ordered; array position never becomes
identity.

## 6. Map generation 3

Map formats 1 and 2 remain exact. Generic actor matches use a new map
generation with:

- stable named spawn anchors rather than exactly two positional spawns;
- team-neutral spawn pools assigned by the resolved format/topology;
- typed objective and placement regions;
- movement-layer occupancy metadata;
- stable tile tags for transition restrictions;
- presentation metadata outside gameplay fingerprints.

An initial life binds to a named resolved spawn. Validators prove:

- unique canonical IDs and floor positions;
- enough compatible spawns for the exact topology;
- no duplicate initial occupancy;
- mode-required objective geometry exists;
- transition placement regions can satisfy their declared bounded output;
- format/map symmetry or fairness requirements declared by the playlist.

Movement layers describe traversal compatibility, not independent vertical
occupancy. All actor lives remain mutually exclusive per tile across layers;
Air can cross wall geometry but cannot share a tile with a Ground life.

Maps select typed profile values only. They cannot insert callbacks or reorder
engine phases.

## 7. Forms, profiles, actions, and transitions

Forms are catalog entries, never Prime/Child/Turret branches:

```text
form
  max health
  movement profile
  vision profile
  attack profile
  objective weight
  legal action IDs
  presentation capability ID
```

Named movement and attack profiles let two forms differ in wall traversal,
speed, damage, range, cadence, or projectile behavior without pretending those
are all global values.

Actions keep stable string IDs and numeric codes. Arguments use a bounded
tagged union with structural descriptors and per-tick legality masks. Do not
introduce arbitrary JSON/object bags. Unknown action or parameter semantics
make an artifact explicitly ineligible or produce a typed unsupported result;
they never silently become `Wait`.

Two transition families are distinct:

1. **Same-life form transition** — one life and runtime survive, preserving
   private memory according to a typed continuity policy. Anchor and a future
   reversible ground/flight switch use this family.
2. **One-to-many replication transition** — one source life retires and fresh
   descendant lives start in bounded slots. Split uses this family.

The first Air movement profile is a localized new engine capability with
explicit wall, projectile, landing, vision, and objective semantics. Once it
exists, additional flying forms and their health/duration/action tuning are
data-only.

## 8. Initial Split proof

The player-facing action name is **Split**; the internal transition family is
replication. The first proof arm uses:

- stable action ID `split` and code `103`;
- no decision payload; the resolved contract owns all outputs;
- source-form allowlist;
- two outputs in the initial arm;
- source generation zero and no prior same-life transformation;
- output generation one and no recursive Split;
- output health `floor(source current health / output count)`, minimum one;
- discarded remainder rather than created health;
- ordered source-relative candidate positions;
- reuse of the source stable slot plus lowest-ID compatible dormant slots;
- atomic all-or-nothing reservation;
- completion at the next tick start;
- source disposition `retired-by-replication`, not Destroyed.

The source remains targetable through the tick in which Split is queued.
Lethal damage cancels the pending replication. On completion, the source
runtime is disposed before each descendant receives a new life ID, isolated
runtime instance, private memory, and deterministic life seed. Descendants
share the participant artifact and normal team information, never WASM memory.

Simultaneous replication bundles that claim a common tile or slot all block.
Resolution never awards a reservation to whichever actor happened to appear
first in a collection.

Replay events are generic lifecycle facts:

- `replication-queued`;
- `replication-cancelled`;
- `life-retired` with reason `replication`;
- one `life-spawned` per descendant, carrying parent life, generation, form,
  health, position, and spawn reason.

These values are mechanics-proof inputs, not balance recommendations.

## 9. Actor host and mode sessions

The common actor host owns:

- exact contract delivery and capability negotiation;
- participant-to-life runtime factories;
- deterministic per-life seed derivation;
- start/tick/end invocation;
- exact active-actor preparation;
- runtime budgets, failure handling, and disposal;
- replay chronology.

A typed mode session owns:

- mode state and observation projection;
- action legality beyond structural validation;
- objective/score updates;
- mode completion and terminal facts.

The migration order is:

1. wrap the current `FrontlineMatchSession` behind the boundary without
   changing replay v2;
2. extract only demonstrably shared entity/combat/lifecycle operations;
3. implement Deathmatch as the second mode;
4. prove both modes use the same actor host and generic replay/result envelope.

This avoids a speculative universal phase DSL and avoids copying the complete
Frontline engine into every mode.

## 10. Replay, bots, ML, viewer, and soundtrack

Replay 3 retains the actor-complete chronology that made replay 2 trainable:

- exact resolved contract and topology;
- tick-start lifecycle;
- exact observation delivered to every active life;
- submitted and structurally accepted decisions;
- chosen and validated typed arguments;
- ordered authoritative events and projectile traversal;
- post-state, generic scores, standings, and terminal facts;
- replication lineage and exact firing-life attribution.

Dynamic mode observation is a tagged union plus the generic scoreboard.
Static mode/victory semantics live in the match-start contract. Variable
entity collections and legality masks remain canonical.

This is structurally suitable for entity/set encoders, recurrent per-life
state, rules vectors, and factored action heads. It does not promise zero-shot
competence on unseen team counts, modes, or action kinds. A new capability may
make a legacy model ineligible or strategically obsolete; that is intentional.

The web keeps one version-normalized replay model. Mode-specific scoreboard and
objective panels register by mode ID; actors, forms, projectiles, lifecycle,
selection, and health stay generic. An unknown presentation capability gets a
legible fallback rather than invalidating replay playback.

Adaptive music consumes normalized presentation signals—match progress,
score closeness, recent damage/destruction, active-life count, objective
pressure, and terminal state—not Frontline serializer fields. A mode may add a
small intensity adapter without coupling audio to its engine state.

## 11. Playlists, ladders, and rating

The persistence migration is additive:

```text
Playlist
PlaylistVersion (immutable definition + fingerprint)
Ladder (opaque ID, playlist version, season, state, rating policy)
BotRating (bot ID + ladder ID + scalar display rating + versioned policy state)
Series entrants
Series team results
Match team results
```

Historical rules-version ladders are backfilled as legacy playlists/ladders.
Existing `?rules=` APIs remain adapters through a unique legacy-rules alias.
BotA/B columns and response shapes stay during the compatibility period.

The rating boundary accepts all entrants and tied team standings atomically.
`DuelEloV1` must reproduce today's K=32, floor=100, zero-sum transfers exactly.
FFA rating is not implemented by looping pairwise Elo over opponents; its
policy is a separate later product decision.

One playlist version pins the ruleset, format, map pool, series scheduler, and
matchmaking/admission policy. One ladder pins that playlist version, season,
and rating-policy version. Workers store both exact identities when a series
is queued and never resolve “whatever is current” at execution time.

## 12. Implementation packages

### Package A — compatibility shield

- Pin official replay-v1 and contract hashes.
- Pin an end-to-end `frontline-alpha-1` actor replay-v2 hash.
- Keep the experimental alpha rules/map/match fingerprints exact.

### Package B — vocabulary and unused domain types

- Add typed game-mode, victory, score, match-format, playlist, ladder, and
  tied-standing definitions.
- Add pure compatibility validators and `DuelEloV1`.
- Route no production or experimental session through them yet.

### Package C — generic map/topology contract

- Add map generation 3 with named neutral spawns and typed regions.
- Resolve head-to-head, FFA-4, and 2v2 fixture topologies.
- Add rules schema 3 and match-contract schema 2 writers while keeping old
  writers byte-exact.

### Package D — actor protocol and replay 3

- Mirror the new contract in the SDK tagged codec.
- Add generic mode observation, scoreboard, action arguments, result, and
  lifecycle lineage.
- Bump only actor-generation contract versions.

### Package E — common actor host and Frontline adapter

- Put the alpha-equivalent session behind the neutral host/mode boundary.
- Prove observation, resolution, and replay equivalence.

### Package F — Split

- Implement bounded one-to-many reservation, retirement, lineage, fresh
  runtimes, events, observations, and determinism tests.

### Package G — Deathmatch proof

- Add typed Deathmatch mode state and completion.
- Run the same definition as 1v1 and FFA-4; add a 2v2 topology fixture.
- Treat all values as an experimental mechanics arm.

### Package H — competition persistence

- Add playlist/ladder/series normalization and dual-write legacy duel data.
- Route the current official ladder through `DuelMirrored6V1` and
  `DuelEloV1` without changing outcomes or APIs.
- Add generic read APIs before admitting multiplayer ranked creation.

### Package I — presentation and evaluation

- Extend normalized replay/viewer/mobile models from replay 3.
- Preserve form-specific health and add Split/scoreboard causality.
- Integrate adaptive soundtrack through normalized signals.
- Run the required native-cohort, dynamics, and outcome-blind workflow before
  any mode or mechanic becomes ranked.

## 13. Acceptance gates

Architecture is accepted only when:

- every official replay-v1 hash and `frontline-alpha-1` fingerprint/replay-v2
  hash remains exact;
- Deathmatch rules have the same rules fingerprint under 1v1 and FFA-4 while
  format/topology/match fingerprints differ;
- Frontline and Deathmatch share one actor host and replay/result envelope;
- Split starts isolated fresh runtimes with deterministic lineage and cannot
  exceed declared topology capacity;
- reversing input collection order cannot change reservation, combat, result,
  or replay hash;
- 1v1, FFA-4, and 2v2 results support tied standings without winner-slot
  assumptions;
- current ranked duel creation, six-game mirroring, Elo, secrecy, ratings,
  endpoints, and generated clients remain compatible;
- maps/rules/formats fail typed compatibility validation before match start;
- bots receive exact topology, mode/victory definitions, rules values, and
  legal-action masks;
- replay 3 remains sufficient for deterministic playback and ML dataset
  extraction without re-simulating engine logic.

## 14. Experiment pre-registration

This is a **substantial architecture and mechanics change**. The initial
packages answer compatibility and mechanical-executability questions only.
They do not constitute a balance or entertainment verdict.

Hypotheses:

1. official duel and Frontline-alpha evidence remains byte-exact;
2. new formats change only format/topology/match fingerprints;
3. new typed mechanics require localized implementations rather than
   cross-system envelope rewrites;
4. Split and Deathmatch remain deterministic under reversed input order;
5. generic results and ladders represent duel, FFA, and teams without lossy
   BotA/B or winner-slot projection.

The first mechanics fixtures use frozen maps, policies, seeds, and placeholder
numbers only to exercise paths. No value is promoted or tuned from those
fixtures. A product verdict later requires at least four independently
authored mode-aware doctrines, canonical all-WASM artifacts, frozen holdout
blocks, descriptive dynamics, and at least twelve outcome-blind replay reviews
under [`EVALUATION-METHODOLOGY.md`](EVALUATION-METHODOLOGY.md).
