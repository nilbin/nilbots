# Tapline-9 Mid-Well Ribs Promotion

Date: 2026-08-20
Branch: `main`
Decision: `docs/DECISIONS.md` #350

## Outcome

The owner-approved mid-Well wall-rib geometry is the current immutable Arc
Relay profile:

- loop profile: `tapline-9`;
- ruleset: `arc-relay-tapline-09`;
- map: `arc-relay-tapline-03`;
- hosted entrant playlist: v26, with a fresh 1200 rating book.

Tapline-8, map `arc-relay-tapline-02`, entrant playlist v25, and every earlier
profile remain registered and unchanged for historical matches and replays.
Tapline-9 carries Tapline-8's 1/2/4 scoring, exact-reactor banking, opening Well
timing, bank-triggered 46-tick source recharge, signatures, combat rules, and
600-tick horizon. Terrain is the only gameplay delta.

The Tactics work was incorporated first by fast-forwarding local `main` from
`716ee314` to `agent/tactics-editor` at `8ffda1a3`. It was not squashed,
rebased, or pushed.

## Geometry contract

Tapline-9 adds exactly these 14 walls over Tapline-8 and removes none:

```text
(12,11) (13,11) (14,11) (15,11) (16,11)
                                    (22,12)
(11,13)                         (19,13)
(8,14)
              (14,15) (15,15) (16,15) (17,15) (18,15)
```

The executable engine test pins the old and new fingerprints:

- Tapline-8: `e536d04deff7e53296d10f2b93c38741a6a3e465a03e779a84336ba0ae1df519`;
- Tapline-9: `27e0d0dd62544a9f77f3273f811a5171db1c0de84488af7cad92bfca9ce1e191`.

Independent candidate analysis and the engine contract agree on a 31x27 map,
509 open tiles in one connected component, exact 180-degree symmetry, all two
reactors/three Wells/two healing tiles open, no false diagonal corner passage,
no meaningful cut vertex under the two-tile stub allowance, and no candidate-
created interior leaf.

## Route-distance ledger

Distances are corner-strict, terrain-only steps. They describe geometry; they
are not forced routes for a mind.

| Route | Steps |
|---|---:|
| Own Well -> own reactor | 12 |
| Own Well -> Centre Well | 12 |
| Centre Well -> enemy Well | 12 |
| Centre -> own reactor, practical north | 24 |
| Centre -> own reactor, practical south | 24 |
| Own reactor -> enemy Well, direct | 28 |
| Own reactor -> Centre -> enemy Well | 36 |
| Enemy Well -> own reactor, direct | 28 |
| Enemy Well -> Centre -> own reactor | 36 |
| Centre -> own reactor, middle-shadow north/south | 31 / 47 |
| Centre -> own reactor, full-shadow north/south | 48 / 70 |
| Enemy Well -> own reactor, middle-shadow north/south | 43 / 41 |
| Enemy Well -> own reactor, full-shadow north/south | 56 / 58 |

The intended commitment is present: a reactor-origin Mid visit before an enemy-
Well rotation costs 36 steps, versus 28 for the dedicated outer push, while the
two practical Centre returns remain equal at 24.

## Current surfaces and sheets

`ArcRelayLoopProfile.Current`, CLI defaults/help, the hosted playlist seeders,
Tactics catalog/map export, review-map presentation, product docs, and the
exhibit harness now resolve Tapline-9. CLI compatibility moved to 0.9.66; a
future publish owes `cli-v0.9.66`. No release or deploy workflow ran.

The current authoring catalog is deliberately small:

- First Stand — recommended starter;
- Mid Pincer — advanced starter;
- Demolition March — first and primary practice opponent / deathball
  regression;
- Idle Field — deterministic movement and mechanics practice.

Demolition March has a Tapline-9-native centre-overwatch zone and enemy-centre
approach anchor. First Stand's posts/facing and both Tactics documents now bind
the new map. Stale experimental sheets were not preserved as current catalog
content.

## Match evidence

The standard six fixed side/seed cells were run in-process on Tapline-9.
Demolition March beat Mid Pincer in all six by reactor destruction at ticks
217, 194, 209, 204, 213, and 204. All six passed the replay-visible movement
sanity read: zero closed cycles, recovery no-progress episodes, false move
traces, contradictory traffic traces, local move orbits, held yields, and
carrier route trips. Two Mid Pincer cells nevertheless tripped the stricter
formation-freeze eligibility bar, so no eligible gallery was claimed.

Against First Stand, Demolition March again won all six at ticks 334, 244, 282,
319, 313, and 317. Five movement reads were clean; one First Stand Lantern made
a 17-tick local orbit while trying to re-enter its central post after combat.
That run was also rejected rather than presented as an eligible gallery.

This is deterministic regression evidence that the repaired Demolition March
still executes and applies a strong deathball on the new geometry. It is not a
balance verdict. In particular, the current starters do not yet demonstrate the
split/raid/interception counterplay that motivated the ribs, and should not be
advertised as doing so.

## Validation

Final command results are recorded here before the promotion commit:

- `dotnet build BotArena.sln`: PASS (0 warnings, 0 errors)
- `dotnet test tests/BotArena.Engine.Tests`: PASS (1,542/1,542)
- `dotnet test tests/BotArena.Cli.Tests`: PASS (305/305)
- `dotnet test tests/ArcRelayTacticalPlaybookMind.Tests`: PASS (76/76)
- `dotnet test tests/BotArena.Determinism.Tests`: PASS (17/17)
- `dotnet test tests/BotArena.App.Tests` (non-PostgreSQL): PASS
  (213 passed, 90 environment-gated skips)
- focused hosted App PostgreSQL integration smoke: PASS (1/1)
- Tapline-9 v26 fresh-rating PostgreSQL cases: PASS (6/6)
- `bash scripts/generate-api-clients.sh`: PASS (no generated drift)
- `cd web && npm test`: PASS (577/577)
- `cd web && npm run build`: PASS
- map-analysis regression: PASS (8/8); candidate analysis: PASS
- `git diff --check`: PASS

## Remaining limitations

- Six cells establish neither balance nor fun. Demolition March currently
  overwhelms both starter examples; a future owner-directed tuning pass should
  author a visible counter rather than weakening the regression opponent by
  accident.
- Mid Pincer's formation-freeze failures and First Stand's one post-rejoin orbit
  remain sheet/editor follow-up evidence, not map blockers.
- Playlist v26 intentionally starts a fresh rating era because the terrain
  changes strategic distances. It reuses the existing frozen trusted stock mind
  artifact and pins a Tapline-9-native map plan; no frozen artifact was amended.
