# Playlist, ladder, and series persistence plan

Status: **active migration plan; not implemented**, 2026-07-27.

This plan is the persistence and publication companion to
[`GAME-MODE-ARCHITECTURE.md`](GAME-MODE-ARCHITECTURE.md). It replaces
rules-version-as-ladder and duel-shaped result storage with immutable playlist
versions, opaque seasonal ladders, normalized entrants, and team standings.
It must preserve the current official duel scheduler, Elo calculations, and
legacy API shapes throughout migration.

The migration is additive. Generic FFA and team matches may be played and
read before a multiplayer rating policy exists, but they must not enter an
open ranked ladder.

## 1. Invariants

1. A queued series pins an exact playlist version and ladder. Workers never
   resolve whichever rules, map pool, format, or policy is current later.
2. A playlist version is immutable. A change creates the next version.
3. A ladder is one rating population for one playlist version, season, and
   rating-policy version. A ruleset name is only a legacy lookup alias.
4. Match and series outcomes are team standings with ties. Bot A/B,
   WinnerSlot, and scalar A/B fields are compatibility projections for Duel.
5. Rating changes are one atomic policy application over all entrants. FFA
   is never approximated by repeatedly applying pairwise Elo.
6. Simulations finishing does not make a ranked result public. Ratings,
   ranks, achievements, notifications, and result deltas remain unchanged
   until every required broadcast is revealable.
7. Settlement follows ladder sequence order. A later revealed series cannot
   use ratings that omit an earlier hidden series.
8. Historical rows remain truthful. Unknown historical scheduler, map-pool,
   admission, matchmaking, or season metadata is labelled `legacy-import`;
   current definitions are not retroactively invented.

The sixth invariant closes an existing secrecy gap: today the finalizer can
update `BotRating` while broadcasts are still pending. Redacting
`ratingChange` on the match-set response is insufficient because leaderboard
totals, rank, bot detail, achievements, and notifications can reveal the
result indirectly.

## 2. Physical model

`MatchSet` remains the physical series header during compatibility. A second
`Series` table would duplicate ownership and make dual-write correctness
harder.

### Playlist

```text
Playlist
  Id
  Key                    unique stable public key
  DisplayName
  CreatedAt

PlaylistVersion
  Id
  PlaylistId
  Version                positive, unique with PlaylistId
  GameModeId
  RulesetId
  MatchFormatId
  MapPoolId
  SeriesPolicyId
  MatchmakingPolicyId
  AdmissionPolicyId
  CanonicalDefinition
  DefinitionFingerprint lowercase SHA-256
  Provenance
  Visibility
```

The database rejects update or delete of `PlaylistVersion`. Definition
fingerprints cover the exact canonical definition, not display metadata.

### Ladder

```text
Ladder
  Id                     opaque
  PlaylistVersionId
  SeasonId
  Status                 draft | open | closed
  RatingPolicyId
  LegacyRulesVersion     nullable unique compatibility alias
  IsListed
  AwardsAchievements
  NextRatingSequence
```

`(PlaylistVersionId, SeasonId)` is unique and at most one ladder for a
playlist version may be open.

### Series and entrants

Add to `MatchSet`:

```text
PlaylistVersionId
LadderId
SettlementStatus
ResultReadyAt
SettlementDueAt
SettledAt
RatingSequence
```

`(LadderId, RatingSequence)` is unique when present. Legacy BotA/B, ScoreA/B,
rating A/B, winner, and rules fields become nullable only after normalized
reads exist; they remain dual-written for compatible Duel series.

```text
SeriesEntrant
  Id
  SeriesId
  EntrantOrder
  TeamId
  BotId
  BotVersionId
  Bot/owner/appearance/artifact snapshots
  RatingBefore           nullable until settlement
  RatingAfter            nullable until settlement
  RatingChange           nullable until settlement
  PolicyStateBefore      nullable until settlement
  PolicyStateAfter       nullable until settlement
```

Entrant order and bot are unique within one series. Historical snapshot
fields intentionally survive deletion of a bot or version, so those snapshot
references are not cascading foreign keys.

```text
SeriesTeamResult
  SeriesId + TeamId      primary key
  Placement
  SeriesPoints
  Outcome

MatchTeamResult
  MatchId + TeamId       primary key
  Placement
  Outcome

MatchTeamScore
  MatchId + TeamId + ScoreChannelId
  Value                  signed 64-bit
```

Typed score-channel rows preserve queryability and canonical ordering without
an unbounded JSON score bag.

Add `PlaylistVersionId` to `Match`, with a real foreign key from
`Match.MatchSetId` and a consistency constraint preventing a match from
naming a playlist version different from its series.

Add `TeamId` and nullable `SeriesEntrantId` to `MatchParticipant`.
`(MatchId, SeriesEntrantId)` is unique when present.

### Rating

`BotRating` gains:

```text
LadderId
PolicyState
RatingRevision
```

Its logical key becomes `(BotId, LadderId)`, with leaderboard index
`(LadderId, Rating DESC, BotId)`. `RulesVersion` remains a nullable legacy
mirror during migration; generic ladders leave it null.

## 3. Pinned Duel compatibility

The current official series scheduler becomes a pure policy:

```text
DuelMirrored6V1
  exactly three distinct map/seed pairs
  exactly six games
  for each pair: A/B, then B/A
  win = 1 series point
  draw = 0.5 series point
  competition-ranked team standings
```

The official playlist pins `DuelMirrored6V1` and `DuelEloV1`. `DuelEloV1`
retains K=32, floor=100, canonical entrant ordering, and zero-sum transfers.
The production finalizer must call that named policy once, while the existing
`EloAdjustment` API may remain as a compatibility wrapper.

## 4. Settlement state machine

Settlement has its own state; it is not inferred from `MatchSet.Status`.

### Creation

In one transaction:

- create a Running set with `PendingExecution` settlement;
- pin PlaylistVersion and Ladder;
- snapshot normalized entrants;
- materialize every scheduled match and execution job;
- dual-write legacy Duel fields from those same entrant objects.

### All simulations terminal

Acquire locks in the order `Ladder → Series`.

If any match failed:

- mark the series Failed;
- mark settlement Failed;
- assign no rating sequence and mutate no rating.

If every match completed:

- validate the pinned series policy;
- aggregate match-team results into series-team standings;
- reserve `RatingSequence = Ladder.NextRatingSequence++`;
- set `ResultReadyAt`;
- mark settlement `AwaitingReveal`;
- populate Duel compatibility result fields where applicable;
- enqueue settlement for the latest broadcast boundary plus safety margin;
- do not mutate rating, achievement, notification, or public delta state.

### Reveal-time settlement

The settlement worker locks the Ladder and processes the lowest unsettled
rating sequence.

For each candidate:

1. Recheck that every required broadcast is complete at the database time.
2. Stop if the earliest sequence is not revealable; later sequences cannot
   overtake it.
3. Lock the series, then every entrant bot row in sorted BotId order.
4. Load or create each `(BotId, LadderId)` rating.
5. Resolve exactly one policy from `Ladder.RatingPolicyId`.
6. Invoke it once with all entrants and tied team standings.
7. Require exactly one finite output for every entrant.
8. Atomically update ratings and policy state, write entrant before/after
   facts, project compatible legacy A/B deltas, award progression, mark the
   series Settled, and enqueue its now-due announcement.

Retries of Settled work are no-ops. The universal lock order is:

```text
Ladder → Series → Bot rows ordered by BotId
```

Public result projection requires both broadcast completion and Settled.
Failed series may reveal their failure without rating facts.

## 5. APIs

Keep the current request/response shapes unchanged:

- `POST /api/matches/ranked`;
- `GET /api/leaderboard?rules=`;
- `GET /api/matchsets/{id}`;
- bot `ratings` and `currentStanding`;
- existing CLI, web, and mobile Duel views.

Compatibility adapters resolve `?rules=` through
`Ladder.LegacyRulesVersion`. Only ladders with an alias appear in legacy
rating collections. Generic series never fabricate BotA/B.

Additive generic reads are:

- `GET /api/playlists`;
- `GET /api/ladders`;
- `GET /api/ladders/{ladderId}/leaderboard`;
- `GET /api/series/{seriesId}`.

The generic series response contains entrants, teams, score channels, and tied
standings. It has no winner-slot field. Every response is a named DTO
registered with `.Produces<T>()`.

After API changes regenerate and compile:

- `contracts/BotArena.App.json`;
- `web/src/api/schema.d.ts`;
- `mobile/src/api/schema.d.ts`;
- `src/BotArena.Cli/Generated/ApiContracts.cs`.

Legacy schemas must have no accidental diff.

## 6. Migration

### Expand

Create the new tables, indexes, immutability trigger, and nullable identity
columns. Relax legacy duel columns only where multiplayer rows require null.
Keep all existing columns and indexes. Old application images must still be
able to insert during the expand window.

### Idempotent application backfill

Use a migrate-role application backfiller because canonical fingerprints and
historical classification do not belong in ad hoc SQL:

- collect every distinct nonblank rules version from ratings, sets, and
  matches;
- create immutable `legacy-import` playlist versions and one historical
  ladder per observed population;
- mark the current alias Open and older aliases Closed;
- backfill LadderId without changing rating IDs, values, or ranked-set counts;
- backfill playlist/ladder IDs on series and matches;
- create team-0 BotA and team-1 BotB entrants independent of mirrored slot;
- derive match-team results from WinnerSlot through participant TeamId;
- copy completed standings and rating transitions;
- mark completed history Settled, failed history Failed, and running work
  PendingExecution.

Natural keys and conflict handling make the backfill repeatable.

### Dual write and switch reads

Drain old workers, or explicitly pin every queued set, before enabling new
execution. A legacy worker could otherwise finish without normalized rows and
publish ratings early.

Deploy dual-write code while legacy reads remain authoritative. Assert and
measure normalized-versus-legacy equality. Then switch legacy reads to ladder
aliases and expose generic reads.

Only after rollback images are outside the support window:

- require PlaylistVersionId, LadderId, and participant TeamId;
- add composite identity-consistency foreign keys;
- retain legacy columns for at least one further release;
- treat their removal as a separate destructive migration.

## 7. Acceptance tests

Pure tests:

- all 729 six-game Duel outcome combinations match the old aggregator;
- `DuelEloV1` matches its half-point reference exhaustively;
- tied FFA and team standings remain ties;
- playlist fingerprints are canonical and versions immutable.

PostgreSQL and worker tests:

- empty-schema constraints, indexes, and triggers;
- pre-migration backfill for current, historical, and experimental ladders;
- repeatable backfill;
- uniqueness of aliases, bot+ladder, entrant order/bot, and team results;
- two workers finalizing one series;
- concurrent series sharing multiple entrants;
- rollback after aggregation, rating flush, achievement write, and before
  commit;
- changed server defaults cannot alter a queued match;
- unknown scheduler or rating policy fails closed.

Secrecy tests:

- before reveal, leaderboard totals and ranks are unchanged;
- bot list, detail, and current standing are unchanged;
- no peak-rating entitlement or notification appears;
- set and generic-series standings/deltas remain concealed;
- one through five watched Duel games still conceal the series;
- an early settlement job refuses to settle;
- every public surface changes atomically after final reveal.

HTTP and client tests:

- legacy JSON characterization fixtures remain exact;
- rules aliases resolve canonically;
- FFA-4 and 2v2 reads preserve ties and arbitrary entrant counts;
- deleted bots use entrant snapshots;
- OpenAPI and all generated clients compile.

## 8. Deferred decisions and blockers

- Historical map-pool, scheduler, matchmaking, admission, and season metadata
  cannot be reconstructed; the backfill must say so.
- Existing queued rows with an unpinned current-rules lookup must be drained
  or converted explicitly.
- Matchmaking intentionally ignores hidden results until reveal. Rating
  sequence order makes subsequent settlement deterministic.
- There is no accepted FFA/team rating policy. Multiplayer playlists remain
  unranked until one is separately designed and evaluated.
- Announcements, notification payloads, and some history UI are Duel-shaped.
  The official adapter remains safe, but those consumers must be generalized
  before multiplayer ranked admission.
