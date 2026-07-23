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

## Rules 0.3 shipped: range cap + lane-safe spawns (2026-07-23)

The basics review (RULES-0.3-DESIGN) went through the 5-arm harness: 5 bots
(both champions + Metronome/Oracle/Switchblade), 3 maps incl. the new
crossfire-01, fixed seeds, 60 games/arm:

| arm            | draw% | med tick | elims |
| -------------- | ----- | -------- | ----- |
| 0.2 control    | 38%   | 153      | 52    |
| range cap 8    | **22%** | **120** | 50    |
| strafe         | 32%   | 278      | 48    |
| hill           | 23%   | 302      | 41    |
| full slate     | 33%   | 263      | 41    |

**Shipped as rules 0.3: shot range 8 + spawn lane safety** — the only arm
passing every criterion (draws nearly halved, games shorter, eliminations
intact). crossfire-01 joined the ranked pool. **Strafe held**: the predicted
oscillation-dodging materialized (games +80% length for −6pp draws).
**Hill held for gen-4**: draws −15pp but games doubled with zone-IGNORANT
bots — its fair test needs agents that contest the zone (`--rules hill`,
implementation complete incl. viewer/SDK/docs-in-skill). The combined slate
underperformed range alone — mechanics dilute each other; ship the winner,
not the bundle.

## Gen-4 trial: hill v2 = exclusive accrual (2026-07-23, DECISIONS #50)

The trial's zone-aware challenger (Castellan) swept both champions 12-0 on
the server (mostly Domination ~t151-162) — zone-aware vs zone-ignorant is
no contest, as predicted. The design finding came from the mirror set:
under shared accrual two zone-aware equals co-occupy the zone without
firing a shot (150-146, zero damage, every game a slot-1 Domination at the
identical tick) — the hill degenerates into a spawn-order footrace.
`--rules hill` is now **exclusive accrual** (sole occupant accrues;
contested zone pays nobody), which makes eviction the game; shared stays
at `--rules hill-shared` as the baseline. Same-day follow-up (DECISIONS
#51): spawns under hill are **zone-distance-fair** (BFS delta ≤ 2), so the
opening race is decided by play, not spawn luck. Open risk to measure in
the gen-4 tournament: two hold-averse bots could stand off adjacent to the
zone — watch draw rate among aware-vs-aware games.

## Gen-4 bracket verdict (2026-07-23, DECISIONS #52)

Three zone doctrines (Bastille fortress / Talon denial / Castellan legacy),
two rounds, one improvement iteration, all under hill v3. **Talon won every
head-to-head** and topped the ladder (1258). The mechanic's data:

| pairing (6 games each)   | round 1 draws | round 2 draws |
| ------------------------ | ------------- | ------------- |
| Bastille vs Talon        | 1             | 1             |
| Bastille vs Castellan    | 1             | 2             |
| Castellan vs Talon       | 4             | 0             |
| any agent vs any champion| 0 (36 games)  | 0 (36 games)  |

Aware-vs-aware draws fell 33% → 17% in one iteration, and the fix was
counter-play, not rule tuning: Talon's sorties broke the freeze-camper
stalemate; Bastille's bait-refusal ended the dodge-bait accrual exploit.
Emergent depth actually observed: shoot-to-bait (not to hit), position
inference from frozen public zone counters, timed off-pad sorties,
lead-aware freeze/evict switching. The hill hypothesis is confirmed:
zone control creates decisions and variety among bots that play it.

**The open ship question was the meta reset, not the mechanic** — resolved
same day: SHIPPED as official 0.4 (DECISIONS #53).

## Gen-5 season premiere: the duel era ends (2026-07-23, DECISIONS #55)

### The spectator cost at the top of the meta (owner-observed)

The crown-defining Bastille-Talon crossfire game measured: leader 454/500
Waits, 5 shots; trailer 117 moves, 108 turns, ZERO shots; zone contested
from t7, 403 frozen ticks — decided 90-0 by t~90, then theater until 499.
Both bots are playing CORRECTLY: a leader's frozen board is a won board
(statue), and shooting a perfect-timing dodger always misses (pacifist
dance). The anti-draw goal held — the game has a merit winner — but this
is the camping problem transformed, not killed, and it worsens as bots
improve. Candidate levers for the next harness run, in test order:
(1) MaxTicks 500→~300 (median decided game is 158; pure rules-data);
(2) contested decay — while contested, the LEADER's ledger decays 1/N
ticks, breaking the statue equilibrium from both sides (root-cause fix);
(3) stalemate sudden death — no accrual and no damage for N ticks →
seed-deterministic zone relocation.

Deeper cut from the same game (owner question "why did Talon never
shoot?"): in 499 ticks Talon NEVER occupied a clear, loaded, aligned
firing line — because lanes are mutual and the action economy favors the
reactor: attacker needs arrive→turn→shoot (2-3 telegraphed ticks,
facing-locked fire), defender needs one sidestep (moves resolve before
shots) or one counter-turn with an always-loaded gun. Between tempo-model
bots, initiated shots beyond point-blank are strictly negative EV; kills
happen only in forced geometry (doorways, bridges, crossings, baits).
Contested decay fixes the stall but not this — the mechanic that does is
an AIMED SHOT candidate: hold facing N consecutive ticks → next shot
resolves BEFORE moves (undodgeable, spectator-readable stance, threatens
statues and dancers alike). Harness it alongside the stall levers.

Final 0.4 ladder: Bastille 1279 (crowned — champions/bastille-gen5),
Talon 1268, Castellan 1244, Meridian 1219, then the duel-era champions
(swept 48-0). Design findings: (a) the meta has depth — a fresh,
well-briefed challenger with one iteration could NOT displace the gen-4
doctrines (rock-paper-scissors persisted: Talon beat Bastille twice but
lost the league); (b) the 6-map pool with 3-of-6 sampling adds real
set-to-set variance — bots strong on the classic trio struggled on
sampled keep/bridge/edge hills; (c) suspected bug to verify: SpawnVariation's
64-attempt fallback returns MAP-FIXED spawns that bypass ZoneSpawnFairness
(a ranked game showed zone distances 1 vs 4+); (d) accepted follow-ups
from Meridian's report: `play` should refresh out/bot.wasm or print the
artifact hash it used; `set` should accept pinned map/seed pairs for exact
A/B; a `maps --show` ASCII render; shot events could carry ray direction.


## Rules 0.4 shipped: zone control (2026-07-23)

Pre-registered harness run, 5 bots (2 frozen champions + 3 zone-aware
doctrines), 3 maps, fixed seeds 101/202/303, 60 games/arm:

| arm  | draw% | decisive% | med tick | aware-aware detail                   |
| ---- | ----- | --------- | -------- | ------------------------------------ |
| 0.3  | 37%   | 63%       | 77       | (aware bots are strong duelists too) |
| hill | 12%   | 88%       | 158      | 18 games: 4 draws, wins 7/4/3 — all  |
|      |       |           |          | three doctrines viable; 0 draws in   |
|      |       |           |          | all 36 aware-vs-champion games       |

Draw rate and decisiveness passed decisively; diversity held. **Median
length doubled and Domination replaced most Eliminations — recorded as the
accepted trade, against the standard criteria's letter**: draws were the
product's disease across four generations; a long decided game beats a
fast dead one. Meta reset accepted deliberately (pre-launch is the
cheapest it will ever be): every pre-zone bot loses to any zone-aware bot,
so the gen-5 title fight under official rules is open — Rampart gen-2's
crown is now genuinely at stake, which is the season story, not a bug.

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

Rules-targeting note (0.4-ship question, resolved by DECISIONS #54): bots
stay deliberately rules-agnostic — one artifact plays whatever ruleset the
match declares — and participation stays universal (the champions ratchet
needs titles defended under current rules, and a pilot-sized queue must
not fragment). What partitions is the RATING: one elo ladder per rules
version, a ranked challenge may pin any resolvable ruleset, and legacy
ladders stay playable forever — a rules era change never vaporizes a
bot's standing, it just opens a fresh ladder.

Map knowledge note (gen-4 question): observations carry no map id, but
MapWidth×MapHeight uniquely fingerprints the 3-map pool (12×8 / 16×12 /
24×18), so a bot can legally ship embedded layouts and skip wall discovery.
That's chess-opening-style skill, not an exploit — maps are public — and
spawn overfit is already impossible (seeded spawns + lane safety + zone
fairness). It stops being healthy if the pool stagnates: the counter is
backlog #1 (grow/rotate the pool) or, further out, seed-generated maps
(validated the same way zone/spawn constraints already are), which would
make wall-memory a live skill again.

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
