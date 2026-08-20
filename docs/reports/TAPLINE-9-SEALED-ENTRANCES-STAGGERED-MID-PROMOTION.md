# Tapline-9 Sealed Entrances with Staggered Mid Cover Promotion

Date: 2026-08-20
Branch: `main`
Decision: `docs/DECISIONS.md` #350

## Outcome

The unpublished Tapline-9 promotion was corrected in place to the
owner-approved sealed outer-Well entrances with staggered Mid cover:

- loop profile: `tapline-9`;
- ruleset: `arc-relay-tapline-09`;
- map: `arc-relay-tapline-03`;
- hosted entrant playlist: v26, with a fresh 1200 rating book.

Tapline-8, map `arc-relay-tapline-02`, entrant playlist v25, and every earlier
profile remain registered and unchanged for historical matches and replays.
No Tapline-10 or playlist v27 was created. The previously staged 14-wall
candidate was neither published nor deployed and is not combined with this
map. Tapline-9 keeps Tapline-8's 1/2/4 scoring, exact-reactor banking, opening
Well timing, bank-triggered 46-tick source recharge, signatures, combat rules,
and 600-tick horizon. Terrain is the only gameplay delta.

The previously merged Tactics work at `8ffda1a3` remains intact; this correction
does not redesign or revert it.

## Geometry contract

Tapline-9 adds exactly these six walls over Tapline-8 and removes none:

```text
(12,9) (15,9) (13,12)
(17,14) (15,17) (18,17)
```

The executable engine test pins the old and corrected fingerprints:

- Tapline-8: `e536d04deff7e53296d10f2b93c38741a6a3e465a03e779a84336ba0ae1df519`;
- Tapline-9: `935cdb4225498db195145c3cd462a6907f0be8ec8bff723b537189024b4c98be`.

Independent candidate analysis and the engine contract agree on a 31x27 map,
517 open tiles in one connected component, exact 180-degree symmetry, all two
reactors/three Wells/two healing tiles open, no false diagonal corner passage,
no meaningful cut vertex under the two-tile stub allowance, and no candidate-
created interior dead-end branch beside either own-side Well. The terrain has
no `A` scoring/drop-zone landmark; banking remains possible only on the exact
reactor tile.

## Route-distance ledger

Distances are corner-strict, terrain-only steps. They describe geometry; they
are not forced routes for a mind.

| Route | Steps |
|---|---:|
| Own Well -> own reactor | 12 |
| Own Well -> Centre Well | 22 |
| Own Well -> Centre via healing side | 22 |
| Centre Well -> enemy Well | 22 |
| Centre Well -> enemy Well via healing side | 22 |
| Centre Well -> own reactor, fast return | 19 |
| Centre Well -> own reactor, healing-side return | 29 |
| Own reactor -> enemy Well, dedicated route | 31 |
| Own reactor -> Centre -> enemy Well | 41 |
| Enemy Well -> own reactor, direct | 31 |
| Enemy Well -> Centre -> own reactor | 41 |

## Current surfaces and Tactics

`ArcRelayLoopProfile.Current`, CLI defaults/help, hosted playlist v26, the
Tactics catalogue/map export, review-map presentation, product docs, and the
exhibit harness resolve the corrected Tapline-9 identity. The playlist pins the
corrected native Tempered Standard map plan with source hash
`07e7951b4aabb5c596badbfb48e60edacf337c0a2172c5928ac8c4f7bde278ba`.
CLI compatibility remains 0.9.66; a future publish still owes
`cli-v0.9.66`. No release or deploy workflow ran.

The current catalogue remains deliberately small:

- First Stand — recommended starter;
- Mid Pincer — advanced starter;
- Demolition March — primary practice opponent and deathball regression;
- Idle Field — deterministic movement and mechanics practice.

First Stand's two wall posts were moved off the newly sealed tiles and their
facings repaired. Tempered Standard's stock cover zone was moved off a sealed
tile. First Stand and Mid Pincer were freshly materialized against the corrected
map. Mid Pincer, Demolition March, and Idle Field compile and resolve without an
exact post, route waypoint, or anchor landing on a wall. Demolition March itself
was deliberately left behaviorally unchanged: it remains the primary deathball
regression opponent rather than being tuned to make this map promotion look
better. Experimental legacy sheets were not preserved as current catalogue
content.

The synthetic signature stage follows `Current`, so its caster/Exchange staging
positions were moved off the newly sealed tiles while preserving the same
mechanical evidence. The Core-custody presentation stage remains explicitly
pinned to Tapline-8 and was not moved onto Current.

Local persistent databases may already contain the rejected, unpublished v26
row. The entrant seeder recognizes only that exact rejected definition
fingerprint, refuses correction if any match or match-set history exists,
replaces its executable definition in place under the database immutability
guard, and immediately restores normal immutable validation. A PostgreSQL
integration test pins this one-time correction; every other contradiction still
fails closed.

## Match evidence

The standard six side/seed cells ran in-process on the corrected Tapline-9 with
the unchanged Demolition March against freshly materialized Mid Pincer.
Demolition won all six by reactor destruction:

| Cell | End tick | Movement sanity |
|---|---:|---|
| `w-9001` | 269 | clean |
| `w-9002` | 262 | clean |
| `e-9005` | 280 | rejected: one 18-tick Raider local orbit |
| `e-9009` | 257 | rejected: one 18-tick Raider local orbit |
| `w-9012` | 262 | clean |
| `e-9004` | 280 | rejected: one 18-tick Raider local orbit |

The three rejected cells expose the same mirrored behavior from tick 30 through
47: Demolition team 1 unit 7, under `raid-conversion`, remains within six tiles
while moving toward the enemy Well. There are no closed cycles, recovery
no-progress episodes, false move traces, contradictory traffic traces, held
yields, or carrier-route trips in any cell. The repeatable local orbit is still
a real movement/sheet finding and was not waived. Because the frozen movement
gate rejects those three cells, no owner gallery or public URL is claimed.

These matches establish that the four current catalogue inputs compile on the
corrected geometry and that Demolition reaches terminal reactor destruction on
both seats. They do **not** establish balanced deathball counterplay or prove
the map solved the deathball problem: Demolition won every cell in this small
exhibition, and the east-seat Raider orbit remains disclosed follow-up evidence.

## Validation

- `dotnet build BotArena.sln --no-restore`: pass, zero warnings/errors;
- `dotnet test tests/BotArena.Engine.Tests --no-build`: 1,542 pass;
- `dotnet test tests/BotArena.Cli.Tests --no-restore`: 305 pass;
- `dotnet test tests/ArcRelayTacticalPlaybookMind.Tests --no-build`:
  76 pass;
- `dotnet test tests/BotArena.Determinism.Tests --no-build`: 17 pass;
- PostgreSQL-required `BotArena.App.Tests`: 301 pass, 3 intentionally skipped
  external S3/toolchain cases;
- `cd web && npm test`: 577 pass;
- `cd web && npm run build`: pass;
- map-analysis regression tests: 8 pass;
- candidate analyzer: valid, 517 reachable floor tiles, zero structural errors;
- generated API clients: no endpoint change and no generated diff;
- `git diff --check`: pass.

## Remaining limitations

- Six exhibition cells cannot establish balance or fun. Any observed Demolition
  outcome is reported as evidence of what the current sheets do, not proof that
  the map solves deathball balance.
- Demolition's mirrored east-seat Raider approach orbit remains sheet/executor
  follow-up evidence. The movement gate was not loosened to admit it.
- Playlist v26 intentionally starts a fresh rating era because the terrain
  changes strategic distances. It reuses the existing frozen trusted stock mind
  artifact and pins a corrected Tapline-9-native map plan; no frozen artifact
  was amended.
