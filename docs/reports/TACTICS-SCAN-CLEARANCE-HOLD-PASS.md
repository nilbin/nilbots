# Tactics Scan, Clearance, and Hold Pass

Date: 2026-08-20

## Result

Tactics phases can now prove that an area was actually checked, distinguish a
quiet area from one with an active local fight, and defend authored Hold ground
without freezing the body's facing. The implementation preserves the existing
Tactics/phases architecture and the single top-to-bottom Instruction priority.
It adds no Tactics job type, Program runtime, scheduler, state machine, or scan
allocator.

The map editor now presents the selected phase at full strength, its immediately
previous and next phases dimmed for context, and hides more distant phases by
default. **Show every phase** is independent of **Show every Tactic** and reveals
the distant phases more faintly.

## Instruction and evidence semantics

### Scan

`scan` is an advanced Orders action, not a player-facing Tactics job and not a
new engine action. It accepts a zone and freshness window and resolves through
the existing rotate, move, and hold commands.

- Visibility is computed from the real map, walls, cardinal facing, and the
  body's vision profile.
- A useful rotation in place is preferred over movement.
- Otherwise the body moves toward a legal viewing position that refreshes stale
  zone tiles.
- A useful viewing target is retained until satisfied, invalid, unreachable, or
  pre-empted, avoiding target orbiting.
- Existing proposals, capacity allocation, movement reservations, and
  top-to-bottom Instruction priority are reused.
- Capacity is two scanners per zone and one scanner per viewing tile.
- Custody, urgent recovery, active Fight, and committed signatures remain above
  generated Scan; passive positioning resumes beneath it.
- Debug reasons distinguish rotating, moving, holding, and satisfied scans.

A positive `cleared` condition on a phase transition generates visible Scan
Instructions only for phase groups whose effective ground overlaps or touches
the clearance zone. Validation rejects a clearance zone with no local group.
Start and abort conditions remain observation-only and never recruit scanners.

### Trustworthy clearance

`cleared: { zone }` compiles to the ordinary `zone-cleared` fact. It is true only
when every walkable tile in the zone was observed by team vision during the
rolling last 20 ticks and no unresolved live-enemy observation remains there.
An enemy memory is resolved by a later observation outside the zone, confirmed
destruction, a later observation of its last-known tile as empty, or expiry from
the freshness window. Unseen, partially seen, and stale areas are not clear.

Player copy is **Area is cleared of enemies** with the readback: “Your team
checked the whole area within the last 20 ticks and no enemy is known to remain
there.” The old instantaneous meaning is no longer presented as clearance.

### Zone-bounded fighting

`notFighting: { zone }` compiles to `friendly-fights-in-zone == 0`. It is false
when an active friendly Fight Instruction intersects the zone through its
fighter, selected hostile target, or pending/committed strike. Rotation and
weapon cooldown inside an ongoing Fight still count; enemy visibility alone
does not. An unrelated fight elsewhere does not block it.

The fact is derived from the existing selected-Instruction and pending-strike
state at the normal decision boundary. It intentionally has a one-decision
observation delay rather than introducing parallel combat state. In the editor,
adding `cleared` or `notFighting` reuses the other row's zone by default.

### Hold defence

Hold now means retain assigned ground, not retain authored facing. An
empty-handed holder rotates toward, tracks, and fires on an actionable enemy
from any direction while remaining on its exact post or inside its Hold ground.
Combat facing wins over Scan and resting facing during rotation and weapon
cooldown. The authored resting facing resumes after combat. Hold does not chase
outside its ground, and Core carriers retain the existing non-fighting rule.

## Deterministic proof

Focused tests cover:

- unseen, partial, expired, and fresh full zone coverage;
- unresolved enemy memory and resolution by outside sighting, destruction, and
  re-observing the last-known tile empty;
- rotation-first Scan, movement when rotation is insufficient, sticky viewing
  targets, distinct target capacity, and the two-scanner zone cap;
- generated phase-transition Scan Instructions, local-group validation, normal
  Instruction ordering, and no Scan generation for start/abort conditions;
- local Fight intersections through fighter, target, and strike, unrelated
  fights, ongoing rotation/cooldown, and fight completion;
- Hold attacks from front, rear, north, and south, exact-ground retention,
  tracking, cooldown-facing retention, resting-facing restoration, and the
  carrier exception.

The executor remains deterministic: all selection and tie-breaking uses stable
positions, facing order, source priority, and unit identity; it adds no random
choice or parallel allocator.

## Browser and replay evidence

A production-build browser smoke opened the persisted **Test 1** sheet. It
verified the independent phase visibility control, selected/adjacent/distant
phase treatment, exact clearance help, `notFighting`, shared-zone reuse, and the
two map labels. The draft was cancelled, so the persisted sheet was not changed.

Diagnostic gallery:

https://diversity-breed-perceived-guides.trycloudflare.com

This is the standard six side/seed cells for Mid Pincer versus First Stand on
Tapline-9. It is explicitly a diagnostic exhibit, not balance evidence. The
eligibility gate was bypassed because four Hold-oriented cells trip the existing
formation-freeze/passivity gallery bars; that is disclosed here rather than
interpreted as a fun or balance result. All six cells passed motion sanity and
completed by reactor destruction at ticks 424, 505, 425, 424, 426, and 425.

Observed Scan actions:

| Cell | Scan actions | Tick span | Move | Rotate | Wait | Max scanners/tick |
| --- | ---: | --- | ---: | ---: | ---: | ---: |
| west / 9001 | 199 | 40–296 | 172 | 17 | 10 | 2 |
| west / 9002 | 248 | 40–505 | 219 | 15 | 14 | 2 |
| west / 9012 | 149 | 40–168 | 128 | 18 | 3 | 2 |

The mirrored east cells did not enter the phase carrying the clearance
transition, so they correctly emitted no Scan action. The trace at tick 40 in
west/9001 records a stale/unresolved clearance read followed by
`move-to-view`; tick 296 records a rotation refresh. These are action-level
mechanical observations, not an outcome claim.

## Verification

- `dotnet build BotArena.sln --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test BotArena.sln --no-build --no-restore`: 2,368 passed, 91 skipped,
  0 failed. Skips are the existing opt-in App database/service cases.
- `cd web && npm test`: 580 passed.
- `cd web && npm run build`: passed, including the production site and six
  scoped viewers.
- `git diff --check`: passed.

CLI/toolchain version advances from 0.9.66 to 0.9.67 because the controlled
compiler/runtime input changed. No release or deployment workflow was run;
`publish-cli` owes the `cli-v0.9.67` tag before publishing that version.

## Known limits

- `notFighting` observes selected Fight state at the next normal decision
  boundary, so it can lag a newly selected or ended fight by one decision.
- Tactics generates Scan only for positive phase-transition clearance checks.
  Start/abort clearance remains observation-only by design.
- At most two bodies scan one zone at a time. Other bodies continue their
  ordinary phase Instructions.
- This pass does not add minimum phase duration, new phase jobs, map balance
  changes, or a second priority/runtime system.
