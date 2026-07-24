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
