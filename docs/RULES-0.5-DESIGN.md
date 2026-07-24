# Rules 0.5 design — the watchability slate (cone vision + projectiles)

Deliberation before implementation, per the 0.3 precedent. Root-cause
thesis (GAME-DESIGN, gen-5 findings): every degenerate equilibrium on
record — energy disarmament, the statue/dance, 0-for-122 sprays — stems
from **perfect information + perfect reaction**: all wind-ups are seen,
all seen shots are dodged, so initiating is negative-EV and optimal play
converges to stillness. 0.5 attacks information (cone vision) and gives
missed shots value (projectiles as zoning). Spectator payoff is a design
goal, not a side effect: both mechanics are visible state (constraint §3).

Acceptance spec = the executable-plays catalog (GAME-DESIGN): Backstab,
Red-Light Approach, Decoy Shot, Corner Flush, Shepherd, Vanish, Vanguard
Push, Double-Lane Squeeze; anti-plays: Radar Statue, the gen-5 fortress
90-0 freeze.

## A. Cone vision

- **Cone**: the 90° quadrant in the facing direction — tile (dx,dy)
  relative to the bot is in-cone iff `forward ≥ 0 && |lateral| ≤ forward`
  (facing East: `dx ≥ 0 && |dy| ≤ dx`; diagonal edges included). Own tile
  is trivially in-cone.
- **Proximity ring**: Chebyshev ≤ 1, omnidirectional — point-blank is
  never blind; melee-range play stays sane.
- Range stays 6 Chebyshev; corner-strict LOS unchanged; visibility =
  (cone ∪ ring) ∩ range ∩ LOS. The observation SHAPE is unchanged —
  `visibleTiles` just shrinks — so the wire protocol and every existing
  bot keep running (they simply see less: the compat mode is "myopic",
  not broken).
- **Hearing** (forced by the Decoy Shot play): without out-of-cone
  signals, cone vision starves bots into random wandering; with
  omniscient events it is undermined. Middle (hardened per §H item 1):
  **loud events (Shot, Damage, Destroyed) beyond sight but within
  Chebyshev `HearingRadius` arrive REDACTED as sounds** — event type, an
  8-way bearing octant (cardinal only when one axis dominates by more
  than 2:1), and a distance band (near ≤2 / medium ≤5 / far) — never
  coordinates, slots, or outcomes. The sound sits at the event's PRIMARY
  position (the muzzle, the victim). Disqualification is not loud: no
  world position, and it ends the match (§I). A sighted event is a full
  event and never also a sound — and under the cone, "sighted" means the
  ACTOR's tile is visible: seeing a ray's endpoint is not seeing the
  gun, so an impact from an unseen shooter degrades to a sound too (§I).
  Quiet events (Turn, Move, MoveBlocked) stay sight-gated.
  `HearingRadius = 8` (= ShotRange: you hear as far as guns reach).
  Sound is a cue and a decoy channel, not a radar — the v1 behavior
  (full authoritative events through walls) was radar and is retired
  with the v1 arms.
- Why 90° and not 180°: turning is 90°/tick, so a spinner sweeps the full
  circle in 4 ticks. At 180° the sweep closes in 2 ticks and stalking
  (Red-Light Approach) dies; at 90° a blind arc always exists, corner
  holds cannot watch two approach lanes (Corner Flush stays live), and
  the spin-scan trade is real: eyes and muzzle are one resource — a
  scanning defender's gun points wherever it is looking.
- GameRules: `bool VisionCone`, `int HearingRadius` (0 = off).

## B. Projectiles

- `int ProjectileTicksPerTile` (0 = instant rays, the legacy behavior;
  the arm ships 2 — one tile per two ticks, deliberately slow: bolts are
  zoning tools, and slow is the spectator-legible variant).
- **Lifecycle**: a validated Shoot spawns a bolt on the first tile in the
  shooter's facing during the shooting step (post-move board). Spawn-tile
  checks: wall → despawns (the point-blank wall shot stays a no-op); an
  active non-owner bot → immediate hit (point-blank stays effectively
  instant). Afterward the bolt OCCUPIES its tile — every tick, any active
  non-owner bot sharing a bolt's tile is hit (walking into a bolt is
  lethal; that is the lane-denial) — and advances one tile every
  `ProjectileTicksPerTile` ticks, despawning on walls or after
  `ShotRange` tiles.
- **Resolution order** (within the §4.7 tick): turns → bot moves →
  for each existing bolt: occupancy hit-check against post-move
  positions, THEN advance (phase-due), THEN occupancy hit-check again →
  new shots spawn (+ spawn checks) → all damage lands simultaneously.
  The double check (hardened per §H item 2) closes the phase-surfing
  gap: stepping onto a bolt's tile is lethal even on exactly the tick
  the bolt advances away. Crossing bolts pass through each other
  (collision is a future dial — Vanguard Push note); a bolt never hits
  its OWNER (Vanguard Push requires overtaking your own slow bolt).
- **Cooldown/energy semantics unchanged** — Shoot is still Shoot; no
  action parameters, no protocol bump. Damage events fire on hit with
  the existing shape; the Shot event marks the launch (to = spawn tile).
- Replay: per-tick `projectiles` list (x, y, direction, owner,
  ticksUntilAdvance, remainingTiles) — omitted (null) under instant-ray
  rules, so all historical hashes stand. Observation: trailing `P`
  section with visible bolts carrying the same six fields, sight-gated
  like everything else. Dodge timing is COMPUTABLE, never measured
  (§H item 2): `ticksUntilAdvance == 1` means the bolt moves this very
  tick, right after movement; `remainingTiles` is its residual range
  (−1 = uncapped), lethal on the final tile.

## C. Squeeze math — why bolts alone don't evict, and what does

With TicksPerTile 2, a bolt fired from (6,5) eastward at tick t spawns
directly on (7,5) during t's shooting step and occupies it for t..t+1,
then (8,5) for t+2..t+3: each zone tile is hot for 2 ticks, sweeping
west→east, with the first tile hot the moment the trigger is pulled
(§H item 2 fixed an earlier off-by-two here — prose only, the
implementation and its tests always had spawn-at-t). Tick-table the 2×2 camper against a two-bolt volley
(rows y=5 then y=6, second bolt ~3 ticks later after turn-move-turn):
the camper **counter-surfs** — it steps east-to-west *behind* the sweep,
returning to already-swept tiles, staying on zone throughout. Slower
bolts widen each window but also space the instants — surfable either
way. Conclusion, recorded honestly: **pure bolt fire cannot evict a
correctly-surfing camper from a 2×2 zone; what it does is make the
camper's position deterministic.** The executable Double-Lane Squeeze is
therefore bolts + body: two bolts comb the zone, the attacker walks onto
the forced refuge tile — the camper is hit, displaced off-zone, or
contested (accrual frozen) with its dodge lanes closed. Acceptance test:
against both canonical camper policies (hold-then-flip, counter-surf)
the scripted sequence must end in a hit or ≥N contested/off-zone ticks.
This is also the Shepherd play in its purest form.

## D. Spawn-fairness fallback fix (rides along)

Gen-5 finding #1: SpawnVariation's 64-attempt sampler can exhaust and
fall back to map-fixed spawns, silently bypassing ZoneSpawnFairness.
The v1 mitigation (`SpawnAttempts` 256) shrank the window but kept the
silent fallback; the hardened fix (§H item 3) removes sampling from the
arms entirely: **`ExhaustiveSpawns` enumerates every ordered floor pair
satisfying ALL constraints** (min distance, connectivity via one
component labeling, lane safety, zone-distance fairness) in canonical
scan order and lets the seed pick one uniformly. No attempt budget, no
fallback; a map whose valid set is empty is rejected loudly
(`SpawnVariationTests` gates every shipped map under every arm). Legacy
rules keep the sampler bit-identically.

## E. Arms and versions

Hardened revision **v3** (DECISIONS #59-#60) — every arm shares redacted
hearing, double-check collision, computable bolt timing, exhaustive
spawns, per-tick replay zone tallies, AND the seed profile
`0.5-exp-shared`: spawn selection and per-bot RNG streams derive from
the profile, not the arm's version string, so the same map + seed gives
every arm identical starting geometry and identical bot streams — a
paired A/B row differs only by the tested mechanic (§I finding: the
version-salted spawn seed silently unpaired the harness).

| arm         | version string          | on top of 0.4                          |
| ----------- | ----------------------- | -------------------------------------- |
| 0.5-control | 0.5-exp-control-v3      | matched baseline only (§H item 3)      |
| cone        | 0.5-exp-cone-v3         | VisionCone + HearingRadius 8           |
| bolts       | 0.5-exp-bolts-v3        | ProjectileTicksPerTile 2               |
| conebolts   | 0.5-exp-conebolts-v3    | both                                   |
| conebolts1  | 0.5-exp-conebolts1-v3   | both, bolts at movement speed (§G counter-tune) |

Earlier revision strings are retired, not preserved: experiments carry
no bit-compat promise, and gen-6 artifacts cannot parse the widened `P`
section regardless (their stored replays remain viewable; only re-
verification by re-simulation is lost). Rules 0.1–0.4 stay bit-identical
(all new code behind flags; full suite + goldens pass untouched).
SDK/GuestAdapter 0.6.0: `HeardSounds` (trailing `H` section, additive)
and the 4→6-field `P` section (breaking for 0.5.0 adapters only).

## F. Evaluation plan

1. Play-acceptance engine tests (Backstab, squeeze-vs-both-policies,
   hearing, cone geometry, projectile lifecycle) — these gate merge.
2. Mechanical harness: 4 arms (0.4 control / cone / bolts / conebolts),
   gen-5 population as cone-blind, bolt-blind controls — measures
   mechanical effect only (the 0.2-energy lesson: unaware populations
   answer "does it break the old game", not "is it good").
3. The real verdict: a gen-6 tournament of bots WRITTEN for 0.5
   (scouting, feints, suppression) — run separately (owner will drive
   it on a different model). Ship decision follows the 0.3/0.4 pattern:
   pre-registered criteria, plays observed in ranked replays, winners
   ship individually unless the combo proves genuine.

## G. Failure modes to watch

- Slow-bolt kinematics (owner question): at 1 tile/2 ticks a bolt never
  catches a fleeing bot and cannot hit a seen, unconstrained crosser —
  intended (bolts zone, cone kills). The direct-hit paths that remain:
  approach interception (closing speed 1.5 tiles/tick — doorways become
  defensible), the solo lane-web (cooldown 2 + ~14-tick flight = up to
  FIVE bolts in flight from one shooter: rotational sustained fire is
  area control without a prepared ambush), constrained targets (zone
  campers are semi-stationary by choice — that is the anti-camp
  mandate), and unseen launches under the cone. Gen-6 failure signature:
  bolt hit-rate ~0 outside ambushes AND falling shot counts (fire judged
  pointless again). Counter-tune ready: ProjectileTicksPerTile = 1 as a
  harness arm (equal-speed bolts: thinner denial, halved prediction
  horizon).

- Cone alone: mutual blindness could produce wander-draws with unaware
  bots (expected in the harness; gen-6 decides).
- Bolts alone: seen bolts are easier to dodge than rays — pacifism could
  deepen without the cone (the pairing hypothesis).
- Spin-scanning at 90°/tick restores ~omniscience with 4-tick latency;
  if that latency proves sufficient for free defense, the Radar Statue
  anti-play fails and cone needs a rethink (slower turns? scan cost?).
- Hearing radius 8 on small maps (12×8) ≈ map-wide — the Decoy Shot may
  be free information there; acceptable for 0.5, map-size dependent
  tuning noted for later.

## H. External review (Sol, 2026-07-23) — dispositions

Verdict accepted: **0.5 stays experimental; none of the arms promote to
official until the items below land.** Adopted as the pre-official-0.5
program (DECISIONS #58). Gameplay-affecting fixes ship as ONE coherent
hardening batch with a single version-string bump per arm (the hill
v1→v2→v3 precedent: behavior changes under a version string require a
new string — never mutate in place).

1. **Hearing is currently radar — AGREED, fix in batch.** Confirmed in
   code: `IsLoud` events inside HearingRadius are delivered as full
   authoritative GameEvents (exact shooter slot, coords, destination,
   hit/target, health) — at radius 8 on small maps that is a global
   tracking feed, undermining the cone it was meant to complement.
   Redaction design: a `HeardSound` record carrying only event Type, a
   coarse relative bearing (8-way octant from the listener), and a
   distance band (near ≤2 / medium ≤5 / far ≤8) — enough for the Decoy
   Shot and "someone is fighting north of me", not enough to aim.
   Heard-only events leave `VisibleEvents`; sighted events stay full
   (you SEE those). New trailing protocol section + SDK field.
2. **Projectile hidden state — AGREED, fix in batch.** `ObservedProjectile`
   gains `TicksUntilAdvance` and `RemainingTiles` (dodge timing must be
   computable, not measured); replay projectiles gain the same so the
   viewer can telegraph advances. The `P` protocol section grows 4→6
   fields — breaks gen-6 experiment bots' parsers; acceptable for
   experiment artifacts, documented. The **phase-surfing edge** (a bot
   entering a bolt's pre-advance tile on its advance tick survives) is
   real — occupancy is checked only post-advance. Fix: check before AND
   after advancement. The §C counter-surf conclusion survives (the
   counter-surfer enters tiles vacated on EARLIER ticks —
   `Squeeze_CounterSurf_SurvivesBoltsAlone` pins this and must still
   pass). §C prose timing corrected in place (spawn at t, not t+2 —
   prose-only error).
3. **Spawn fallback + experiment confound — AGREED.** The 64→256 raise
   shrinks the fallback window but keeps a silent unfair fallback.
   Batch fix: exhaustive deterministic enumeration — precompute the
   valid pair set (connectivity + lane safety + zone-distance fairness),
   seed-derived pick from it; if the set is EMPTY the map is rejected
   loudly, never silently unfair. Landed now (this commit): the
   **0.5-control arm** (`0.5-exp-control` = 0.4 + SpawnAttempts 256),
   so every A/B delta is measured against a spawn-matched baseline —
   the old 0.4-control-at-64 comparison confounded spawn-sampler and
   mechanics effects.
4. **Play tests prove mechanics vs passive defenders, not vs competent
   defense — AGREED, tests in batch.** New adversarial acceptance tests:
   optimal 4-tick scanner as the Radar Statue (does the anti-play
   actually break it?), Red-Light Approach vs that scanner, Corner
   Flush vs wall-backed defender, Shepherd forced-dodge window, Vanish,
   Vanguard Push counterplay, the gen-5 fortress geometry under
   conebolts, and the full §C squeeze (bolts + body) against both
   camper policies.

Evaluation upgrades (batch): paired per-scenario analysis in
balance-eval.py (same population/map/seed across arms — per-game
deltas, not aggregate rates); a speed-1 bolt arm in the aware
evaluation (final arm set: 0.5-control / cone / conebolts / conebolts-1
per §G's counter-tune); and pre-registered ship criteria, frozen before
gen-7 runs:

1. conebolts beats the spawn-matched control on paired decisiveness.
2. The Radar Statue is breakable by at least one executable play, shown
   in an adversarial test AND observed in a ranked replay.
3. Aware bots actually fire — suppression/feints appear unprompted.
4. Shot count does not collapse vs 0.4 baseline.
5. Hit rate is meaningful beyond point-blank (bolts land at range ≥2
   outside scripted ambushes).
6. ≥2 distinct doctrines viable at the top (no forced monoculture).
7. Median duration does not regress past the 0.4 accepted trade.
8. The gen-5 fortress scenario is breakable under the new rules.
9. Hearing produces uncertainty behavior (search/reorient), not
   tracking behavior (beeline to exact coords).
10. Each mechanic individually justifies its complexity via paired
    results (cone alone, bolts alone) — the combo ships only if the
    pairing hypothesis holds.

Sequencing: hardening batch → gen-6 DX docs/tooling pass
(DX-FINDINGS-GEN6: player rules card, `replay --summary` cone/bolt
columns) → gen-7 aware tournament under the final arms = the official
0.5 ship decision.

### §H status (hardening batch, DECISIONS #59)

- Item 1 hearing redaction: **DONE** (HeardSound = type + octant + band;
  ConeVisionTests/HearingTests pin delivery, redaction, dedup, cutoff).
- Item 2 projectile state: **DONE** (6-field observations + replay,
  double-check collision; ProjectileTests pin the surf hit and that the
  exposed timing predicts the actual advance; §C prose fixed earlier).
- Item 3 spawns: **DONE** (ExhaustiveSpawns everywhere in v2; loud map
  rejection; every shipped map × every arm gated by test; the
  spawn-matched 0.5-control arm is the harness baseline).
- Item 4 adversarial tests: **DONE for scriptable defense**
  (AdversarialPlayTests): open-field Radar Statue detection theorem
  (straight stealth approach impossible — sweeps beat walkers — but the
  sweep has exploitable latency), cover-timed Red-Light backstab kills
  the optimal scanner without ever being seen before the first hit,
  bolts+body squeeze denies a PERFECT timing-aware dodging camper (and
  bolts alone still don't kill it — §C honesty holds), Vanish breaks
  contact laterally inside vision range, and the gen-5 fortress freeze
  breaks via doorway bolts + Vanguard entry. NOT yet pinned: the
  armed door-watcher (a defender that shoots back) and Shepherd's
  follow-up-shot timing — those need adaptive play and are exactly what
  gen-7's aware bots + ship criteria #2/#8 evaluate.
- Evaluation: paired per-game transitions vs control landed in
  balance-eval.py; conebolts1 (speed-1) is a first-class arm; the ten
  ship criteria above are frozen.

## I. Follow-up review (Sol, 2026-07-23) — dispositions

Verdict accepted: hardening approved for gen-7; one experimental-design
flaw and two cleanup items, all fixed in revision v3 (DECISIONS #60):

1. **The control arm was not actually matched per game — CONFIRMED,
   fixed.** Spawn seeds derived from `RulesVersion`, which differs per
   arm, so the "same game under different rules" rows of the paired
   harness had DIFFERENT starting geometry — a transition could be
   mechanics, spawns, or both. Worse than reported: per-bot RNG streams
   were version-salted too (`DeriveBotSeed`), so even spawn-matched
   games would diverge for any bot that rolls dice. Fix:
   `GameRules.SeedProfile` (null = RulesVersion, the historical
   behavior) feeds BOTH derivations; all v3 arms share
   `0.5-exp-shared`. Consequence, pinned by test: a mechanics-blind bot
   plays the bit-identical game under every arm, so every paired
   transition is caused by the tested mechanic. The v2 paired table
   (18 games, blind champions) is VOID — re-run under v3.
2. **Disqualification could not be heard — CONFIRMED, resolved by
   de-louding.** The event has no world position (nothing physical
   happened anywhere) and a disqualification ends the match on the same
   tick, so a sound could never inform a decision. `IsLoud` drops
   Disqualified; docs updated. (The alternative — positioning the
   event — would change 0.4 replay bytes for DQ games; not worth it for
   a sound no one can act on.)
3. **A seen impact revealed the unseen shooter — CONFIRMED, fixed under
   cone rules.** The any-reference visibility rule delivered the full
   Shot event (exact shooter tile + slot) when only the ray's endpoint
   was visible — including a shot in the back landing ON the observer.
   Under `VisionCone`, full delivery now requires the event's PRIMARY
   position (the actor) to be visible; otherwise the event degrades to
   a HeardSound located at that primary position (bearing to the
   muzzle, not the impact). Omnidirectional rules keep the legacy
   any-reference rule bit-identically — there the shooter would be
   visible at those ranges anyway, and 0.4 must not change.

## J. Gen-7 redesign — active control + fast bolts (revision v4)

Gen-7 validated cone vision and redacted hearing, but rejected the
anti-camping thesis of slow bolts. Bastille's unchanged diagonal mirror
remained self-sufficient: a bolt forced movement, but movement did not
cost zone progress. The correction changes the defender's reward loop
before adding more weapon power (DECISIONS #61-#62).

### Active holding

Under `ActiveZoneControl`, a bot exerts control only when all are true:

- it is active after damage resolves;
- it ends the tick on a zone tile;
- its validated action is `Wait`;
- the action result is `Success`.

Move, turn, shoot, blocked/inert actions, and faults do not hold. A
cooldown Shoot that validates to Wait still has `OnCooldown`, so it does
not accidentally count. This makes suppression economically useful:
forcing a dodge stops control even when the bolt misses.

Passive per-bot `ZoneTicks` are replaced by one signed meter:

- slot 0 is the positive side; slot 1 is negative;
- one active holder moves pressure by 1 toward its side;
- two active holders contest and freeze pressure;
- no active holder decays non-zero pressure one point toward zero every
  `ControlPressureDecayInterval` ticks;
- `±ControlPressureLimit` wins by Domination;
- at MaxTicks, pressure sign wins, then health, then damage, then draw.

The v4 experiment uses limit 100, gain 1, and decay every 2 ticks.
These are tuning values, not a ship verdict. Observations expose
`ControlPressure` and `ControlPressureLimit`; legacy `MyZoneTicks` /
`EnemyZoneTicks` are null in active arms. Replays carry the pressure per
tick and at the result.

For bot authors, the experimental observation surface is nullable because
the same artifact can play official rules without these mechanics:

```csharp
foreach (HeardSound sound in context.HeardSounds ?? [])
{
    // sound.Kind; sound.Bearing (SoundBearing); sound.Distance (SoundDistance)
}

foreach (VisibleProjectile bolt in context.VisibleProjectiles ?? [])
{
    // bolt.Position / Direction / OwnerSlot / TilesPerAdvance /
    // TicksUntilAdvance / RemainingTiles
}
```

Use `context.ControlPressure` / `ControlPressureLimit` only when non-null.
`ZoneTiles` is nullable for rules with no objective. Treating any of these
collections as always present makes one rules-aware artifact fault under a
comparison arm and invalidates the causal experiment.

### Fast ordered projectiles

`ProjectileTilesPerAdvance` is now independent of
`ProjectileTicksPerTile`. The v4 candidates advance every tick and test
one versus two ordered tile substeps. For each substep the engine:

1. checks the next tile for a wall;
2. enters the tile and spends one range tile;
3. hits the first active non-owner occupant;
4. checks final-range despawn.

Resolution stops on the first wall, victim, or final-range tile. A
speed-two bolt therefore cannot tunnel through an intermediate wall,
bot, or its last legal tile. New shots still spawn only on the adjacent
tile during their firing tick; their first multi-tile advance is the
following tick. Point-blank hits stay immediate and damage remains
simultaneous.

Projectile observations now carry:

```
Position, Direction, OwnerSlot,
TilesPerAdvance, TicksUntilAdvance, RemainingTiles
```

The replay additionally records each projectile's ordered traversal for
the tick. The viewer interpolates across that path: A→B in the first
half and B→C in the second. A first-substep hit ends at B; a
second-substep hit ends at C. Simulation remains discrete and
deterministic.

### Revision-v4 experiment arms

Every arm shares seed profile `0.5-redesign-shared`, so map, spawn,
facings, and per-bot random streams are paired.

| arm | version | delta from matched 0.4 foundation |
| --- | --- | --- |
| `control` | `0.5-exp-control-v4` | passive zone scoring, no cone |
| `cone-control` | `0.5-exp-cone-control-v4` | cone + hearing, passive scoring |
| `cone-active` | `0.5-exp-cone-active-v4` | cone + hearing + active pressure |
| `cone-active-bolt1` | `0.5-exp-cone-active-bolt1-v4` | active pressure + 1 tile/tick |
| `cone-active-bolt2` | `0.5-exp-cone-active-bolt2-v4` | active pressure + 2 tiles/tick |

The primary causal comparisons are `cone-control` vs `cone-active`,
then the three active arms. Trails, residue, strafe, spread, and
undodgeable attacks remain out so the scoring redesign and speed change
can be measured independently.

### Geometry and gates

Ranked zones must be connected, contain at least four tiles, have both
horizontal and vertical local movement, expose at least two approach
directions, and have surrounding attack space. The five ranked maps now
use connected 3×3 or 3×2 regions. `causeway-01` keeps its narrow 2×2
zone as an adversarial map but leaves the ranked pool.

Before a ship decision, scripted tests must prove:

- moving mirrors and turning scanners earn no pressure;
- a successful stationary Wait does;
- suppression creates the hit-versus-control choice;
- abandoned pressure decays instead of banking forever;
- speed-two bolts hit intermediate targets and never tunnel;
- MaxTicks resolves pressure, then health, then damage;
- old rules 0.1–0.4 remain behavior- and replay-compatible.

The aware tournament must still show that unchanged Bastille is no
longer self-sufficient, at least two doctrines remain viable, ranged
hits occur beyond point blank, match duration stays acceptable, and
projectiles add measurable value beyond active control alone.

## K. Gen-8 revision-v4 evaluation

The final aware harness used four isolated docs/CLI-only doctrines plus
unchanged Bastille, one improvement iteration, three paired seed profiles,
and 180 games per arm. Full results and the balance-gate decision are in
GAME-DESIGN and DECISIONS #64.

The redesign passes its core strategic test: active control makes defensive
actions stop paying, bolt arms dethrone unchanged Bastille, distinct holder
and suppressor doctrines remain viable, speed-two traversal produces 291
ranged hits, and bolt1/bolt2 change paired outcomes. Bolt2 wins the speed
comparison and remains the primary experimental candidate.

The official promotion gate does not pass. Bolt2 improves draw rate versus
control but reduces elimination share and increases match duration; 24/180
games reach MaxTicks. Most late games have pressure close to zero, so neither
a lower control limit nor faster damage is justified by the evidence. Keep
all v4 arms experimental, keep official rules at 0.4, and investigate only
the near-zero-pressure late-game loop next.

## L. Gen-8 late-game isolation — control overtime (revision v5)

Pre-registered before the v5 harness: inspect the complete 180-game bolt2
population, freeze every v4 mechanic, and change only late objective
resolution.

The diagnosis is specific. Of 24 MaxTicks games, 15 are the same
holder-versus-suppressor periodic loop. In their final 100 ticks the common
pattern is 60 sole-holder ticks, 40 nobody-holding defensive ticks, 20
projectile launches, and zero hits. The two slots contribute 40 versus 20
sole holds while the normal one-per-two-ticks decay removes the same net 20
pressure. The meter therefore returns to zero every ten ticks despite one bot
making twice the active-control commitment. Four additional MaxTicks games
are mutual-hold or no-contact stalls and may remain unresolved by this arm.

The isolated candidate is `cone-active-bolt2-overtime`
(`0.5-exp-cone-active-bolt2-overtime-v5`):

- regulation remains bit-for-bit bolt2-v4 through tick 199;
- tick 200 begins overtime;
- the domination target changes from ±100 to ±10;
- nobody-holding pressure decay stops;
- existing signed pressure carries into overtime;
- cone, hearing, maps, spawns, active-hold gain, projectile traversal,
  cooldown, damage, health, and MaxTicks remain frozen;
- the arm retains seed profile `0.5-redesign-shared`.

The hypothesis is narrow: a net active holder should turn its repeated
commitment advantage into a short overtime domination instead of a tick-499
pressure tie. This arm can fix MaxTicks and average duration; by construction
it does not claim to repair the separate elimination-share gate.

Evaluate the unchanged five-bot final Gen-8 population over the same three
seed profiles. Retain the candidate only if MaxTicks and average duration
drop materially without increasing draws, changing the top-level doctrine
ordering, or introducing a slot-skewed overtime result. Official 0.5 still
requires the original full gate: draws down, median/average duration down,
elimination share up, and diversity retained versus matched control.

### Revision-v5 result

The isolated arm passes its own late-resolution test:

| arm | draws | eliminations | median | average | MaxTicks | leader |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| bolt2-v4 | 15/180 | 97/180 | 71 | 134.9 | 24 | ActiveHolder 51 |
| bolt2-overtime-v5 | 15/180 | 97/180 | 71 | 102.0 | 5 | ActiveHolder 51 |

Overtime changes 26 paired games: 19 MaxTicks become Domination and seven
existing late Dominations finish earlier. Total tick savings are 5,940
(median 259.5 among changed games). Overtime Domination winners are balanced
14 slot 0 to 12 slot 1. The doctrine order remains ActiveHolder, Bastille,
Suppressor, MobileFlanker, SoundHunter. One of 180 winners flips: on
Crossfire seed 303, Bastille's net active holding wins the overtime race
instead of Suppressor taking the old tick-limit tiebreak.

Retain `cone-active-bolt2-overtime` as the v5 experimental flagship: it fixes
the diagnosed long tail without touching combat. Do not promote official
0.5. Against matched control it still fails median duration (71 vs 43.5) and
elimination share (53.9% vs 62.2%); average duration is effectively tied
(102.0 vs 101.1) and draws are better (8.3% vs 11.7%). The next design
decision must address the elimination/tempo shape or explicitly redefine
why a decisive Domination is an acceptable substitute; this experiment does
not waive the pre-registered gate.

### Structural follow-up: preserve decay in revision v6

Review against the original ship criteria found one unacceptable property:
v5 stops decay, so an abandoned overtime lead is permanently banked. That is
acceptable as an isolated diagnosis but cannot ship under criterion 4.

Pre-register `cone-active-bolt2-overtime-gain`
(`0.5-exp-cone-active-bolt2-overtime-gain-v6`) before its harness. It keeps
the tick-200 ±10 overtime target, keeps normal one-per-two-ticks decay, and
changes sole-holder gain from 1 to 2 only during overtime. In the diagnosed
ten-tick cycle, doubled net active contribution replaces the pressure that
v5 preserved by disabling decay. If both bots abandon control, the lead still
returns to zero.

Use the same 180 paired games. v6 is preferred over v5 only if it keeps
MaxTicks at five or fewer, average duration at 105 or lower, draws and
eliminations unchanged, doctrine order stable, and the abandonment-decay
test passes.

Revision v6 passes every pre-registered replacement gate:

| arm | draws | eliminations | median | average | MaxTicks |
| --- | ---: | ---: | ---: | ---: | ---: |
| overtime-stop-decay v5 | 15/180 | 97/180 | 71 | 102.0 | 5 |
| overtime-double-gain v6 | 15/180 | 97/180 | 71 | 102.2 | 5 |

All five bot records, the doctrine order, and the 14/12 overtime slot split
are identical. Twenty-two games change only their finishing tick, with v6
costing 45 ticks total across the population. The abandonment test proves a
lead still decays after overtime begins.

Supersede v5 with `cone-active-bolt2-overtime-gain` as the experimental
flagship. Keep v5 resolvable as the causal reference. Official 0.5 remains
HOLD for the same median-duration and elimination-share failures; v6 fixes
the structural overtime flaw, not those remaining promotion gates.

## M. Pre-implementation projectile theory lab

Pre-register this analysis before changing the engine, SDK, guest adapter, or
tracked WASM artifact. The design question is more fundamental than numeric
balance: a deterministic projectile whose complete future path is revealed
before a defender receives a guaranteed movement action is normally
dodgeable forever on open floor. Curvature alone does not change that.

The candidate interaction uses privately programmed but immutable arcs.
Shooter and defender choose from the same pre-tick state. The shot's initial
travel resolves after bot movement on that firing tick. The defender observes
only path segments that have physically occurred; the complete future plan is
available to the shooter and authoritative replay, not to the opposing bot.
There is no random accuracy, target lock, pathfinding, or mid-flight retarget.

Before implementation, model a finite arc family with the existing range-eight
budget and speed-two ordered traversal:

- initial heading is forward-left, forward, or forward-right;
- the shooter chooses when curvature begins;
- curvature turns the heading by one 45-degree octant at a chosen cadence;
- total programmed sweep is capped at 135 degrees;
- every entered tile spends range and checks walls and bots;
- diagonal steps use strict corner collision and cannot cut past either
  orthogonally adjacent wall;
- hitting a wall truncates the path; the engine never routes around it;
- the launch traverses its first two ordered tiles immediately, then surviving
  projectiles traverse two tiles after movement on following ticks.

Integer heading schedules are the authoritative simulation. A viewer may
interpolate their tile centers as a smooth arc, but visual smoothing never
changes collision.

The combat lab must enumerate distinct paths, defender actions, and bounded
defender policies over the full projectile lifetime. Classify each local state:

- **universal defence**: one defender policy survives every hidden arc;
- **prediction contest**: every individual arc is dodgeable when known, but no
  single policy survives all hidden arcs;
- **forced attack**: at least one individual arc is unavoidable even when its
  complete path is known;
- **irrelevant**: no legal arc can threaten the defender.

The theory candidate passes only if:

1. Prediction-contest states exist on open floor and around ranked-zone
   geometry; hidden intent must change the reachable outcome.
2. Neither universal defence nor forced attacks consume the entire practical
   distance-two-to-four interaction envelope.
3. Correct facing and early movement preserve counterplay against every
   individually known arc in representative open states.
4. At least one manually programmed path can clear a corner and reach a tile
   behind cover on the ranked maps; no path may enter or cut through a wall.
5. The deduplicated path catalogue is small enough for bots to enumerate with
   an SDK preview helper.
6. Revealing each completed segment lets the defender narrow its belief and
   react; uncertainty comes only from the opponent's still-hidden committed
   choice.

Report distributions rather than selecting numeric curve parameters from one
anecdote. If the structural gate fails, revise or reject the trajectory family
without paying the SDK migration and NativeAOT rebuild. Passing this lab only
authorizes an engine experiment; it does not satisfy the balance-harness ship
gate.

### Theory-lab result

The finite model enumerates 219 parameter combinations, which collapse to 125
distinct open-floor paths after deduplication. It gives the defender full
knowledge of every completed path segment and searches all Wait, TurnLeft,
TurnRight, and MoveForward policies through the projectile's range-eight
lifetime. This is conservative for the attacker: real cone vision and wall
occlusion can reveal less.

The pre-registered two-tile launch passes the structural gate but is too
punishing at close range. A one-tile immediate launch sensitivity arm keeps
the hidden-intent result while preserving known-path counterplay:

| open floor, distance 2–4 | prediction contest | universal defence | forced attack |
| --- | ---: | ---: | ---: |
| immediate launch 1 | 53/84 (63.1%) | 31/84 (36.9%) | 0/84 |
| immediate launch 2 | 64/84 (76.2%) | 8/84 (9.5%) | 12/84 (14.3%) |

With launch one, every individual arc is dodgeable when its full path is
known in all 84 open states. In 53 states no single policy survives every
hidden committed arc. Hidden intent therefore changes the reachable outcome
without requiring an intrinsically unavoidable shot.

The complete ranked-zone sweep covers all 10,240 distance-two-to-four states:

| map | states | prediction contest | universal defence | forced attack | irrelevant |
| --- | ---: | ---: | ---: | ---: | ---: |
| arena | 2,092 | 588 (28.1%) | 581 (27.8%) | 55 (2.6%) | 868 (41.5%) |
| basic | 1,628 | 807 (49.6%) | 549 (33.7%) | 28 (1.7%) | 244 (15.0%) |
| bastion | 1,752 | 747 (42.6%) | 245 (14.0%) | 8 (0.5%) | 752 (42.9%) |
| causeway | 736 | 188 (25.5%) | 216 (29.3%) | 44 (6.0%) | 288 (39.1%) |
| crossfire | 1,424 | 170 (11.9%) | 298 (20.9%) | 36 (2.5%) | 920 (64.6%) |
| gallery | 2,608 | 1,052 (40.3%) | 1,114 (42.7%) | 30 (1.2%) | 412 (15.8%) |
| **total** | **10,240** | **3,552 (34.7%)** | **3,003 (29.3%)** | **201 (2.0%)** | **3,484 (34.0%)** |

Every ranked map contains prediction-contest states and at least one
strict-corner-valid programmed path whose endpoint is behind cover from its
origin. Forced known-path attacks are geometry-created rather than universal:
2.0% overall, highest on the narrow Causeway at 6.0%.

The theory gate passes. Select one immediate launch tile, then speed-two
travel, as the engine-experiment timing. Keep the 125-path arc family and
private immutable plan. This result authorizes an in-process engine prototype;
it does not authorize an official rules promotion or prove that strategy bots
can use the action space effectively.

## N. Pre-registered v7 engine/SDK experiment: programmed skill shots

The isolation arm is `cone-active-bolt2-arcs`
(`0.5-exp-cone-active-bolt2-arcs-v7`). It is bit-for-bit the retained v6
overtime-gain flagship except that `Shoot` may carry a private immutable
program from the theory-lab family:

- initial aim: forward-left, forward, or forward-right in 45-degree octants;
- first bend after 1–4 entered tiles;
- later bends every 1–3 tiles;
- 1–3 bends, all clockwise or all counter-clockwise;
- range 8, one ordered launch tile, then two ordered tiles per tick;
- wall and victim collision after every substep, with strict diagonal corners.

The program is validated once and cannot home or change after firing. Bots get
the numeric envelope as nullable `context.ShotPrograms` and can enumerate paths
with `ShotPaths.Preview`. A defender sees only a visible projectile's current
eight-way `Heading`; neither observations nor the wire contain future path
tiles. Omniscient replays retain the program and complete path so spectators
can understand the skill shot. Selecting a bot in developer vision hides an
opponent's unrevealed plan; selecting the owner may show it for debugging.

The player and WASM contracts are additive. Historical `Actions.Shoot()` means
the canonical straight program under v7 and behaves exactly as before under all
older arms. `Actions.Shoot(program)` is safely blocked when
`context.ShotPrograms` is null. SDK/guest 0.8 carries limits and exact current
headings in trailing protocol-0.1 sections, so old pre-arc artifacts continue
to run and simply ignore them.

### Frozen comparison

Compare v6 `cone-active-bolt2-overtime-gain` against v7
`cone-active-bolt2-arcs` on identical maps, spawns, bot random streams, three
seed sets, and game ordering. Use the same five policies in both arms:
ActiveHolder, Suppressor, SoundHunter, MobileFlanker, and unchanged historical
Bastille. The four aware sources may use exact current headings in both arms;
only Suppressor branches on `ShotPrograms` to enumerate committed arcs. Under
v6 that branch is unreachable and its historical straight-shot doctrine is
preserved.

Use the in-process runtime for the 180-game strategy iteration because it runs
the same engine and bot code without invoking NativeAOT for every source edit.
Before accepting evidence, require:

1. engine/SDK path parity tests, strict-corner tests, speed-two no-tunnelling,
   launch-substep collision, invalid-program faults, and replay privacy;
2. a real WASM-vs-in-process replay-hash parity match in the v7 arm;
3. at least one representative all-WASM mirrored set;
4. replay-derived action counts, curved-shot counts, and hit distances in
   addition to the standard draw/elimination/duration table.

### Success gates

This experiment succeeds as a usable skill-shot layer only if:

1. aware code selects valid non-straight programs in real matches;
2. at least one curved shot lands beyond the immediate launch tile;
3. at least one curved miss forces movement or interrupts active holding, so
   value is not limited to damage;
4. v7 creates ranged hits or eliminations that the paired v6 game does not,
   without increasing draws or collapsing the four aware doctrines into one;
5. no engine, protocol, or viewer surface exposes unrevealed future path to the
   defending bot;
6. the same v7 match is deterministic and identical across in-process and WASM
   runtimes;
7. historical straight-shot artifacts still complete v7 matches without
   protocol faults.

Failure does not imply stronger or homing shots. First inspect whether the
action catalogue is too large for practical selection, whether one-tile launch
plus speed two leaves useful reaction timing, and whether the aware policy is
aiming at plausible refuge tiles. Official 0.5 remains on HOLD regardless of
this isolated result; promotion still requires the full rules ship gates.

### Gen-9 v7 result: the finite action space works in real bots

The paired 180-game run used seed blocks `101/202/303`, `404/505/606`, and
`707/808/909`. Strategy iteration ran in-process; the protocol was separately
pinned by replay-hash parity and an all-WASM Suppressor/Bastille mirrored set.

| arm | draws | eliminations | median | average | MaxTicks | leader |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| overtime-gain v6 | 15/180 (8.3%) | 97/180 (53.9%) | 71 | 102.2 | 5 | ActiveHolder 51 |
| programmed-arcs v7 | 11/180 (6.1%) | 106/180 (58.9%) | 61 | 87.2 | 3 | ActiveHolder 46 |

All three blocks move in the same direction. Four draws become decisive and
none become draws; 24 other decisive games flip winner. Suppressor is the only
policy that selects programs and improves from 30 wins to 43. ActiveHolder
46, Bastille 45, MobileFlanker 22, and SoundHunter 13 all still win games, so
the action does not collapse the field into one viable doctrine.

The replay-specific analyzer reports:

- 918 explicit programs, including 824 with requested bends;
- 821 paths physically manifest a bend before despawning;
- 137 attributed programmed hits, 113 from curved programs;
- 110 hits after a bend had manifested;
- 111 curved hits beyond the one-tile launch, across 46 games;
- five no-damage crossings of a vacated active-holder tile across four games.

The smoke interaction explains the numbers. A holder saw a northwest-moving
bolt and correctly knew that heading, but could not know its already committed
westward bend on the next advance. Waiting lost health; moving would have
sacrificed control. Complete-path knowledge still makes that exact open-floor
shot dodgeable, so the eligibility comes from prediction rather than hidden
randomness.

The v7 usability gate passes. Retain `cone-active-bolt2-arcs` as the next
experimental flagship and v6 as its causal straight-bolt reference. Official
0.5 remains on HOLD: this isolation run establishes that programmed skill
shots are usable and improve v6, not that the complete 0.5 package has passed
every comparison against shipped 0.4 or its map-geometry gate.

## O. Pre-registered final promotion comparison: shipped 0.4 versus v7

Freeze the complete v7 package. Do not tune control, cones, hearing, overtime,
projectiles, arc limits, maps, cooldown, damage, or health during this run.

The all-WASM population contains six frozen doctrines:

| bot | role | artifact SHA-256 |
| --- | --- | --- |
| ActiveHolder | active objective holder | `7b493d77a8374e441d450f2fd32c807e2c9acca1fdc19ec61c3e806b785e80d3` |
| Suppressor | projectile suppression | `ff0a3a337ab7aefb8e7bb905050520e5a12e01b1f350bd71162082691a1499c4` |
| SoundHunter | redacted-sound search | `0b5f61c38177723def2154320d08dae13cc8124885e44c6262a27566cfed82cf` |
| MobileFlanker | mobile pressure | `2eda06f20e94902a84d5c3f6bb4b4121bb6adbf5c658f5e27b932b78ba7cdf1b` |
| Bastille gen-5 | unchanged historical benchmark | `712490f2e8425674d40bb7ae0328820fb31767df513b4509ce6b473c1cb0748a` |
| Helix | independent docs-only arc holder | `d4483a859c7c2e6ae992a3aedec5d995304075ffd54cbf3c8b988790751eb987` |

Run three arms:

1. `control`: 0.4 passive scoring with v7's shared seed profile, exhaustive
   fair spawns, current ranked maps, and replay tallies. This is the exact
   per-game causal baseline for v7.
2. `cone-active-bolt2-arcs`: the frozen v7 candidate.
3. `0.4`: the actually shipped rules. Its aggregate is the product baseline.
   It does not share v7's seed profile, so fixed seed numbers do not imply the
   same spawn/RNG rows; do not describe individual 0.4→v7 transitions as
   causal. Use `control` for paired transitions.

Every arm uses the same ordering and three six-game-set blocks:

- maps `basic-01,arena-01,crossfire-01`, seeds `101,202,303`;
- maps `bastion-01,gallery-01,basic-01`, seeds `404,505,606`;
- maps `arena-01,crossfire-01,bastion-01`, seeds `707,808,909`.

With six bots this is 90 games per block, 270 games per arm, 810 total. All
games use the canonical WASM runtime. Preserve every replay under a separate
arm/block directory.

Apply the standard balance gate without redefining it: relative to shipped
0.4, v7 must lower draw rate, lower median end tick, raise elimination share,
and retain strategic diversity. Report Domination and MaxTicks separately;
objective endings do not silently count as eliminations. The exact paired
`control`→v7 comparison must move in the same direction, with no net
draw creation. At least three non-historical doctrines must win games,
unchanged Bastille must not remain self-sufficient, Helix must not fault, and
no result may depend on an in-process-only artifact.

One standard criterion failing means no ship. Record the data and keep v7
experimental; do not tune a failed number inside this frozen run.

### Final promotion result: strict gate says HOLD

All 810 canonical WASM games completed with the frozen artifacts and zero bot
faults. The aggregate product comparison is:

| arm | draws | eliminations | Domination | MaxTicks | median | average | p90 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| shipped 0.4 | 31/270 (11.5%) | 175/270 (64.8%) | 58 | 37 | 31.5 | 120.1 | 499 |
| shared-seed control | 14/270 (5.2%) | 146/270 (54.1%) | 86 | 38 | 50 | 135.6 | 499 |
| programmed-arcs v7 | 17/270 (6.3%) | 151/270 (55.9%) | 105 | 14 | 64.5 | 99.0 | 200 |

Against shipped 0.4, v7 passes draw rate and diversity but fails the frozen
median-duration and elimination-share criteria. Its average and long tail are
substantially better, but those were supporting results rather than permission
to replace a failed gate. Against the exact shared-seed control, v7 adds five
eliminations and cuts average duration by 36.6 ticks, but median rises 50→64.5
and draws rise 14→17. The 270 paired rows contain 11 draw→decisive transitions,
14 decisive→draw transitions, 63 winner flips, a zero median tick delta, and a
−36.6 mean tick delta.

The strategic correction itself survives the larger field. Bastille is
85-0-5 under passive shared-seed control and 82-0-8 under shipped 0.4, but
falls to 49-29-12 under v7. Helix leads v7 at 67-21-2, followed by
ActiveHolder 53-29-8; every one of the six doctrines wins, and leader share
falls from 34.3% of decided 0.4 games to 26.5%. Unchanged Bastille is no
longer self-sufficient and the field does not collapse around the arc-aware
bot.

Nor is the outcome-label shift evidence that combat disappeared. Damage lands
in 189/270 v7 games (70.0%), versus 183/270 under shipped 0.4 (67.8%).
Programmed-shot replay evidence contains 1,563 launches, 1,380 curved launches,
1,349 manifested curves, 250 curved hits, 192 hits after a bend, and 224
ranged curved hits across 95 games. Thirty-six curved misses cross a zone tile
an active holder just vacated, across 26 games. The projectile thesis is
visible at population scale.

Candidate draws concentrate on Bastion (8/60) and Basic (5/60); Gallery has
none. Candidate per-map median ticks are 74 Arena, 25.5 Basic, 112 Bastion,
53.5 Crossfire, and 45 Gallery. This is not the old universal tick-499
failure: v7 cuts MaxTicks endings from 37 to 14 and p90 from 499 to 200 versus
shipped 0.4.

The pre-registered verdict is therefore **HOLD official 0.5**. Retain
`cone-active-bolt2-arcs` as the experimental flagship; do not adjust health,
damage, trajectory limits, control values, or the result labels inside this
completed comparison.

### What the result makes the next question

The next decision is now a product criterion, not an untested combat mechanic:
must a watchable objective game beat instant-ray 0.4 specifically on
elimination share and median ticks, or should decisive Domination, combat
incidence, average/p90 viewing time, and replay quality form an explicit
watchability gate?

Do not answer that by silently re-scoring these 270 games. If the product gate
is deliberately revised, pre-register absolute viewing-time and engagement
limits, publish a deterministic non-highlight replay sample, and validate them
on fresh holdout seeds. Continue reporting the original balance criteria next
to any new gate.

A frozen-policy durability diagnostic argues against reflexively changing
health first. Ending the recorded v7 timelines on the second hit would produce
at most 169/270 eliminations (62.6%), median 40, and average 83.1: still below
0.4's elimination share and above its median. Ending on the first hit would
cross the numeric thresholds, but 16 games have mutual first-hit ticks and it
would turn a prediction contest into one-contact lethality. Neither diagnostic
authorizes an implementation.

## P. Pre-registered v8 isolation: sole occupancy earns, contest decays

Programmed arcs materially changed the premise behind Wait-to-control. V7
proved that private committed curves can damage, suppress, and dethrone the
historical diagonal camper. Test whether the objective can now return to the
more spatial rule the viewer suggests:

- exactly one active bot occupying any zone tile gains signed pressure,
  regardless of whether it waits, moves, turns, shoots, or faults that tick;
- two active zone occupants are a physical contest and move existing pressure
  one decay step toward zero on the normal decay cadence;
- zero active zone occupants also decays pressure toward zero;
- dead or disqualified bots do not occupy or contest;
- occupancy is evaluated after movement, projectile collision, and damage;
- regulation/overtime limits, gain, decay cadence, maps, cones, hearing,
  projectiles, arc programs, health, damage, cooldown, and all timing remain
  bit-for-bit v7.

This is not passive 0.4 banking. There is still one shared decaying tug meter,
but the commitment is territorial rather than an action tax: hold the space,
and use skill shots to push the opponent fully out of it.

Add one experimental arm:

`cone-occupancy-bolt2-arcs`
(`0.5-exp-cone-occupancy-bolt2-arcs-v8`)

It shares v7's `0.5-redesign-shared` seed profile. Keep
`cone-active-bolt2-arcs` resolvable and behavior-compatible as the exact
Wait-to-control reference.

### Scripted gates

Before population evidence, prove:

1. a sole occupant gains while waiting, turning, shooting, faulting, and moving
   between zone tiles;
2. an opponent entering any zone tile immediately stops gain and starts decay;
3. both occupants decay even if one or both choose Wait;
4. when one occupant is evicted, the survivor starts gaining on that same
   post-movement/post-damage tick;
5. empty-zone pressure decays and abandoned leads cannot bank;
6. v7's successful-Wait semantics and historical 0.1–0.4 behavior remain
   unchanged;
7. v7 and v8 resolve identical spawns and bot random streams for the same map
   and seed.

### Frozen population comparison

Reuse the six artifact hashes and all-WASM blocks pre-registered in §O. The
270 v7 replays are the frozen paired reference; run 270 v8 games with the same
ordering:

- maps `basic-01,arena-01,crossfire-01`, seeds `101,202,303`;
- maps `bastion-01,gallery-01,basic-01`, seeds `404,505,606`;
- maps `arena-01,crossfire-01,bastion-01`, seeds `707,808,909`.

Retain v8 over v7 only if:

1. draws do not exceed 17/270;
2. eliminations do not fall below 151/270;
3. median, average, p90, and MaxTicks do not exceed v7's
   64.5 / 99.0 / 200 / 14;
4. all six doctrines still win and top share stays at or below 35% of decided
   games;
5. unchanged Bastille is not champion and loses at least 20 games;
6. replays contain both contested-decay ticks and sole-occupancy gains
   immediately following a real eviction;
7. every game uses WASM and every bot records zero faults.

Continue reporting the standard shipped-0.4 comparison. One failed criterion
means v7 remains the flagship and v8 stays only as a reproducible experiment.
Passing makes v8 the preferred experimental objective rule; it does not by
itself silently replace the still-unresolved official promotion gate.

### Pre-registered adaptation safeguard

The frozen bots were authored to earn control specifically by Wait. If more
than 75% of v8 sole-occupancy gain ticks still use Wait, treat that run as a
mechanical regression screen rather than the final strategy verdict. Before
seeing adapted results, commission two isolated docs/SDK/CLI-only doctrines:

- an aggressive territorial breacher that body-contests and fires to evict;
- a predictive arc shepherd that herds opponents off-zone, then patrols while
  scoring.

Allow one bounded loss-forensics iteration. Their authors may use only player
docs, public SDK source, CLI play/set/build, and CLI replay summaries—never
engine source, design documents, other bot source, or raw replay JSON.

The adapted comparison uses the two final artifacts plus ActiveHolder,
Suppressor, unchanged Bastille, and Helix under both v7 and v8, with identical
all-WASM blocks from this section. V8 passes the adaptation gate only if,
relative to v7 in that same population:

1. draws, average, p90, and MaxTicks do not increase;
2. eliminations do not decrease;
3. both new doctrines win games, record zero faults, and visibly score with
   non-Wait actions;
4. unchanged Bastille is not champion and loses at least 20 games;
5. the field retains at least four winning doctrines and top share stays at or
   below 35% of decided games;
6. authoritative replays show contested pressure decay followed by real
   eviction and same-tick sole-occupancy gain.

The frozen and adapted tables remain separate. Do not use adaptation to hide a
mechanical regression, and do not reject a territorial action economy solely
because Wait-trained bots continue waiting.

### V8 result: territorial economy works; formal product verdict stays open

All 270 frozen-population v8 games and all 270 adapted-population v7/v8 games
used canonical WASM with zero faults. Scripted gates passed, including
same-tick scoring after post-damage eviction and byte-identical v7 replay
compatibility.

The frozen v8 arm failed its pre-registered v7-retention table: draws rose
17→22, median/average/p90 rose 64.5/99.0/200→66.5/119.1/499, and MaxTicks rose
14→35, although eliminations held at 151. Eighty-four percent of its score
ticks were still Wait, so the pre-registered adaptation safeguard correctly
triggered.

Two isolated docs/SDK/CLI-only authors produced Breacher and Shepherd. Both
used motion, turning, shooting, body contest, and private curves while scoring;
both recorded zero faults. In the adapted six-bot field, v8 versus v7 changed:

| arm | draws | eliminations | Domination | MaxTicks | median | average | p90 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| adapted v7 Wait-control | 24 | 218 | 35 | 17 | 26 | 71.7 | 200 |
| adapted v8 territorial control | 22 | 219 | 21 | 30 | 25.5 | 85.0 | 499 |

The original adaptation gate therefore also failed average, p90, and MaxTicks.
That completed gate is not retroactively rewritten. V8 stays experimental.

The richer replay evidence nevertheless validates the action economy the arm
was designed to test:

- 3,911 sole-occupancy gain ticks, including 2,080 (53.2%) with non-Wait
  actions: 501 Move, 412 Shoot, 764 TurnLeft, and 403 TurnRight;
- 16,235 contested ticks and 780 real pressure-decay steps;
- 189 contested-to-sole transitions, all 189 scoring on the same tick;
- 162 of those transitions coincided with damage to the evicted bot;
- damage in 246/270 games (91.1%), reciprocal damage in 166/270 (61.5%),
  and damage on multiple ticks in 236/270 (87.4%);
- all six doctrines won; Shepherd led only 56/248 decided games (22.6%);
- unchanged Bastille fell to 30-47-13.

The exact remaining failure is not passive scoring. Thirty matches enter
MaxTicks with zero pressure; every one spends its final 100 ticks physically
contested, with no damage in that final window. Replay-dynamics analysis marks
30/270 (11.1%) as repeated-frame loops. The other 81.1% end by elimination.

This result motivated the generation-aware evaluation policy in
`EVALUATION-METHODOLOGY.md`: old bots are compatibility and causal diagnostics,
not the product veto for a substantial redesign; duration is a guardrail, while
combat, action variety, repetition, objective interaction, and blind replay
review are primary. That policy is prospective, not permission to recalculate
this completed v8 gate.

Next run a fresh holdout with at least four independently authored or
substantially adapted territorial doctrines. Freeze the dynamics thresholds
and outcome-blind viewer sample before opening aggregate outcomes. Do not add a
hard tick cap merely to hide the 30 loops; make candidate-aware bots prove they
can evict, escape, or punish a persistent physical contest.

## Q. Pre-registered territorial-native holdout

This is the first prospective run under
`EVALUATION-METHODOLOGY.md`. Freeze v8 exactly as implemented; no rules,
health, damage, curve, control, overtime, map, or viewer tuning may enter this
holdout.

Commission four isolated docs/SDK/CLI-only doctrines with equal budgets:

- **Pincer:** aggressive body contest and refuge-tile forks;
- **Comet:** mobile zone skirmishing and changing attack angles;
- **Augur:** motion prediction and private curved interception;
- **Echo:** sound-driven ambush and flank eviction.

Each author gets an initial implementation, one six-game set against each of
the same two black-box candidate-aware training artifacts (Breacher and
Shepherd), at most one summary-driven revision, the same final sets, and one
canonical WASM verification. Authors may not inspect engine/design internals,
raw replay JSON, or any bot source.

After all four final WASM hashes freeze, run their native v8 round-robin and
rerun the previous native quartet—ActiveHolder, Suppressor, SoundHunter, and
MobileFlanker—under their native v7 rules. Both product generations receive
the same fresh map/seed blocks:

- maps `basic-01,arena-01,crossfire-01`, seeds `1103,2207,3301`;
- maps `bastion-01,gallery-01,basic-01`, seeds `4409,5501,6607`;
- maps `arena-01,crossfire-01,bastion-01`, seeds `7703,8807,9901`.

Each native cohort has six pairings × six mirrored games × three blocks =
108 games. V7 and v8 share `0.5-redesign-shared`, so map/spawn/RNG
distributions remain comparable, but the table is explicitly a product
generation comparison, not a single-mechanic paired A/B.

Run each new bot against unchanged Bastille and Helix as a separately labeled
historical-sentinel screen. Sentinel records cannot veto the native product
gate unless they expose faults, deterministic breakage, or a concrete
degenerate exploit.

### Frozen native-v8 gates

All final games must use WASM, verify deterministically, and record zero
faults. Across the 108 native games:

1. draws are at or below 10%;
2. all four doctrines win, and no bot owns more than 35% of decided wins;
3. damage occurs in at least 75% of games;
4. both bots deal damage in at least 40%;
5. damage lands on multiple ticks in at least 60%;
6. active-world ticks are at least 75%;
7. stalled games are at or below 5%;
8. repeated-frame loop games are at or below 10%;
9. median normalized action-family entropy is at least 0.60;
10. at least half of sole-occupancy score ticks use non-Wait actions;
11. replays contain at least 36 contest-to-sole transitions, including at
    least 24 coinciding with real damage eviction;
12. median end tick stays at or below 100; average and p90 are reported as
    viewing context, not comparative ship gates.

Before opening the aggregate outcome or dynamics tables, select 12 v8 replays
with `replay-review-sample.py` using selection seed `20260724`. Export and
publish their self-contained viewers with outcome-neutral titles. At normal
speed, record legibility, tension, visible action/counter-action,
repetition/downtime, and whether the ending feels earned. At least 9/12 must
score 3 or better on action/counter-action, and no more than two may score 1–2
for repetition/downtime. Publish a separate, clearly labeled highlight set
only after the representative notes are frozen.

One numeric failure or a confusing/dull blind sample keeps official 0.5 on
HOLD and records the exact residue. Passing makes territorial v8 the preferred
0.5 candidate; official pinning still requires completion of the replay review,
not just the outcome table.

### Holdout result: dynamics pass, diversity fails

The 108-game native-v8 holdout completed exactly as pre-registered. All games
used the four frozen WASM artifacts, recorded zero faults, and reproduced
108/108 replay hashes on an independent rerun.

| gate | threshold | result |
| --- | ---: | ---: |
| draws | ≤10% | 2/108 (1.9%) |
| winning doctrines | all 4 | all 4 |
| leading share of decided wins | ≤35% | **Pincer 45/106 (42.5%) — FAIL** |
| damage games | ≥75% | 108/108 (100%) |
| reciprocal damage | ≥40% | 85/108 (78.7%) |
| multiple damage ticks | ≥60% | 108/108 (100%) |
| active-world ticks | ≥75% | 100% |
| stalled / looped games | ≤5% / ≤10% | 0 / 0 |
| median action entropy | ≥0.60 | 0.728 |
| non-Wait sole-score ticks | ≥50% | 1,006/1,030 (97.7%) |
| contest-to-sole / damage evictions | ≥36 / ≥24 | 87 / 51 |
| median end tick | ≤100 | 23 |

Records were Pincer 45-9-0, Echo 26-27-1, Comet 18-35-1, and Augur
17-35-2. Every match ended with an Elimination reason, including two mutual
elimination draws. Average end tick was 24.7 and p90 was 41. Curved shots were
not ornamental: 615/856 programs curved, 307 curved shots hit, 259 were ranged
curved hits, and 96/108 games contained at least one ranged curved hit.

The outcome-blind 12-replay sample also passed. Mean ratings were 4.17
legibility, 3.92 tension, 4.33 visible action/counter-action, 3.75 freedom from
repetition, and 4.75 earned ending. All 12 scored at least 3 for
action/counter-action; only one scored 2 or below for repetition. Reviewers
understood sole scoring and contest decay. The remaining presentation weakness
is delayed-projectile causality: some impacts require the event feed to connect
them to the originating shot.

Therefore official 0.5 remains on **HOLD**. The failure is deliberately narrow:
Pincer exceeded the frozen diversity ceiling. Do not waive the 35% gate or
tune rules after seeing it. Freeze v8 and Pincer, then give counter-doctrines a
bounded equal adaptation pass on fresh seeds. The next test asks whether bot
strategy can restore diversity without sacrificing the dynamics and viewer
results. Full artifacts, sentinel rows, and DX findings are in
`DX-FINDINGS-TERRITORIAL-V8-2026-07-24.md`.

## R. Pre-registered frozen-Pincer counterplay trial

**Superseded before execution by the product-owner promotion in §S.** None of
the fresh seeds below were opened. The protocol remains in the record to show
the follow-up that the original 35% policy would have required.

This follow-up isolates strategic adaptation. The v8 rules, ranked maps,
viewer, Pincer WASM
`0c0271655d25e6b91d520b2f0d55acdefaabd3e205646fff6b98a82b4c1e5abd`,
and all gates from §Q remain frozen. No health, damage, projectile, curve,
control, overtime, geometry, or threshold change may enter the trial.

Give Comet, Augur, and Echo one equal docs/SDK/CLI-only improvement iteration.
Each may inspect `replay --summary` output and the self-contained viewers from
its own §Q losses, but not raw replay JSON, engine/design internals, Pincer
source, or another bot's source. The author may change doctrine as much as
needed within that one iteration. Freeze and record the three resulting WASM
hashes before final play.

The new holdout is the four-bot round-robin—frozen Pincer plus the three
adapted counter-doctrines—on these unopened blocks:

- maps `basic-01,arena-01,gallery-01`, seeds `12011,13007,14009`;
- maps `crossfire-01,bastion-01,basic-01`, seeds `15013,16001,17011`;
- maps `arena-01,gallery-01,crossfire-01`, seeds `18013,19001,20011`.

That is again 108 mirrored games. Final evidence is all-WASM, zero-fault, and
must reproduce deterministically. Reapply every §Q numeric gate, especially
all four doctrines winning and no leader above 35% of decided wins. Also
report Pincer's exact record/share change from §Q; it is diagnostic context,
not a replacement threshold.

Before opening aggregate results, select a new 12-replay header-only sample
with selection seed `20260725` and apply the same viewer gates. Preserve the
§Q results alongside the follow-up. A pass means candidate-aware strategy can
counter Pincer without rules tuning and territorial v8 may return to the
official-pinning decision. Any failure remains a HOLD and names the failed
dimension before rules work reopens.

## S. Product-owner promotion: official 0.5

After reviewing the complete result, the product owner judged Pincer's 42.5%
share to be an appropriate champion performance rather than unhealthy
concentration. The future substantial-rules diversity ceiling is therefore
45%, the conservative end of the proposed 45–50% range.

This is not a retroactive pass under §Q. The 35% pre-registration failed and
stays visible. Decision #75 is a separate product-policy override:

- every safety and determinism hard gate passed;
- all four native doctrines won;
- every combat, activity, repetition, objective, duration, and blind-viewer
  gate passed;
- Pincer's 45-9-0 record is below the new 45% ceiling at 42.5%;
- v8 ships without a single mechanic or numeric retune;
- §R is cancelled before its fresh seeds or adaptations are used.

Official `GameRules.V0_5` is the exact v8 mechanic set with rules version
`0.5` and the same `0.5-redesign-shared` seed profile: cone vision, redacted
hearing, territorial sole-occupancy pressure, contested/empty decay,
speed-two ordered projectiles, private immutable skill-shot programs, and the
tick-200 pressure overtime. The historical
`cone-occupancy-bolt2-arcs` alias retains its experimental version string for
old scripts and evidence.

Pincer is crowned as `champions/pincer-gen10`, preserving its final source and
the exact holdout WASM hash
`0c0271655d25e6b91d520b2f0d55acdefaabd3e205646fff6b98a82b4c1e5abd`.
