# DX findings — agent-arena gen-6 (0.5 conebolts, round 1), 2026-07-23

Two challengers written for rules 0.5 (Nightjar scout-flanker, Ballista
suppression-zoner) under `BOTARENA_RULES=conebolts`. Stopped after round 1
(owner call — the signal was decisive). Both bit-identical artifact parity,
zero faults. Tournament verdict in DECISIONS #57; this is the pre-ship task
list. The two agents' reports converged so strongly that every HIGH finding
below was reported independently by both.

## Ship-blocking: the docs don't teach the load-bearing mechanics
A player working only from the site docs designs the wrong bot on the first
try. Before 0.5 ships, the player rules card must state, plainly:

1. **[blocker] Point-blank is instant; range ≥2 is a slow dodgeable bolt.**
   The single most decisive combat fact. Only a distance-1, in-facing shot
   hits the tick it fires; everything else is a bolt travelling 1 tile per
   2 ticks that the target can walk out of. Survival strategy is built
   entirely on this line and it is currently one clause of an experiment
   brief.
2. **[blocker] No strafe → a dodge is 2 ticks (turn, then move).** The docs
   still describe v0.4 "perpendicular sidestep" dodging, which no longer
   exists — so a boxed point-blank is a forced 1-for-1 trade (a mutual-death
   draw). Ballista reported this drove "three full redesigns."
3. **[blocker] The exact cone predicate.** visible ⟺ Chebyshev ≤ 1 OR
   (forward ≥ 1 ∧ |lateral| ≤ forward ∧ Chebyshev ≤ 6), minus corner
   occlusion. Both agents wrote diagnostic bots to confirm it. The 8-tile
   proximity ring reveals enemies in ALL directions including directly
   behind — state it.
4. **[blocker] Bolt occupancy timing.** A bolt dwells on each tile for
   exactly 2 ticks (measured, not read); it appears on the adjacent tile the
   tick after the shot; it never hits its owner (a bot may stand on its own
   bolt). Dodge timing is only solvable once this is known.
5. **[high] Split-zone "contested pays nobody" spans pads.** On maps whose
   zone is two disconnected pads (arena-01: x=10 and x=13), both bots being
   on *any* zone tiles — even different pads — freezes accrual for both, so
   the game is decided by the opening asymmetry then the zone-tiebreak.
   Nightjar lost a game 27-0 on zone-ticks before understanding this; the
   docs give no split-zone geometry.

## Ship-blocking: the replay tooling can't show the two new state channels
All four items below RESOLVED in the 0.5 hardening batch (DECISIONS #59):

6. ~~**[blocker] `replay --summary` shows neither cone contents nor bolts.**~~
   Fixed: printed ticks now carry `bolts»` lines (owner, tile, direction,
   `advN` ticks-until-advance, `remN` residual range) and, under cone rules,
   per-bot `sees»`/`hears»` lines — the bot's actual contact picture.
7. ~~**[med] `--full` is a sub-flag of `--summary`.**~~ Fixed: `--full` alone
   implies the summary; debug truncation is 200 chars and OFF under `--full`.
8. ~~**[med] Ranged hits read as two events 2 ticks apart.**~~ Fixed the lie,
   not the shape: under projectile rules an unresolved Shot prints `launch`
   (never `miss`); the landing stays a Damage event, now correlatable via the
   `bolts»` flight lines.
9. ~~**[low] `--swap` "slot-0 perspective" reads inverted.**~~ Fixed: batch
   totals name the bot (`W = <name> wins`).

## What worked (kept for the record)
Bit-identical parity on first submission for both; fuel headroom ~300-500x;
the rules pin, deterministic replays, WASM parity, `set`/`replay --summary`
forensics, and the register→submit→poll API flow all used heavily without
friction. And the viewer's mid-run cone + bolt rendering (DECISIONS #57)
turned the ruleset watchable in real time.

## The gameplay itself: success (see DECISIONS #57)
Decisive aware-vs-aware kills, a three-way rock-paper-scissors, and every
executable play from the design catalog observed unprompted in ranked
replays. The mechanics are right; only their documentation and tooling
surface lag — which is the whole reason a docs-only tournament exists.

## Gen-7 addendum (aware tournament, 2026-07-24)

Both aware challengers independently reported the same top items — credible by
convergence:

- **[med] Strafe silently no-ops with no way to query enabled actions.** Under
  conebolts `Actions.StrafeLeft/Right` validate to Wait/Blocked (no strafe is
  a load-bearing 0.5 mechanic — the 2-tick dodge), but a bot only discovers
  this by burning a tick. The doc-comment says "experiment arms only" without
  naming which, and there's no `context`-queryable capability set. A defender
  built to sidestep silently eats every hit. Fix candidates: a capability flag
  in the observation, or a validated-action echo the first tick.
- **[expected, not a bug] Player docs describe v0.4 (instant ray, omni
  vision), false for conebolts.** By design — conebolts is an experiment arm,
  not shipped; the SDK type doc-comments hedge correctly (the inert pattern).
  Converts to a real task ONLY if 0.5 ships official (the player rules card
  rewrite already tracked here).
- **[investigate] Artifact-hash parity is bot-specific, not systemic.**
  Bulwark's local build hash != server hash across BOTH its versions;
  Bloodhound's MATCHED (local == server) on both. Same framework, same SDK
  0.6.0 — so the mismatch is something in Bulwark's sources/build, not a
  framework reproducibility break. Behavior stays deterministic (faults 0,
  identical gameplay). Worth a root-cause pass: what in one bot's build embeds
  environment state (path? file order in the submitted sources map?).
- **[kept] `replay --summary` cone/bolt/hearing lines carried the whole
  iteration loop** — both agents cited `sees»`/`hears»`/`bolts»` as essential.
  The gen-6 tooling pass paid off.
