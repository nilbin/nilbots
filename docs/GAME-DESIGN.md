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

Follow-up (owner question "why not shoot the camper from behind?"):
there IS no behind — vision is omnidirectional and facing is public, so
the aim-turn is a broadcast telegraph and the sidestep (orientation-
agnostic, resolves before shots) beats any seen shot. The one counter to
a reactive statue — a blind range-7-8 snipe from outside vision at its
remembered tile — is map-dependent: crossfire-01's longest clear lane
onto its zone is 4 tiles (measured), so the map deletes its own hill's
only ranged threat. Two consequences: the aimed-shot candidate converts
exactly this scene into a kill threat; and a zone-map design guideline —
keep at least one 7+ tile lane onto the hill as the anti-statue channel.
Owner direction (same day): CONE VISION promoted to the rules-0.5
flagship candidate — root-cause analysis: every degenerate equilibrium on
record (energy disarmament, statue/dance, 0-for-122 sprays) stems from
perfect information + perfect reaction; directional perception creates
unseen wind-ups, the first landable shots. Zero wire change (visibleTiles
already explicit), spectator-legible (fog view shows the cone; spin-scan
is a readable tell with exploitable lag), composes with the zone. Pair
with a 1-tile omnidirectional proximity ring; MaxTicks trim rides along.
Aimed shot: rejected by owner. Grenades: parked until after cone vision —
under perfect information they collapse into forced-move puzzles, and a
target tile needs parameterized actions (first real protocol bump).
Strafe-attack (move+shoot): declined — it breaks single-action-per-tick
and move-before-shoot, the beams under the whole tempo economy. Owner addition: PROJECTILES (travel time instead of instant rays)
join cone vision as 0.5 co-flagship — "the watchability release".
Mechanics case: a bolt fired at an occupied zone tile forces vacate-or-eat
(missing acquires zoning value; suppression is the eviction tool the
statue equilibrium lacks), and no protocol params are needed (Shoot is
unchanged; bolts are world state — trailing observation section +
additive replay field, both proven hash-safe patterns). Honest caveat:
against a SEEN shooter travel time makes dodging easier — alone,
projectiles may deepen pacifism while fixing camping; paired with cone
vision, seen bolts are dodgeable counterplay and unseen bolts connect.
Harness four arms: 0.4 control / cone / projectiles / both (0.3's
ship-individually lesson vs a candidate genuine combo — data decides).
Spec decisions for the design doc: bolt speed (1-2 tiles/tick),
sweep-collision semantics (bot entering a bolt's tile mid-flight),
bolt-bolt pass-through, in-flight cap via cooldown, viewer rendering.
0.5 design-doc requirement (owner scene): NAMED EXECUTABLE PLAYS —
concrete scenes the tuning must make possible, each with tick-math as an
acceptance test. Entry #1, THE DOUBLE-LANE SQUEEZE: solo attacker forces
a camper fully off a 2x2 zone by making both rows hot simultaneously.
Math: sequential per-lane bolts fail (the camper hops rows on-zone; the
attacker's reposition cycle turn+move+turn ≈ 4 ticks dwarfs a fast
bolt's ~2-tick lane window) — the enabling lever is ProjectileSpeed as a
rules value tuned SLOW (1 tile per 2+ ticks → lane occupancy ≥ the
reposition cycle; also the most spectator-legible variant). Counterplay
stays: the squeeze costs 2 shots + ~8 non-accruing ticks, the camper can
advance on the shooter mid-cycle, keep walls shorten lanes, and under
cone vision wave two can arrive unseen. Falsification test: the gen-5
fortress 90-0 freeze must be breakable under shipped 0.5 numbers.
Further named plays (each pins a distinct spec parameter):
THE BACKSTAB — unseen approach connects (pins cone angle + proximity
ring); THE RED-LIGHT APPROACH — stalk a scanning defender (pins turn/
sweep economics; a spinner can't keep its gun trained — eyes and muzzle
are one resource); THE DECOY SHOT — flash one flank, strike the other
(forces the hearing-radius decision: out-of-cone muzzle events within R
tiles); THE CORNER FLUSH — a wall-backed scanner must still fall to
suppression (no absolute defensive posture); THE SHEPHERD — bolt-deny a
lane so the forced dodge lands in a pre-aimed one (pins two-threat
windows under cooldown 2); THE VANISH — break LOS and genuinely escape,
pursuer must guess (pins memory-divergence gameplay; the zone clock
already punishes hiding); THE VANGUARD PUSH — advance behind a slow bolt
(forces the bolt-bolt collision decision; lean pass-through for 0.5).
ANTI-PLAY (must NOT survive): THE RADAR STATUE — a wall-backed optimal
scanner no composition of squeeze/flush/backstab can evict = 0.5 failed
its own goal.
Status: IMPLEMENTED behind flags (DECISIONS #56) — design doc, engine,
SDK 0.5.0, viewer bolts, play-acceptance tests, 4-arm mechanical harness
(conebolts: 3% draws, the lowest arm ever recorded; combo beats both
singles). Remaining: the gen-6 tournament of 0.5-aware bots = the ship
verdict.

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


Gen-6 verdict: see DECISIONS #57 and DX-FINDINGS-GEN6 (gameplay passes; docs+tooling pass required before 0.5 ships).

## Gen-7 verdict: conebolts is watchable, but bolts do not solve the camper (HOLD)

Two 0.5-aware challengers written FROM THE DOCS (Bloodhound, a sound-hunter;
Bulwark, an armed door-warden), under the hardened conebolts-v3 arm, one
improvement iteration each with the anti-camper counter spelled out
explicitly. Full detail in DECISIONS #61. The finding is nuanced and
decision-relevant:

**What 0.5's information game delivered (validated).** Aware bots crushed the
0.5-BLIND champions Warden and Rampart 6-0 every time — directional vision +
bolts + hearing are a large, real edge over bots that cannot perceive them.
Aware-vs-aware games are FAST and DECISIVE (Bloodhound beat Bulwark with a
t17 point-blank elimination, not a stalemate). Hearing behaved exactly as
designed: Bloodhound navigates by `HeardSounds` bearings, NEVER fires from
sound alone (always converts a sound to a sighting first), and diagonal
bearings stay genuinely ambiguous because it can only face cardinals — search
behavior, not position-tracking. The redaction works.

**What bolts did NOT deliver (the hold reason).** The whole thesis of 0.5 was
that giving missed shots value (bolts as zoning) would break the camper that
0.4 could not. It did not. The reigning 0.4 champion Bastille gen-5 — 0.5-BLIND,
with no idea cones or bolts exist — finished #1 over BOTH purpose-built aware
bots, even after each was handed the Double-Lane Squeeze counter explicitly.
Both agents independently found the same reason: **on a 2x2 zone Bastille plays
a reactive diagonal-mirror** — it sits on the tile diagonal to the attacker
(never alignable, no shot) and slips to the new diagonal on the exact tick the
attacker fires. A single gun (cooldown 2, a bolt occupies a tile one tick)
CANNOT keep two lanes hot at once, so the "double lane" is physically
impossible; the bolt always lands on the tile the mirror just left; and with
no strafe the attacker cannot herd it onto a refuge tile or force a trade
(it eats the first point-blank hit to the perception delay + turn cost). The
2x2 mirror is mathematically unbreakable by one gun under no-strafe. The
effective counter that DID emerge was not offense at all but **zone-turtle**:
a contested zone pays nobody (exclusive accrual, a 0.4 mechanic), so once
ahead you have already won on the clock — hold a diagonal standoff and let it
run. This dragged Bastille from a 0.5-5.5 blowout to a spawn-decided ~coin
flip (2W-3L-1D on the 2x2 map, every decisive game a MaxTicks zone-race, not
a kill). So the anti-camp work is being done by 0.4 zone control, NOT by the
new 0.5 bolts.

**The bolt promise fails on geometry too.** arena-01's split zone is two
vertical 1x2 pads: a pad has NO in-pad dodge tile, so an off-pad column-camper
kills even a 28-0 zone-tick leader. That is an indefensible-map problem the
new mechanics cannot fix.

**Ten pre-registered ship criteria (RULES-0.5-DESIGN §H) scorecard:**
PASS — #4 shot count holds, #5 ranged hits land, #7 duration improves (fast
decisive games), #9 hearing = uncertainty not tracking (strong). PARTIAL —
#1 conebolts beats control on the BLIND population but not vs the strong
camper; #3 aware bots fire, but firing is not the camper answer; #6 two
doctrines viable vs the field yet both converged on turtle and both lose to
Bastille. FAIL — #2 the Radar Statue was NOT broken in a ranked replay (the
decisive one), #8 the fortress/2x2-mirror is NOT breakable, #10 BOLTS do not
individually justify their complexity (cone + hearing do). Three fails, all
on the anti-camp promise that motivated bolts.

**Verdict: HOLD conebolts — do not promote to official 0.5 as-is.** Cone
vision + hearing are validated and could ship on their own; bolts as an
anti-camp tool are not proven and, on a 2x2 under no-strafe, are provably
insufficient. The forward levers both agents named: (a) ship cone+hearing
WITHOUT bolts; (b) redesign bolts to threaten the mirror (a second
simultaneous lane / spread, longer occupancy, or a limited strafe so a
shooter can pin — but strafe risks reopening the dodge-everything problem
0.5 removed); (c) fix zone geometry (kill the 1x2 pads; 2x2 zones structurally
favor the diagonal mirror). The tournament did its job: it found precisely
where the mechanics do and do not deliver, with mechanistic proof and named
fixes, which no mechanical harness or scripted test surfaced. No crown moves
(experiment rules; Bastille gen-5 defends the 0.4 title).

## Post-gen-7 direction: change the reward loop before the weapon

The selected redesign is not spread, strafe, residue, or undodgeable fire.
It first removes free objective income while defending: only a successful
Wait on a zone tile actively holds, and the objective becomes a shared
decaying pressure meter rather than permanently banked per-bot ticks.
Consequently a missed bolt has value when it forces Move/Turn/Shoot: the
defender survives but does not score, and an abandoned lead decays.

With that economic consequence in place, bolts are retested as actual fast
projectiles at one and two ordered tiles per tick. The two-tile arm checks
every intermediate wall, bot, and range tile and is continuously animated in
the viewer. Cone vision and redacted hearing stay frozen. Ranked zones become
connected 3×3/3×2 regions; the narrow causeway remains an adversarial map
outside ranked. Exact v4 arms, timing, observation fields, scripted gates,
and ship criteria are in RULES-0.5-DESIGN §J and DECISIONS #62.

## Gen-8 verdict: the camper breaks, but the tempo gate does not pass (HOLD)

Four docs/CLI-only bot authors independently produced the requested active
holder, suppressor, sound hunter, and mobile flanker, then received one
bounded improvement iteration from replay summaries. Together with unchanged
Bastille they played three shared seed profiles under every v4 arm: 180 games
per arm, 900 final games.

| arm | draws | eliminations | median | average | MaxTicks | leader |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| control | 21/180 (11.7%) | 112/180 (62.2%) | 43.5 | 101.1 | 12 | Bastille 61 wins |
| cone-control | 14/180 (7.8%) | 112/180 (62.2%) | 50 | 103.7 | 12 | Bastille 56 |
| cone-active | 19/180 (10.6%) | 109/180 (60.6%) | 58.5 | 95.6 | 10 | Bastille 50 |
| cone-active-bolt1 | 15/180 (8.3%) | 92/180 (51.1%) | 86.5 | 139.1 | 23 | ActiveHolder 53 |
| cone-active-bolt2 | 15/180 (8.3%) | 97/180 (53.9%) | 71 | 134.9 | 24 | ActiveHolder 51 |

The central design correction works. Moving, turning, scanning, and shooting
do not earn pressure. Unchanged Bastille is no longer self-sufficient once
bolts make that commitment contestable: it falls from an undefeated control
leader (61-0-11) to second under bolt2 (45-16-11), behind a genuinely
different active-holding doctrine (51-13-8). Suppressor remains viable at
31-35-6, so the field does not collapse to one policy. Bolt2 records 291
ranged projectile hits and changes enough paired outcomes to be strategically
real rather than visual decoration.

Bolt2 is the better speed candidate. Against bolt1 over 180 paired games it
holds draws equal, adds five eliminations and 18 ranged hits, and cuts median
duration by 15.5 ticks and average duration by 4.2. Faster ordered traversal
therefore survives the comparison without making dodging irrelevant.

It still does not ship. Relative to matched control, bolt2 improves draws but
worsens both elimination share and duration. Twenty-four games reach tick 499;
their median absolute final pressure is near zero, so lowering the ±100 limit
would help only a few outliers. These are prolonged aware-vs-aware combat and
contest loops, not permanently banked leads. The next test should isolate a
minimal late-game resolution/overtime rule for near-zero pressure while
freezing cone, hearing, active holding, map geometry, and bolt2 traversal.
Continuously strengthening the weapon is not the next move.

### Pre-registered v5 test: short, non-decaying control overtime

Replay classification found one dominant late loop rather than a general
combat slowdown. Fifteen of the 24 bolt2 MaxTicks games repeat a ten-tick
holder/suppressor cycle: 40 versus 20 sole holds, 40 defensive no-holder
ticks, 20 missed launches per final 100 ticks, and decay cancelling the net
20-hold advantage.

The v5 isolation arm keeps bolt2-v4 unchanged until tick 200. Overtime then
uses a ±10 pressure target and stops nobody-holding decay while preserving
the current signed pressure. Exact frozen surfaces and acceptance gates are
in RULES-0.5-DESIGN §L. This tests late resolution only; it does not redefine
or waive the official elimination-share gate.

### Gen-8 v5 result: overtime fixes the long tail, not the ship gate

| arm | draws | eliminations | median | average | MaxTicks | leader |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| bolt2-v4 | 15/180 (8.3%) | 97/180 (53.9%) | 71 | 134.9 | 24 | ActiveHolder 51 |
| bolt2-overtime-v5 | 15/180 (8.3%) | 97/180 (53.9%) | 71 | 102.0 | 5 | ActiveHolder 51 |

This is a successful causal experiment. Nineteen MaxTicks results become
Domination, seven late Dominations finish earlier, and 5,940 aggregate ticks
disappear. Draws, eliminations, the leader, and the five-doctrine ordering
stay stable. Overtime winners split 14/12 by slot; one game flips winner when
net active holding replaces the old MaxTicks health/damage tiebreak.

Keep overtime as the v5 flagship but keep official rules at 0.4. Relative to
the matched control arm, v5 passes draw rate and diversity, essentially ties
average duration, but still fails median duration and elimination share.
That remaining question is now explicit: either find a combat/tempo design
that restores eliminations without undoing active commitment, or deliberately
redefine the gate around decisive objective endings. Do neither silently.

Structural review prevents v5 itself from becoming a ship candidate: stopping
decay permanently banks an abandoned overtime lead, contradicting the active
control promise. The pre-registered v6 arm uses doubled overtime hold gain
with normal decay instead. Its numeric gates are in RULES-0.5-DESIGN §L.

v6 passes. It matches v5's 15 draws, 97 eliminations, median 71, five
MaxTicks, complete per-bot records, doctrine order, and 14/12 overtime slot
split. Average duration is 102.2 instead of 102.0; preserving decay costs
only 45 aggregate ticks across 180 games. The explicit abandonment test also
passes. v6 therefore supersedes v5 as the experimental flagship without
changing the official HOLD verdict.

### Programmed-arc theory gate

Curved projectiles do not solve perfect defence when their complete future
path is exposed before movement. Before changing the SDK or WASM guest, the
shot theory lab exhaustively modeled privately committed, immutable arcs:
three initial headings, selectable bend start/cadence, at most 135 degrees of
total sweep, strict diagonal corners, range eight, and speed two.

One immediate launch tile is the retained timing. Across 84 open-floor
distance-two-to-four states, 53 are genuine prediction contests and zero
contain a shot that is unavoidable when its path is known. Two immediate
tiles raise prediction states to 64 but also create 12 forced-attack states,
so that faster launch is rejected.

Across all 10,240 ranked-zone local states, the one-tile design produces 3,552
prediction contests, 3,003 universal defences, 201 geometry-created forced
attacks, and 3,484 irrelevant/out-of-envelope states. Every ranked map supports
both prediction play and a legal around-wall path. The theory passes; an
engine experiment is justified, while official 0.5 remains on HOLD. Exact
semantics and tables are in RULES-0.5-DESIGN §M.

### Programmed-arc engine result

The v7 implementation keeps v6 unchanged except for private immutable shot
programs. Across the same five doctrines and 180 paired games, arcs reduce
draws 15→11, raise eliminations 97→106, cut median duration 71→61 and average
102.2→87.2, and reduce MaxTicks finishes 5→3. Every doctrine still wins;
Suppressor, the sole program-selecting bot, improves 30→43 wins.

This is genuine projectile play rather than unused API surface: 821 paths
physically bend, 110 hits land after a bend, 111 curved hits land beyond the
launch tile, and five curved misses cross a zone tile an active holder just
vacated. The retained uncertainty is only future committed intent; current
heading, speed, remaining range, action limits, and completed path remain
exact. RULES-0.5-DESIGN §N contains the frozen contract and full verdict.

### Gen-9 docs-only usability result

A new author restricted to player documentation, public SDK source, and the
CLI independently produced Helix: an active objective holder that enumerates
legal private programs, previews paths against remembered terrain, predicts
movement/refuge tiles, searches from redacted sound, and dodges manifested
speed-two bolts. Its final WASM matched the server artifact byte-for-byte.

Helix beat unchanged Bastille 4–2, Rampart 4–2, and Warden 5–1 in ranked-format
v7 sets. Its self mirror drew 3–3; all six games ended by elimination in
11–31 ticks. This passes the player-learnability gate and demonstrates that
Bastille's historical passive mirror is no longer sufficient by itself.

The geometry gate is also mechanically complete: every ranked zone is a
connected region of at least four tiles with two-dimensional local movement,
multiple approaches, and surrounding attack space; the narrow 2×2 causeway
is adversarial-only. The trial's three champion sets collectively covered all
five ranked maps.

Do not crown Helix or ship 0.5 from this bounded trial. The remaining promotion
step is the matched v7-versus-shipped-0.4 tournament with an aware population.
That comparison—not another combat mechanic—is next.

## Gen-9 full promotion verdict: better replays, strict gate still HOLD

The final comparison froze six all-WASM doctrines, including unchanged
Bastille and docs-only Helix, across 810 games. Programmed-arcs v7 improves the
parts of the viewing experience that motivated 0.5: versus shipped 0.4 it
cuts draws 11.5%→6.3%, average ticks 120.1→99.0, p90 499→200, and MaxTicks
37→14. Damage occurs in slightly more games (70.0% vs 67.8%), 224 ranged
curved hits land, and 36 misses visibly force an active holder off a crossed
zone tile.

The defensive equilibrium is broken without collapsing the meta. Bastille
moves from 82-0-8 under 0.4 to 49-29-12 under v7; Helix leads at 67 wins,
ActiveHolder follows at 53, all six doctrines win, and top share of decided
games falls 34.3%→26.5%.

The frozen balance gate nevertheless says HOLD. Median duration is 64.5
instead of 31.5 and elimination share is 55.9% instead of 64.8%. V7 produces
105 Domination endings, and the methodology does not permit relabeling those
as eliminations after seeing the result. The exact passive-control pairing is
also mixed: eliminations rise 146→151 and mean duration falls 135.6→99.0,
but draws rise 14→17 and median rises 50→64.5.

This exposes a clean product decision. Instant rays create many fast
elimination labels; active control and legible projectiles create fewer draws,
a much shorter tail, broader winners, and objective climaxes. If the latter is
the intended spectator experience, define that gate explicitly before the
next data: absolute viewing time at the viewer's five ticks/second, damage/
pressure engagement, decisive objective endings, replay review, and strategic
diversity. Publish a deterministic non-highlight sample and test the frozen
candidate on fresh holdout seeds. Keep the old table visible.

Do not change health on this evidence alone. A frozen-policy second-hit
diagnostic still misses both failed 0.4 thresholds (62.6% eliminations,
median 40); first-hit lethality would meet the numbers but creates at least
16 observed mutual-first-hit situations and overweights one hidden prediction.
The next move is to decide what “good to watch” means, then validate it without
post-hoc scoring.
