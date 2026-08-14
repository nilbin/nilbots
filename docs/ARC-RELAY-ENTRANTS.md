# Arc Relay entrants

Arc Relay's competitive entity is an **entrant**. The hosted product ships two
kinds on the same ladder surface:

- a **sheet**: a saved tactical playbook plus plotted map layout, compiled by
  the shared tactical compiler and executed by the registered first-party
  tactical mind;
- a **custom mind**: player source built by the controlled toolchain and run
  only in the fuel- and memory-limited WASM sandbox.

Every entrant has a stable identity, player-selected deterministic procedural
crest, eight-slot composition, and exact-compatible Elo rating. A sheet save or
mind resubmission advances its revision without resetting rating. Save-as-copy
or a new submission creates a new identity and starts at the default rating.

The current hosted identity is immutable entrant playlist v22: rules and map
`arc-relay-tapline-04` on the 31x27 Tapline, matching the default local
`nilbots arc-relay` command and its coordinated planner. v7 began the Warren
development rating era; v8 repaired its frozen WASM artifact. Public Season 1
began at 1200 on a separate v8 ladder. v9 moves the stock participant to the
consolidated direct-ladder sheet representation; v10-v12 retained that rating book for
semantic labels, bounded movement, and relative-Well subjects. v13 gives
authored routes persistent terrain courses and progress-aware waypoint hints:
arrival cannot lap a stationary body, and an unproductive point yields to a
later reachable one. Its registered stock-cell gameplay digest changed, so v13
starts a fresh rating book at the default rather than copying v12 Elo. v14
makes reachability-based give-up decisions terrain-only, bounds custody
fallback re-tests, and propagates loaded-carrier right-of-way through allied
queues to the nearest deterministic vacancy. That behavior changes the same
registered stock cell, so v14 starts fresh again rather than copying v13 Elo.
v15 adds bounded traffic detours to sticky authored courses: a body may rejoin
ahead of a transient blockage without reselecting route direction or lap, and
an attempt that cannot beat waiting remains a causal wait. It also makes the
authoritative generic-actor movement gate wall-corner strict using terrain
only. The registered stock digest changes again, so v15 starts fresh rather
than copying v14 Elo. v16 stabilizes loaded-carrier delivery planning for the
whole tick, orders loaded carriers deterministically by distance to their own
reactor and unit id, and lets newly spawned bodies prefer a non-reactor route
for only their first two life ticks. If no such route exists, the ordinary
planner is the immediate fallback. Its stock digest moves, so v16 starts fresh
rather than copying v15 Elo. v17 retains seen loose Cores at their last-known
tile for the authored Core-memory window, makes nearest-authorized friendly
drop recovery distance-first, and adds an optional custody pickup-selection
policy for ordinary Wells. The registered stock digest moves, so v17 starts
fresh rather than copying v16 Elo. v18 keeps an accidental carrier's later
drop claimable when it never committed to a custody policy, and excludes
already-loaded bodies from pickup reservations. The registered stock digest
moves again, so v18 starts fresh rather than copying v17 Elo. v19 promotes the
Tapline map and its native Hunter/Wellwright layouts. A different map is a
different competitive era, so v19 starts every opted-in entrant at the default
rating while retaining v18 as readable history. v20 introduced the Orders v2
sheet line on Tapline-1; v21 promotes Tapline-3 and starts a fresh rating book.
v22 promotes the combined Tapline-4 game—team-relative 1/2/4 Core charge,
veteran command, public respawn waves, a seven-distinct-class roster floor,
durable deployments, and scaled combat—and starts another fresh book with
Tempered Standard as its frozen stock sheet.
Completed historical broadcasts remain readable, but old Arc Relay playlist
jobs are replay-only rather than executable by a current worker.
ForwardCombat/Counterflow v6 ratings and matches remain readable history but
were not copied.

## Admission and composition

Both kinds snapshot eight unlocked classes with no more than two copies of one
class and at least seven distinct classes. Custom minds declare that
composition at submission and revision time;
the reserved adaptive-composition fields remain empty in v1. The server
validates declarations against current ownership before it accepts them.

A sheet is authored as the consolidated doctrine grammar: one ordered mode
ladder per role, narrow shared cohort counts and focus caps,
condition predicates, fight and custody policy, and map-plotted zones, routes
and anchors. The server stores the exact playbook and layout sources, validates
every save with the same compiler as the CLI, and snapshots the compiled package
into a match. Saving enters the sheet into the ladder by default, with an
explicit opt-out. A trial runs the current revision against a tracked stock
sheet and opens it in the ordinary replay viewer. v22's stock sheet is the
Tapline-native Tempered Standard Orders v2 sheet, and six additional named
strategy sheets are available as practice opponents. New map plans are authored directly
on the Tapline export; earlier-map sheets are shown as legacy and cannot enter
v22 until replotted and brought up to the current roster rule.
The server never projects coordinates between maps.

A custom mind must complete one hosted validation match without a runtime fault
before ladder opt-in. Ladder execution never trusts submitted code in-process.
Every sheet executes through the registered first-party tactical mind's trusted
in-process lane; its pinned artifact hash is version provenance, not a loaded
WASM module. A ranked match that trips a felt-degeneracy bar suspends the
entrant from pairing until a corrected revision is submitted and preflighted.

Completed historical matches are read from their stored broadcasts. Historical
playlist definitions remain available to interpret their identity, but current
workers do not claim unfinished jobs for those versions: running them through
today's in-process tactical mind would falsely rewrite historical behavior.

## Ranked lane

An account may opt in at most three entrants across both kinds. The background
pairer chooses different-account opponents by nearest rating, avoids recent
rematches where capacity permits, limits daily matches per entrant, and obeys
the existing admission locks and worker-capacity ceiling. An account's own
entrant-vs-entrant scrimmages are always unrated.

Ratings settle only after the causal broadcast is complete. Public ladder and
match surfaces expose no result-derived fact before that boundary. Every match
records the exact entrant revision, artifact/sheet hash, composition hash and
crest snapshot used for the match.

## Legacy retirement

The former Duel bot creation, submission and queue surfaces are retired. Server
admission is feature-gated off in production with an explicit retired response.
Historical bot pages, matches and replays remain available in a clearly marked
read-only archive. Engine identifiers, the nilbots brand and CLI, frozen
contracts, replay verification, and historical decisions are intentionally not
renamed or removed.
