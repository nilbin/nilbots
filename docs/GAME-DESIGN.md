# Game design — depth assessment and backlog

The design counterpart to `docs/PLAN-SUMMARY.md` (engineering) and
`docs/DECISIONS.md` (choices made). This file owns the questions "is the game
deep enough?", "what do we add next?", and "how do progression and monetization
fit without poisoning the premise?". Rule changes decided here still get a
DECISIONS entry when implemented.

## Where the game stands (evidence: agent-arena tournament, 2026-07-22)

Real emergent depth showed up with zero prompting:

- **Crossing shots** — exploiting move-before-shoot resolution to trade or
  mutual-kill on approach (the very first smoke match after the eval-speed work
  ended in a mutual elimination at tick 52).
- **Corner play** — camping and peeking around corner-strict LOS.
- **Cooldown baiting** — drawing a shot, then advancing during the 2-tick gap.
- The champion (Warden, elo 1216) won on disciplined sight-line control, not
  aggression.

And the limits showed up just as clearly:

- The **leaderboard stabilized within 3–4 improvement iterations** — the
  strategy space is real but converges fast.
- **Draws are too common**: phase-locked patrol cycles that never cross sight
  lines, and 500-tick health stalemates. Passive play is too safe.

**Verdict: deep enough to pilot with friends for a few weeks; not deep enough
to hold a community. The #1 design debt is that passivity is under-punished.**

### Gen-2 tournament data (same day, 2 rounds, 36 games/round)

The second tournament sharpened the picture. Depth is real: gen-2 bots beat
the gen-1 champion 32.5-3.5 on aggregate (the ratchet works), and the meta
evolved visibly between rounds — Rampart's round-2 "hold fire against
orbiting lane-dancers" discipline turned Switchblade's 5-draw standoff
profile into a 6-0 rout, and Oracle's cadence-timed lane entries were the
only thing that ever beat the eventual champion. But the defense-beats-
offense skew got MORE pronounced at higher skill: across both rounds the
three new bots' 18 head-to-head games produced 3 decisive results in round 1
(15 draws/1-point margins) and a fortress sweep in round 2 only because one
side's aggression misfired. Draw mechanisms observed: mutual-elimination
trade chains, phase-locked standoffs where neither side finds a safe commit,
and full-health tick-500 sieges. This is direct evidence for backlog #2
(energy-cost shots / engagement forcing): a disciplined fortress is currently
the strongest shape, and its games only end when someone else takes a risk.

## Design constraints (non-negotiable, from the plan)

1. Every gameplay value lives in `GameRules`, pinned by the rules version; maps
   are versioned; SDK/protocol changes bump their axes. A rule change is a
   version bump, never a silent edit (golden tests enforce this).
2. Determinism is the product: any new mechanic must be a pure function of
   (artifacts, map, rules, seed). Pickups, hazards and loadouts must derive
   from the match seed, never from new entropy.
3. Replays stay legible: a spectator should see *why* something happened. Favor
   mechanics with visible state over hidden modifiers.

## Rules 0.2 shipped; energy candidate held back (2026-07-22)

First full harness run (`scripts/balance-eval.py`: champions + gen-2 bots,
round-robin 6-game sets, fixed seeds 101/202/303, 36 games/arm):

| ruleset          | draws | draw% | elims | med tick | avg tick |
| ---------------- | ----- | ----- | ----- | -------- | -------- |
| 0.1 baseline     | 15    | 42%   | 30    | 196      | 244      |
| + seed spawns    | 10    | 28%   | 33    | 151      | 197      |
| + spawns + energy| 15    | 42%   | 32    | 158      | 212      |

**Seed-spawn variation shipped as rules 0.2** — draws down a third, games a
quarter shorter, more eliminations, and it also fixes "seeds don't vary
battles". **Energy (6 max, 2/shot, +1 per 3 ticks) did not ship**: with
energy-UNAWARE bots it cancelled the spawn gains — attackers ran dry
mid-assault, converting kills into stalemates. It taxes aggression as much as
camping when nobody manages the resource. It stays implemented behind
`--rules energy`; the fair re-test is a gen-3 agent-arena tournament played
UNDER those rules, so bots are written to manage energy (backlog #2 stays
open with this evidence attached).

## Energy candidate: closed as-tuned (gen-3 verdict, 2026-07-23)

The re-test the gen-2 data demanded — a tournament played UNDER energy rules
by a bot built for them (Metronome, single challenger) — closed the case.
Energy (6 max, 2/shot, +1 per 3 ticks) fails in both directions:

- **Energy-unaware bots** run dry mid-attack (gen-2 A/B: cancelled the spawn
  gains; gen-3 ranked round: Rampart blind-fired its meter to 0-1 permanently
  and Warden was swept 6-0 by simple meter exploitation).
- **Energy-aware play** makes it worse: the challenger's mirror set was
  **six tick-499 stalemates out of six** — two disciplined meters never find
  a spend worth making. And its 9-fix improvement branch proved a fortress
  WITH a health lead is structurally unbreakable under energy (every
  aggression dial converted draws into deaths; the agent shipped v1 back
  unchanged — an honest convergence signal).

Verdict per the methodology: draws UP, entrenched leads stronger — the
opposite of the anti-draw goal. Energy stays implemented behind
`--rules energy` (and `BOTARENA_RULES=energy` server-side) as a reference
failed candidate. If ever revisited, change the SHAPE, not the numbers:
energy as a tiebreak at MaxTicks, late-game regen escalation, or
movement-coupled costs. Next anti-draw candidates worth a harness run:
health pickups (forces map contests) and shrinking-zone variants.

Also filed from gen-3: seed spawns can start a bot on the opponent's firing
lane (tick-0 hit before its first decision — basic-01 s5150). Mirrored sets
keep it fair set-wise; still worth a no-mutual-lane constraint in
SpawnVariation at the next rules bump.

## Methodology: agent-arena is the balance harness

Before/after any candidate rule change, run the tournament (now fast:
`BOTARENA_BROADCAST_TPS=250`, `BOTARENA_COMPILE_WORKERS=3`, DECISIONS #41/#42)
and compare:

- **Draw rate** (target: sharply down from today)
- **Median end tick** (target: down; 500-tick walls should be rare)
- **Elimination share of results** (target: up)
- **Strategy diversity** — do distinct personas still produce distinct
  action mixes, or did the change collapse everything into one build?
- **Champion turnover** — does the reigning champion (champions/) survive?
  A good change shakes the top without invalidating skill.

Ship a rule change only if draws drop without collapsing diversity.

## Backlog (ranked)

| # | Item | Size | Touches |
| --- | --- | --- | --- |
| 1 | **More maps, asymmetric geometry** (chokepoints, rooms, long lanes) | S | maps/ only — no code |
| 2 | **Anti-draw: energy-cost shots** — shots spend energy that regenerates; camping starves, tempo play is rewarded. First candidate; alternatives: shrinking zone, health pickups | M | GameRules + context field → SDK/protocol bump |
| 3 | **New actions: Scan / Shield / Dash** — information and commitment trades | M each | SDK + protocol + rules bump |
| 4 | **Seed-deterministic pickups / map events** | M–L | engine events, map format, viewer |
| 5 | **Loadout modules** — pick 1 of N sidegrades pre-set (vision+1 / cooldown−1 / HP+1); the progression hook | L | match config + replay header + Matches snapshot + docs |
| 6 | **2v2 team matches** | XL | engine slots, ranked pairing, viewer |

Rule of thumb from the constraint list: items 1–2 are "rules data" changes;
3–5 are version-axis changes the architecture was explicitly built to absorb
(loadout = one more match input feeding the replay hash); 6 is a redesign.

## Progression (retention without power creep)

- **Leagues/divisions** derived from Elo (data already exists) — visible rank
  is the cheapest retention feature we can ship.
- **Achievements** tied to replay facts ("won on health at tick 500", "won
  without taking damage", "3 wins with zero faults") — computable from stored
  replays, no engine change.
- **Seasons** (frozen phase 6) are the natural container: rating soft-reset,
  seasonal cosmetic rewards, a champion's gallery per season (champions/
  already started this tradition).
- **Bot version history as the journey** — the garage already shows it; lean
  in (diff view between versions, per-version win rates).
- If loadout modules land (backlog #5), they are **earned by playing, never
  bought** — see below.

## Monetization stance (decide now, build later)

**Never stats-for-money.** The audience is programmers; pay-to-win kills both
the competitive claim ("your code wins matches") and this crowd's trust.
What we can charge for, in rough order of credibility:

1. **Cosmetics** — custom sprites (plan §33, already roadmapped), accents,
   chassis variants, kill-effects. Pure identity, zero gameplay.
2. **Private leagues / hosted tournaments** — companies, classrooms, meetups.
   This is a feature businesses expense.
3. **Supporter tier** — name flair, replay GIF export, priority queue.
4. **Compute quota** — every submission is a real NativeAOT compile on our
   hardware. A generous free tier plus paid headroom for heavy iterators is
   honest cost recovery, not pay-to-win.

Architecture note: entitlements are an Accounts-module concern (a table and a
check), cosmetics ride the existing appearance/snapshot pipeline, and the
Engine never learns any of this exists.

## Docs implications

When the first rule change ships: the site Rules card gains a version header
and a changelog section; `GameRulesVersion` is already surfaced everywhere it
needs to be. Loadouts/pickups additionally need SDK doc updates and a template
README refresh (the tournament proved agents build exactly what the docs say —
no more, no less).
