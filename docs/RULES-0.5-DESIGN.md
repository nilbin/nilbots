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
  **loud events (Shot, Damage, Destroyed, Disqualified) beyond sight but
  within Chebyshev `HearingRadius` arrive REDACTED as sounds** — event
  type, an 8-way bearing octant (cardinal only when one axis dominates
  by more than 2:1), and a distance band (near ≤2 / medium ≤5 / far) —
  never coordinates, slots, or outcomes. A sighted event is a full event
  and never also a sound; quiet events (Turn, Move, MoveBlocked) stay
  sight-gated. `HearingRadius = 8` (= ShotRange: you hear as far as guns
  reach). Sound is a cue and a decoy channel, not a radar — the v1
  behavior (full authoritative events through walls) was radar and is
  retired with the v1 arms.
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

Hardened revision **v2** (DECISIONS #59) — every arm shares redacted
hearing, double-check collision, computable bolt timing, exhaustive
spawns, and per-tick replay zone tallies:

| arm         | version string          | on top of 0.4                          |
| ----------- | ----------------------- | -------------------------------------- |
| 0.5-control | 0.5-exp-control-v2      | spawn-matched baseline only (§H item 3) |
| cone        | 0.5-exp-cone-v2         | VisionCone + HearingRadius 8           |
| bolts       | 0.5-exp-bolts-v2        | ProjectileTicksPerTile 2               |
| conebolts   | 0.5-exp-conebolts-v2    | both                                   |
| conebolts1  | 0.5-exp-conebolts1-v2   | both, bolts at movement speed (§G counter-tune) |

The v1 strings are retired, not preserved: experiments carry no
bit-compat promise, and gen-6 artifacts cannot parse the widened `P`
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
