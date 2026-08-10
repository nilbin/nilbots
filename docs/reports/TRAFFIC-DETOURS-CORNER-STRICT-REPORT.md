# Traffic detours and corner-strict movement

Date: 2026-08-10  
Branch: `codex/traffic-detours-corner-strict`  
Base: `main@d8526966`  
Behavior commit: `6e89fbb6`  
Hosted artifact/playlist commit: `cc856f42`  
Pre-existing built-in fixture refresh: `59b12f46`

This is an executor/engine mechanism report, not a balance read. No battery or
sweep was run. No map, layout, sheet, grammar, class, rules definition, or
canonical contract content changed.

## Review exhibit

Public phone gallery:
<https://means-via-webcams-milton.trycloudflare.com>

The standard six cells were fixed before running, in their registered order:
`w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`. Hunter is the exhibited sheet;
Wellwright v1 is the actual opponent. Cards disclose the final score, trigger,
tactic, counterplay, fallback, and what to watch.

| Cell | Exhibited Hunter record | Completion |
| --- | --- | --- |
| w-9001 | loss | reactor destroyed, t357 |
| w-9002 | win | reactor destroyed, t485 |
| e-9005 | loss | reactor destroyed, t524 |
| e-9009 | win | reactor destroyed, t529 |
| w-9012 | win | reactor destroyed, t382 |
| e-9004 | loss | reactor destroyed, t365 |

These six records describe only the exhibit. They are not evidence that the
change is balanced or that Hunter is stronger.

Chrome loaded the public index at 390×844, found all six cards, loaded
`sample-01` and its replay payload, rendered a 364×227 gameplay canvas, and
started playback. There were no page or console errors. The six compressed
broadcasts are 135,076–200,525 bytes each. The served directory is 54 MiB
because it includes the shared WebGL model/texture/audio bundle once; replay
payloads remain independently compressed.

Both gallery processes remain detached and named:

- `nilbots-traffic-detour-gallery-8997`
- `nilbots-traffic-detour-tunnel-8997`

## 1. Bounded escape from a held route

The existing sticky-course fix was correct about ownership: a route direction
and lap must not be re-derived after every denial. It was too broad about what
was sticky. A legal terrain course also held an occupied next tile forever,
because only impossible terrain could re-plan it.

The direct-ladder executor now makes one bounded traffic attempt after the
authored stuck window:

1. Build a transient obstacle view from current bodies and same-tick claims,
   while retaining persistent terrain obstacles.
2. Search deterministically for a later open tile on the exact held terrain
   course.
3. Accept only a path whose extra length fits the route's authored
   `corridorWidth` budget.
4. Follow that temporary course, then resume the original course after the
   rejoin index. Direction, waypoint and lap are not reselected.
5. If the held course ends at an occupied waypoint, try only later authored
   waypoints in the same forward direction. The route cursor stays monotonic.
6. If neither attempt fits, wait. The executor does not phase, swap, ignore
   occupancy, or invent an unbounded path.

The causal vocabulary now includes:

- `route-detour-commit-rejoin-X-Y-next-X-Y`
- `route-detour-held`
- `route-detour-rejoined`
- `route-detour-waypoint-*`
- `route-detour-considered-wait-*`

Unit tests prove that a successful detour resumes the exact original course
without invoking its planning callback again, and that an over-budget detour
chooses an explicit wait. Existing sticky-course and oscillation tests remain
green.

### Tapline mechanism read

The owner-supplied prior read used an older blocked-wait classifier and recorded
52% wait / 65% blocked-wait on seed 9001 and 51% / 56% on seed 9003. To avoid
pretending a changed classifier is the same measurement, the tracked
`arc-relay-route-read.py` now publishes its exact denominator: comparable live
body-ticks are stationary when start/post positions match; a stationary tick
is traffic-blocked when the authoritative resolution is blocked or its causal
reason names a blocked or considered-wait route.

Re-reading the exact `d8526966` baseline and the new executor with that one
definition gives:

| Tapline seed | Variant | Stationary body-ticks | Blocked share of stationary | Worst one-tile run | Bodies over 100t |
| --- | --- | ---: | ---: | ---: | ---: |
| 9001 | `d8526966` | 4,104 / 8,081 (50.8%) | 1,953 / 4,104 (47.6%) | 227t | 1 |
| 9001 | detours | 3,683 / 7,616 (48.4%) | 1,461 / 3,683 (39.7%) | 30t | 0 |
| 9003 | `d8526966` | 3,446 / 6,614 (52.1%) | 1,588 / 3,446 (46.1%) | 35t | 0 |
| 9003 | detours | 3,554 / 7,678 (46.3%) | 1,416 / 3,554 (39.8%) | 30t | 0 |

Seed 9001 exercised 10 later-waypoint detours and 7 explicit detour waits;
seed 9003 exercised 13 detours and 5 waits. Neither after-cell has a long A-B
flap. Traffic-read flap shares stayed bounded (9001: 3.99% / 3.10%; 9003:
4.39% / 2.59%). The fix therefore bought a bounded escape without giving up
route commitment. Different end ticks make raw tick counts inappropriate for
outcome or pacing conclusions; the table is only a movement-mechanism read.

## 2. Authoritative corner-strict movement

`GenericActorMatchSession` now owns one terrain gate for voluntary movement.
A diagonal target is legal only when the target and both orthogonal companion
tiles are open terrain. Occupancy of a companion is deliberately absent: a
body is traffic, not a wall, and ordinary occupancy/claim arbitration still
runs after the terrain check. The direct engine regression pins open diagonal,
one-wall, two-wall, and cardinal cases without relying on a particular map.

This applies to the generic Arc Relay, Deathmatch and Frontline drivers. The
legacy 0.1–0.5 `MatchSession` path is untouched.

### Geometry audit and corrected premise

The brief said the `ambush-warren-11` grid had zero corner-touching diagonal
pairs. The code audit disproved that premise: its immutable historical Warren
geometry has 20. The contract fingerprints do not encode engine movement
semantics, so unchanged fingerprints are not proof that every possible custom
action stream is unchanged on that geometry.

A shared test now evaluates every registered Arc Relay profile with the
authoritative corner-strict step rule. H0, Drawbench and repaired Tapline pass
both authoring bars: no false corner passage and no stationary body severing
more than a two-tile nook. The immutable Warren family and candidate Crucible
are explicit known failures; no map was re-authored or silently exempted by
changing the movement rule.

Crucible's 14 failing corner pairs are:

```text
(9,3)-(10,2)    (12,3)-(13,2)   (14,7)-(15,8)
(5,9)-(6,8)     (12,10)-(13,11) (16,10)-(17,9)
(17,11)-(18,12) (17,11)-(18,10) (12,12)-(13,11)
(13,13)-(14,12) (15,14)-(16,15) (24,14)-(25,13)
(17,20)-(18,19) (20,20)-(21,19)
```

Crucible has no stationary-body cut larger than a two-tile nook. It remains a
candidate map and was not repaired in this task.

### Golden contract proof

The same ambush-warren-11 stock cell before and after retains all five exact
contract fingerprints:

| Component | SHA-256 before and after |
| --- | --- |
| rules | `99f411e18b40fe93eec75277c434edc2f2dcba2c1004203ea48e5e5eea017cb4` |
| map | `9fe9710576bdda54d145f08d4d872e2756cf7975b7494dd4ea11bb36573ef659` |
| format | `dc81a4f285ada9baceba99751e2de2ede8247cd943ad5c2164368c2f55129463` |
| topology | `e0b47ca09395c27110a1ce4f37a9744563f430ce8b56d275d0c59fe609e72254` |
| aggregate match | `a24ff0d31fc5675dcde0df2e137a96d342c94a2794a1438150d8f2b59c56d44b` |

The full engine suite, including all fingerprint goldens, passed unchanged.

## 3. Determinism and hosted identity

The controlled macOS Docker build produced:

- CLI: `0.9.44`
- SDK / Guest: `0.10.13`
- pipeline: `5`
- build input:
  `cf7edee8cf6c3239c0b3989ac20eea30bbddd8acf806ba46a032340e307d2e77`
- artifact:
  `4443b18bf718114db764a440604c7987751a7ad1c28fbe092862b4d4a7fa8af5`
- bytes: 8,673,836

In-process and sandboxed-WASM Tapline seed 9001 have the same gameplay-state
digest:
`c6073a4bb432b2993f1b17d4c51cf3ff1a9583b160e3c68386628b7cca250abc`.
Their outer replay hashes differ only because diagnostic provenance names the
runtime, as documented by the product invariant.

The registered Wellwright-vs-Wellwright ambush-warren-11 seed-9001 stock cell
also agrees cross-runtime at
`c356c5882fcd86c3646365b15147f18d8c8b494b33bc7e5b3a505f6ab91068a6`.
That is different from v14's registered
`4afb253b831b8d50a2cd04591eedd8b56393961c4d13ba6e72d0656fa5150c5f`.

Therefore v14 and `ambush-warren-11-stock-v8` remain immutable. Playlist v15
pins new `ambush-warren-11-stock-v9` and starts a fresh rating book. It closes
the open v14 ladder, seeds opted-in entrants at the default rating, and retains
v14 ratings/matches as readable history. It does not claim continuity on a
moved stock digest. The PostgreSQL integration test pins that transition.

## 4. Validation

- `bash scripts/test.sh`: green
  - Engine 1,459; CLI 286; Runtime.Wasm 70; Determinism 17;
    Guest 36; SDK 86; App non-PostgreSQL 202 passed / 85 skipped
  - all tracked Python/release/bootstrap/PgBouncer checks green
- PostgreSQL-required App suite in a disposable PostgreSQL 17 container:
  284 passed / 3 non-PostgreSQL-service skips
- `bash scripts/generate-api-clients.sh`: generated surfaces have zero diff
- `cd web && npm test`: 438 passed
- `cd web && npm run build`: production and all five scoped CLI viewers built
- controlled `nilbots build --no-cache`: current mind receipt and frozen v9
  bytes agree
- public 390×844 Chrome index + replay + playback smoke: green, zero errors
- `git diff --check`: green

The full suite also found a pre-existing tracked-fixture omission: SDK contract
reader changes in `a3ed3289` had not refreshed `artifacts/wasm/builtin-bots.*`.
The input-stamped build deterministically refreshed it; the change is isolated
in commit `59b12f46`, and the complete 70-test WASM suite passed against it.

No release or deployment workflow ran. `publish-cli` owes tag
`cli-v0.9.44` on the eventual merged revision before any deployment.
