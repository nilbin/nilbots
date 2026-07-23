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
  omniscient events it is undermined. Middle: **loud events (Shot,
  Damage, Destroyed, Disqualified) are delivered when any reference
  position is within Chebyshev `HearingRadius` of the observer,
  regardless of cone or LOS**; quiet events (Turn, Move, MoveBlocked)
  stay sight-gated. `HearingRadius = 8` (= ShotRange: you hear as far as
  guns reach).
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
  existing bolts advance (phase-due) → occupancy hit-check for every
  bolt against post-move positions → new shots spawn (+ spawn checks) →
  all damage lands simultaneously. Crossing bolts pass through each
  other (collision is a future dial — Vanguard Push note); a bolt never
  hits its OWNER (Vanguard Push requires overtaking your own slow bolt).
- **Cooldown/energy semantics unchanged** — Shoot is still Shoot; no
  action parameters, no protocol bump. Damage events fire on hit with
  the existing shape; the Shot event marks the launch (to = spawn tile).
- Replay: per-tick `projectiles` list (x, y, direction, owner) — omitted
  (null) under instant-ray rules, so all historical hashes stand.
  Observation: trailing `P` section with visible bolts (position,
  direction, owner), sight-gated like everything else.

## C. Squeeze math — why bolts alone don't evict, and what does

With TicksPerTile 2, a bolt fired from (6,5) eastward occupies (7,5) for
ticks t+2..t+3 and (8,5) for t+4..t+5: each zone tile is hot for 2 ticks,
sweeping west→east. Tick-table the 2×2 camper against a two-bolt volley
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
`int SpawnAttempts` joins GameRules (64 legacy; 256 under 0.5 arms —
gameplay-affecting, hence rules-gated, not a silent edit).

## E. Arms and versions

| arm        | version string      | on top of 0.4                                  |
| ---------- | ------------------- | ---------------------------------------------- |
| cone       | 0.5-exp-cone        | VisionCone + HearingRadius 8 + SpawnAttempts 256 |
| bolts      | 0.5-exp-bolts       | ProjectileTicksPerTile 2 + SpawnAttempts 256   |
| conebolts  | 0.5-exp-conebolts   | both                                           |

Rules 0.1–0.4 stay bit-identical (all new code behind flags; full suite
+ goldens must pass untouched). SDK/GuestAdapter bump to 0.5.0 (new
trailing `P` observation section + `VisibleProjectiles` context field).

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
