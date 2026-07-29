# Game-mode and competition architecture

Status: **active implementation; hosted Labs slice implemented behind an
off-by-default flag, no generic ranked product shipped**, 2026-07-28.

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
- Package B's mode/victory/format/topology definitions now drive the generic
  Engine path. Playlist/ladder/rating definitions also underpin the additive
  legacy-identity migration; generic competition results and rating remain
  later packages.
- Package C's isolated map-generation model, resolved actor-rules catalog,
  lifecycle profiles, three transition families, and generic standings
  component are implemented and used by generic sessions. The cross-component
  resolved match aggregate accepts head-to-head, FFA-4, and 2v2 Deathmatch
  through the same rules and rejects invalid capacity, placement, respawn,
  arithmetic, and mode-map combinations before hashing. Separate
  rules/map/format/topology and aggregate match writers now use explicit
  canonical wire IDs, provenance separation, captured capability versions,
  and literal golden fingerprints. Package C is complete.
- Package D's static and dynamic SDK contract, strict tagged codecs, exact
  `generic-actor-match-3` negotiation, profile-aware Guest state machine, and
  controlled-build capability detection are implemented and independently
  reviewed. The neutral actor runtime boundary executes schema-3 matches and
  replay 3 is the strict generic chronology/result envelope.
- Packages E–G now provide one neutral actor session, typed Deathmatch and
  Frontline mode drivers, same-life forms, bounded Split, source-preserving
  fabrication, generic chronology reconstruction, and 1v1/FFA/team proof
  fixtures. Their values remain experimental mechanics inputs.
- Package H's deterministic legacy playlist/version/season/ladder identity,
  repeatable backfill, and Duel dual-write foundation are implemented.
  The narrower hosted Labs slice also persists participant team identity and
  normalized match-team standings/scores for one setless match. Normalized
  series entrants/results, reveal-ordered settlement, broad generic
  competition APIs, and multiplayer rating policy remain planned.
- Package I normalizes replay 3 in the web viewer and carries its closed typed
  presentation through the hosted mobile bridge. A bot-detail Labs panel can
  now create one direct, unranked generic Frontline match when
  `BOTARENA_FRONTLINE_LABS_ENABLED` is true; the flag defaults off and also
  prevents newly compiled generic-only artifacts from activating while
  disabled. The existing direct match page/viewer presents its broadcast-safe
  replay 3.
  There is no generic open matchmaking, season, ladder, rating, or series,
  and none of this checkpoint promotes Frontline balance values.

## 1. Product terminology

These names have one meaning in code, APIs, documentation, and UI:

- **Game mode** — the semantic family that owns objective state, scoring, and
  completion, such as Frontline or Deathmatch.
- **Ruleset** — one immutable revision of gameplay mechanics and numeric
  tuning. A ruleset identifies form, action, transition, lifecycle, combat,
  perception, scoring, and victory definitions.
- **Match format** — the policy that maps submitted participants onto scoring
  teams and stable unit capacity, such as head-to-head, FFA-4, or 2v2.
- **Topology profile** — a descriptive, versioned name for the exact
  participant/controller, scoring-team, stable-slot, and initial-life shape.
  The topology fingerprint, not the profile name, is authoritative.
- **Map** — immutable geometry plus named spawn/objective/placement regions.
  A map declares capabilities; it does not contain imperative game scripts.
- **Playlist version** — one immutable curated combination of ruleset, allowed
  match format, map pool, series scheduler, matchmaking policy, and admission
  requirements. It also pins the execution policy and semantic engine version;
  neither is inferred from admission profile or replay format.
- **Ladder** — an opaque rating population attached to one playlist version
  and season. A ruleset ID is not a ladder ID.
- **Series** — a scheduled collection of matches whose aggregate result may
  update a ladder. The current `MatchSet` name remains a compatibility term
  while persistence is migrated.
- **Match contract** — the complete resolved ruleset, map, format, exact
  topology, and capability versions delivered before tick zero and stored in
  the replay.
- **Evaluation profile** — Balance Lab policy for lineup construction,
  participant/team assignment, payoff interpretation, and compatible metrics.
- **Qualification profile** — the semantic capability distribution on which a
  bot earned its cumulative T/C result. It is separate from playlist admission
  and is fingerprinted in balance evidence.

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
| generic actor match | 3 | 2 | 3 | experimental Engine plus off-by-default hosted Labs path |

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
    lifecycle profiles
    forms[]
    movementProfiles[]
    visionProfiles[]
    attackProfiles[]
    actions[]
    fabricationTransitions[]
    sameLifeTransitions[]
    replicationTransitions[]
    participant runtime-fault policy
    combat/damage/perception/collision/tick rules
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

The public score catalog is separate from the ordered timeout ranking.
Deathmatch may expose deaths, damage, and active health without making them
hidden tiebreakers. Frontline initially exposes exactly one signed
`TerritorialProgress` channel and ranks timeouts by exactly that channel,
higher first; it has no hidden timeout tiebreakers. Dynamic score state is
generic and keyed:

```text
teamId + scoreChannelId + exact integer value
```

Frontline form objective weight is binary (`0` or `1`); positive bodies do not
stack. Sole-team gain reaching or exceeding the threshold completes exactly
one push and discards overshoot, using signed-64-bit addition before the
threshold comparison. Opposing gain erodes a claim to zero without starting
its own claim on the same tick. Empty/contested decay uses consecutive ticks,
resets after each applied interval and on any sole-control tick, floors at
zero, and the disabled `(0, 0)` pair preserves the claim with a zero clock.

`TerritorialProgress` is computed per team as its advance-direction delta
times `(active objective index - centre index)` times the threshold, plus its
own positive claim or minus an opponent's claim. Checked signed-64-bit
arithmetic makes higher values mean progress toward the opposing base for
either direction. A non-breaching capture advances immediately, clears claim
and decay state, leaves actors and projectiles in place, and ignores objective
control through the configured redeploy pause. Control resumes at
`captureTick + 1 + pause`; breach skips the pause. Base breach on the final
allowed tick precedes timeout.

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

Deathmatch uses raw counts rather than point multipliers. Every damage-caused
destruction adds one Death to the destroyed actor's scoring team. The exact
hostile damage instance that reduces remaining health to zero adds one Kill to
its source life's scoring team; persistent projectiles retain their firing-life
source. Allied or self final damage records the victim team's Death but no Kill,
and hostile DamageDealt records only actual health removed. ActiveHealth is the
terminal sum across active team lives.

Lifecycle retirement by replication is not destruction and adds neither a
Death nor a Kill. All joint-tick damage and score increments resolve before an
optional kill limit is checked. A unique highest raw Kill count at or above the
limit wins; teams tied at the top draw, so simultaneous mutual threshold kills
remain representable. TimeoutRanking is not reused for this early result.
On the final allowed tick, this complete-joint-tick kill-limit check precedes
timeout ranking.

Damage contacts are collected before any health mutation. Per target, they are
then applied in canonical source-team/unit/life, projectile-ID, and path-contact
order; actual health removal is capped to remaining health and the first
ordered contact crossing zero owns lethal attribution. Projectile IDs are
match-wide monotonic signed 64-bit integers assigned from canonical same-tick
source order. This makes kill credit and replay events independent of hash-map
iteration while preserving simultaneous joint-action validity.

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
- automatic-return spawns are unique, permanently reserved, and cannot be
  occupied by another initial life or lifecycle placement;
- source-preserving fabrication has a legal source tile plus a candidate
  offset for at least one possible dynamic actor facing;
- format/map symmetry or fairness requirements declared by the playlist.

Movement layers identify traversal compatibility, not independent vertical
occupancy. All actor lives remain mutually exclusive per tile across layers.
Rules schema 3 initially admits only the implemented Ground layer. Air is a
future typed engine capability; its wall traversal, landing, projectile,
vision, objective, and occupancy semantics must be added explicitly before a
ruleset can select it.

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
```

Named movement, vision, and attack profiles isolate capabilities and tuning
from form identity. Schema 3 currently gives movement profiles only an
implemented movement layer; speed or novel traversal becomes an additive typed
capability when its exact tick semantics exist. Damage, range, cadence,
projectile behavior, vision, health, and legal actions are already resolved
data. Optional visual metadata maps stable form IDs to presentation outside
gameplay fingerprints.

Schema 3 admits one reproducible grid-combat kernel rather than arbitrary
boolean combinations: exclusive Ground occupancy, connected conflicting moves
all block, ordered projectile tile substeps cannot tunnel, walls precede actor
contact, and all contacts enter one canonical damage batch. Allied projectile
contact remains a closed selectable policy. Moving into a non-passing
projectile blocks the actor at its origin, consumes contacts in projectile-ID
order, and queues any permitted damage into the joint batch. Allied
pass-through does not itself block or consume. Instant rays use canonical
inert traversal fields; discrete projectiles do not receive a second
launch-tick advance.

Programmed attacks use an explicit eight-heading clockwise-modulo model,
one-octant bends, unique initial offsets in `[-4, 3]`, and bend directions from
`{-1, +1}`. Hearing likewise names its exact eight-octant strict-two-to-one
cardinal boundary model; a sector count alone is not treated as sufficient
semantics. Disabled capabilities use canonical inert values so behavior-equal
rules cannot acquire different fingerprints or ML rules vectors.

Attack availability is evaluated from pre-tick cooldown and energy. A
successful attack sets the configured cooldown at end of tick without
decrementing it immediately; other active ticks subtract one to zero. Energy
cost is paid before same-tick regeneration, and regeneration follows global
completed-match-tick modulo cadence rather than life age or time since spend.
Energy arithmetic uses a signed-64-bit intermediary before clamping.

Actions keep stable string IDs and numeric codes. Arguments use a bounded
tagged union with structural descriptors and per-tick legality masks. Do not
introduce arbitrary JSON/object bags. Unknown action or parameter semantics
make an artifact explicitly ineligible or produce a typed unsupported result.
Within schema 3, movement is one absolute cardinal tile without changing
facing, and rotation sets an absolute cardinal facing without translating.
Unknown/malformed actions are `Faulted`, catalog actions outside the current
form mask are `Rejected`, and valid actions stopped by authoritative state are
`Blocked`; only `Faulted` increments the participant fault counter. Explicit
typed action-variant outcomes override this generic state result.

Three lifecycle-action families are distinct:

1. **Source-preserving fabrication** — the source remains an ordinary active
   life while one explicitly targeted, predeclared dormant slot is reserved
   and later receives a fresh child life. The child has an isolated runtime
   and no inherited private memory.
2. **Same-life form transition** — one life and runtime survive, preserving
   private memory, remaining cooldown, and non-refilling clamped energy
   according to typed continuity policies. Queue and completion placement
   legality use required/forbidden map tags; schema 3 retains the same occupied
   Ground tile. Anchor and a future reversible ground/flight switch use this
   family, with a later position/layer policy added to this tagged boundary.
3. **One-to-many replication transition** — one source life retires and fresh
   descendant lives start in bounded slots. Split uses this family.

Rules own lifecycle profiles; the resolved match assigns one profile and an
allowed-form set to every stable unit slot. Automatic respawn, readiness for
explicit fabrication, and permanent dormancy are closed policies. Match-local
participant-to-region bindings express roles such as “own fabrication pad”
without baking team IDs into reusable rules or neutral maps.

Every newly created life has a monotonically increasing slot-local life ID,
fresh runtime and empty memory, deterministic seed, target-form maximum health,
zero cooldown, target-profile maximum energy, and no previous action result.
It may act on its creation tick and joins the normal global resource cadence
at that tick's end. A projectile may enter a reserved lifecycle output tile
before its due tick; immediately before a return, fabrication, or replication
spawn, every occupying projectile is consumed in projectile-ID order without
damage, then the new life is created before observations.

Static map, rules, topology, mode, and objective facts are immutable
`MatchStart` inputs rather than facts a runtime must rediscover. Private
state learned while playing belongs to one runtime life: same-life form
transitions retain it, while destruction/return, explicit refabrication, and
Split descendants start with empty private memory. Team perception shares the
current frozen observable union allowed by the contract; it does not copy a
parent's WASM memory into a descendant or preserve a historical team map.
Durable team memory would be a separately bounded, replayed blackboard
capability rather than an accidental consequence of clones sharing an
artifact.

Permanent objective-inert forms also need a playlist-level liveness gate.
The initial Frontline shape satisfies it by allowing only the expendable child
to Anchor while a separately renewable, objective-weighted Prime remains
mobile. A future ruleset that lets its last renewable progress-capable life
become stationary must either retain objective/progress semantics or use a
typed bounded tenure with a forced same-life return. `maxTicks` guarantees
termination, but is not an entertainment remedy for two unreachable
stationary actors. Activation limits, return timing, and return-health
continuity belong in a new typed transition capability before such a playlist
is admitted; they are not inferred by the current permanent-transition
kernel.

The first Air movement profile remains a localized new engine capability with
explicit wall, projectile, landing, vision, objective, and occupancy
semantics. Once it exists, additional flying forms and their
health/duration/action tuning can be data-only.

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
An earlier source-preserving fabrication bundle survives this non-destructive
Split retirement and completes from its reserved slot/tile and recorded parent
identity. Participant disqualification is the explicit override that cancels
all owned pending work.

Simultaneous replication bundles that claim a common tile or slot all block.
Resolution never awards a reservation to whichever actor happened to appear
first in a collection.

The hosted Frontline Labs v1 arm resolves Split descendants into the
`replica-mobile` form. Replicas remain mobile and their legal-action catalog
does not contain `transform`; only a `child-mobile` life created through
Fabricate may Anchor into a turret. This is a frozen playlist-v1 contract
choice, not a universal restriction on future replication definitions.

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

Runtime faults are participant-scoped across every controlled slot, life, and
runtime stage. Fault events are ordered by participant, actor identity, then
create/start/tick/validation stage rather than host callback order. A runtime
creation, start, or tick-execution failure discards the instance, contributes
a synthetic `Wait`, and—if still eligible—gets one fresh create-and-start
attempt before that life's next decision. Decision-validation faults retain
the healthy runtime instance. The counter uses signed-64-bit arithmetic
saturated at the configured allowance plus one.

After joint damage and before the mode update, a participant exceeding its
tolerance is disqualified. All owned pending clocks, fabrication,
replication, and same-life work are cancelled in canonical order; claims are
released; surviving projectiles are removed after already-collected damage;
active lives retire without retirement kill/death credit; and owned slots
become permanently dormant. Damage-caused zero-health destruction still
finalizes, but cannot schedule a return for a disqualified slot.

Post-damage destruction finalization precedes fault-based match completion. A
multi-participant scoring team remains eligible while any participant remains;
fully disqualified teams rank below every eligible team and tie at the bottom.
One remaining eligible team wins immediately, while zero eligible teams draw;
this short-circuits mode update. With multiple eligible teams, early mode
completion precedes max-tick timeout and both compare eligible teams only.
Thus an FFA leader cannot fault out and retain a timeout win, and one faulted
member does not erase a 2v2 teammate.

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

One playlist version pins the ruleset, format, map pool, series scheduler,
matchmaking/admission policy, execution policy, and semantic engine version.
One ladder pins that playlist version, season, and rating-policy version.
Workers store both exact identities when a series is queued and never resolve
“whatever is current” at execution time.

### Hosted Frontline Labs checkpoint

The first hosted generic slice deliberately stops below the series and ladder
layers:

- immutable playlist `frontline-labs`, version 1, pins ruleset
  `frontline-labs-1`, map `frontline-labs-01`, head-to-head format,
  `single-match-v1`, direct-challenge admission, and exact contract profile
  `generic-actor-match-3`;
- creation accepts exactly two distinct eligible submitted bots. The first
  entrant is owned by the caller; both active versions must have compile-time
  support for the exact generic profile;
- one setless, unranked `Match` is queued with its playlist version and
  participant-to-team mapping pinned. Turning the discovery/admission flag
  off does not invalidate already queued work;
- generic execution resolves the playlist key/version through a hosted
  definition registry, validates its canonical fingerprint and engine pin,
  and gives every immutable playlist version a distinct retrying queue
  capability. Each configured generic lane claims the capability set known to
  its binary, so old workers leave new definitions pending without
  multiplying concurrency. The legacy Duel lane rejects that execution
  policy. Historical definitions/capabilities remain registered until their
  pending and running jobs have drained or been explicitly migrated;
- canonical replay 3 is stored with its format version. During broadcast the
  replay endpoint emits a validated canonical prefix with result and hash
  withheld; after reveal it returns the complete document;
- terminal authority is normalized `MatchTeamResult` plus keyed signed
  `MatchTeamScore`. Legacy participant outcome/health and winner-slot fields
  are compatibility projections only;
- the existing direct match endpoint and version-normalized viewer are reused.
  Labs matches are excluded from the legacy Duel feed, bot history/statistics,
  achievements, and result notifications.
- durable Labs admission budgets default to 10 starts per account per day,
  one active match per account, and four active matches globally, with a
  separate two-per-minute burst policy. All remain configurable and the
  feature flag remains false by default.

This slice creates no `MatchSet`, series entrant, season, ladder, rating,
series settlement, or generic leaderboard. FFA, 2v2, Deathmatch admission,
ranked generic play, and broader playlist discovery remain future consumers of
the same format/topology/result envelopes.

## 12. Implementation packages

### Package A — compatibility shield

- Pin official replay-v1 and contract hashes.
- Pin an end-to-end `frontline-alpha-1` actor replay-v2 hash.
- Keep the experimental alpha rules/map/match fingerprints exact.

### Package B — vocabulary and executable domain types

- Add typed game-mode, victory, score, match-format, playlist, ladder, and
  tied-standing definitions.
- Add pure compatibility validators and `DuelEloV1`.
- Route generic experimental sessions through the mode/result definitions;
  keep public production admission on the compatibility path until the later
  persistence and rollout gates are complete.

### Package C — generic map/topology contract

- Add map generation 3 with named neutral spawns and typed regions.
- Add flat actor rules with profile catalogs, lifecycle assignments,
  fabrication, same-life transitions, and replication.
- Validate every rules/map/format/topology/deployment reference and bounded
  lifecycle capacity before hashing.
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

### Package H — competition identity foundation and normalization

- Add playlist/version/season/ladder identities and dual-write them onto
  legacy Duel data first.
- Route the current official ladder through `DuelMirrored6V1` and
  `DuelEloV1` without changing outcomes or APIs.
- Persist normalized match-team results and score channels for the narrow
  setless Labs path. Add normalized series entrants and series-team results
  only in the later persistence stage; they are not implied by this slice.
- Defer rating mutation, achievements, notifications, and result deltas until
  reveal-time settlement; simulation completion alone is not publication.
- Keep Labs unranked and outside Duel stats/achievements/notifications; add
  broad generic read APIs before admitting multiplayer ranked creation.
- Follow the additive schema, ordered settlement state machine, compatibility
  adapters, and secrecy gates in
  [`COMPETITION-PERSISTENCE-PLAN.md`](COMPETITION-PERSISTENCE-PLAN.md).

### Package I — presentation and evaluation

- Extend normalized replay/viewer/mobile models from replay 3.
- Preserve form-specific health and add Split/scoreboard causality.
- Reuse the direct match viewer for the off-by-default hosted Labs slice; do
  not infer ranked or evaluated status from availability.
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
