# Arc Relay whole-map candidate: Broadloom 37×27

Date: 2026-08-22
Status: **unimplemented design candidate** — analysis only. No promotion, no
`Current` change, no workshop rules, no gameplay-rule change, no sheet
migration.
Artifacts: `docs/map-candidates/broadloom-37x27/` (deterministic builder,
tactical verifier, atlas spec + regenerator, analyzer JSON, interactive
atlas HTML). The atlas is the owner-review page.

## Verdict

Broadloom answers the baseline report's structural finding — Tapline
concentrates every strategically productive function on the centre — by
splitting the map's value onto a cross: the **scoring run is the mid row**
(reactor → Wells → reactor) and the **development axis is the centre
column** (N workshop → centre Well → S workshop). Each team's 4-value raid
target sits in the *opposite* corner from its own supply, so raids, workshop
pressure and heal denial all route through the two side theatres instead of
through centre. Every hard topology gate passes with measured numbers; the
named plays below are drawn with exact posts on the real terrain. The open
questions that geometry cannot settle (deathball income versus raid-and-train
counterplay, workshop camping, heal turtling) are listed as playtest
hypotheses, not as claims.

## 1. Invariants, platform constraint, reopened hypotheses

Preserved mode invariants: two teams × eight stable slots; three Wells at the
current team-relative 1/2/4 charge; exact-reactor banking inside the
protected pads; two healing tiles; two neutral workshops distinct from Wells
and heals; current combat, carrying (2-tick carrier relocation), visibility
(vision 5 facing-quadrant, zero omni ring, corner-strict supercover) and all
sixteen signature rules read live from
`ArcRelayLoopProfile.Current` = **Tapline15**; deterministic corner-strict
movement; exact 180° competitive symmetry.

Platform constraint (disclosed, **not** implemented here):
`ArenaMap.MaxWidth = MaxHeight = 32` (`src/BotArena.Engine/ArenaMap.cs:30`).
Broadloom is 37×27 = 999 total tiles — under the historical 1,024-cell
budget but over the width cap. Promotion requires an explicit bounded
width-cap change (≥ 37) plus review of observation/wire budgets, replay,
renderer scale, editor geometry and their tests. That change must ship as its
own reviewed platform decision, never hidden inside a map delta.

Everything geometric was reopened: canvas, reactors, spawn shape and exits,
all three Wells, both heals, centre size and cover, outer routes, wall
grammar. Broadloom shares **no terrain** with Tapline15; the live map remains
the measured comparator only.

## 2. Abstract strategic route graph

Drawn before any wall. Nodes and quarter assignments (west perspective;
east is the exact 180° rotation):

```
        NW loop ── roof gallery (y1) ── NE field ── E deep field
           │            │    [skylight]      │           │
  own HEAL ┤       N WORKSHOP (18,4)         ├─ ENEMY WELL (26,7)
           │            │  forks             │      [4-value raid target]
  pad exit ┴─ NW J ─────┼──────────── NE J ──┴─ enemy home lane
  (north)  ─ avenue     │                              │
W REACTOR ══ delivery ══ CENTRE WELL (18,13) ══ delivery ══ E REACTOR
 (3,13)      corridor   │   [mid spine y13]   corridor      (33,13)
           ─ home lane  │                              ┌ pad exit (south)
  OWN WELL ┤       SW J ┼──────────── SE J ────────────┤
  (10,19)  │            │  forks                       │
[their 4-  ├       S WORKSHOP (18,22)                  ├ enemy HEAL (28,20)
 raid tgt] │            │                              │
        SW deep ── floor gallery (y25) ─────────── SE loop
```

Intended relationships:

- **Every quarter holds exactly one landmark** (NW: own heal, NE: enemy
  Well, SW: own Well, SE: enemy heal) plus a junction; the two workshop
  theatres span the north and south bands between the quarters. Eight bodies
  always have more theatres than they can hold.
- **Raid/supply opposition.** West's supply is SW; west's raid target is NE.
  A team massing centrally covers neither band; a team defending its supply
  quarter is far from its raid lanes. Chirality: west's extra pad exit faces
  north (its raid side), east's faces south.
- **Graded commitment along each band.** For west pushing north: own heal
  (safe) → NW junction → workshop (neutral, mid commitment) → enemy Well
  (deep, 4-value) → enemy home lane. Feints fall out of the geometry because
  the workshop and raid approaches share their first half.
- **Profitable trades exist against a deathball.** If east balls at centre
  (2s, throttled by the 46-tick bank-triggered recharge), west can bank its
  own 1s (8-step run), train at either workshop, or take the 24/28/37-step
  raid families at the undefended NE Well — each a productive allocation that
  does not fight eight bodies.
- **Rejection test.** Centre is *not* the best assembly (junctions and the
  x12 staging column are), not the heal pivot (heals are 13 away, versus 8
  on Tapline), and not on the workshop approach families (centre → workshop
  is 10 through 1-wide fork slips). It remains the best *scoring* crossroads
  only — which is what a 2-value Well should be.

## 3. Exact geometry

`docs/map-candidates/broadloom-37x27/build.py` authors the upper half plus a
palindromic middle row; the lower half **is** the rotation. 681 open tiles
(68%), one corner-strict component, chunky Tapline-style wall grammar.

```
    0123456789012345678901234567890123456
  0 #####################################
  1 #..........#.............#..........#
  2 #......##.....####.####......##...#.#
  3 #....#...........#....#.........#...#
  4 #..#########......T.#....###....#...#
  5 #..###.....#..#.#......#.....#..#...#
  6 #....#..+..##.#..........#...##.#...#
  7 #.#...........##.....##...4..#......#
  8 #.....##.##......###.......#........#
  9 ###..........##..###...##......######
 10 #____.###.....#........#..###...____#
 11 #____ddd..#...#.#...#.......#ddd____#
 12 #____ddd..#............#..#..ddd____#
 13 #__R_ddd..##...#..2..#...##..ddd_R__#
 14 #____ddd..#..#............#..ddd____#
 15 #____ddd#.......#...#.#...#..ddd____#
 16 #____...###..#........#.....###.____#
 17 ######......##...###..##..........###
 18 #........#.......###......##.##.....#
 19 #......#..1...##.....##...........#.#
 20 #...#.##...#..........#.##..+..#....#
 21 #...#..#.....#......#.#..#.....###..#
 22 #...#....###....#.T......#########..#
 23 #...#.........#....#...........#....#
 24 #.#...##......####.####.....##......#
 25 #..........#.............#..........#
 26 #####################################
      R reactor · _ spawn pad · d delivery · 1/2/4 Wells · + heal · T workshop
```

West landmarks: reactor (3,13) inside pad x1–4,y10–16; delivery x5–7,y11–15;
own Well (10,19); own heal (8,6); pad north exit (3–4,9). Neutral: centre
Well (18,13); workshops (18,4)/(18,22) on the centre column, equidistant
from the reactors by construction. East = rotation.

## 4. Strategic distances (all pinned in the atlas)

| Measure | Tapline15 | Broadloom |
| --- | ---: | ---: |
| Canvas / open floor | 31×27 / 517 | 37×27 / 681 |
| Own Well → own reactor | 12 | **8** |
| Centre → own reactor | 19 | 16 |
| Enemy Well → own reactor (fast / heal / shadow) | 31 / 33 / 41 | **24 / 28 / 37** |
| Centre → own Well | 22 | **10** |
| Centre → nearest heal | 8 | **13** |
| Own reactor → near heal | 16 | **9** |
| Reactor → reactor | 38 | 31 |
| Reactor → workshops | — | **15 / 18** (3-step supply-side bias) |
| Centre → either workshop | — | 10 |
| Workshop → adjacent owned Well | — | 8 |
| Workshop → own heal | — | 12 |
| Workshop ↔ workshop | — | 19 |
| Deep committed roof flank, exit → enemy reactor | — | 39 |

Twenty-three routes are pinned with `expectedSteps` in
`atlas.spec.json`; any terrain drift fails the analyzer loudly.

## 5. Topology findings

From the analyzer (`analysis.json`, valid, zero errors) and `verify.py`:

- one corner-strict connected component (681 tiles); no false diagonal
  passages; **no meaningful cut vertex** at stub allowance 2; exact 180°
  terrain and landmark symmetry.
- **Spawn projection: zero.** No pad tile has rail-range (12) line of sight —
  or even vision-range sight — to any Well, heal or workshop. Vertical
  through-pad rail alleys were explicitly walled off ((3,4)–(4,5) block and
  its rotation; (31,9)/(5,17) pinches).
- **Seal audit, every major site.** All one- and two-tile removals within
  radius 5 of both workshops, all three Wells and both heals: **no removal
  pair disconnects any site**; worst bounded detours are +6 (workshops), +1
  (centre Well), +5 (owned Wells), +10 (heals, which trade three 1-wide
  doors for deep home positions). The 1-wide skylight and fork slips are
  deliberate sealable *slips*, never the only family. This audit caught and
  fixed one hard failure during design (below).
- **Straight-lane discipline.** After teeth placement, the open straights
  ≥ 12 are the two roof galleries (13 long, 1-wide committed flanks whose
  rail overwatch is the authored counter to skylight raids, single-file
  under the live honest-firing rule) and the two exactly-12 mid-band rows
  (y12/y14), whose shooters must stand inside the exposed corridor ends.
  The longest lane through a training tile is 8 (< rail 12, so an
  interrupting Longshot must post inside the contested band).

## 6. Site cards

**N workshop (18,4)** — *pass.* Room x15–21,y3–6 (7 wide, 4 deep, three
cover pillars; trainee tile has seven exits). Mouths: W (14,3–4, 2-wide,
from the NW band), S antechamber (16–20,7, over the island forks, from
centre), E (22,4–6, 3-wide, from the NE slip/field), plus the 1-wide
skylight (18,2) under the roof gallery. Facing audit: **each cardinal facing
from the trainee sees exactly one mouth** — solo defense leaks information by
construction. First observation: attackers cross vision range in the band /
gallery / forks before any mouth. Interrupt lanes: band rail (12,4)→(19,4)
and skylight rail (18,1)→(18,4); both start on exposed, reachable ground and
each is blocked by one Hardlight or an allied body. Seals: bounded above
(+6). Continuations: raid the adjacent Well (8), fall back to own heal (12),
reinforce centre (11). Authoring: posts, screen areas and patrol loops all on
open rectangles; no raw JSON.

**S workshop (18,22)** — *pass* (exact rotation; same numbers, east's near
band).

**Centre plaza (18,13)** — *pass.* 9×7 chamber with staggered cover
((15,13),(21,13) row pillars, (16,11)/(20,11)-family stagger, off-axis
(23,12)/(13,14) breaks). Longest rail line onto the Well is 5, so no mouth
post owns the objective, and the Well keeps all eight corner-strict exits —
the seal audit proves no two-tile pair can entomb it. W/E mouths 3-wide;
N/S approaches arrive over 1-wide fork slips — pincers stay separate until
one step from the plaza. Formation: 7-wide rows at y12/y14, 5-wide at y13.
Null radius covers 41 tiles but 20 escape-ring tiles exist. It is the best
scoring crossroads and nothing else; assembly happens at the x12/x24 staging
columns (5-tall cross-sections).

**Enemy-Well room (26,7)** — *pass.* 5×5 room, three wide door families (W
raid mouth 23,6–8; S mid mouth 24–25,10; E home lane 29,8–9) plus two 1-wide
N shadow seams (24,4)/(28,4). Guard cannot watch all doors from one facing;
guard post (27,5) covers the raid mouth, mine turn (25,9) predicts the S
mouth. East reaches it from delivery in ~5 steps (defense response), west's
raid arrives through the workshop theatre. Continuations for a successful
raid: three measured return families (25/28/37).

**Own-Well room (10,19)** — *pass* (rotation). The 8-step home door is the
defender's strength; the enemy raid door (13,18–20) faces east's committed
band. Flag: the short own-run makes uncontested banking safe — income is
recharge-gated (46 ticks), so the knob is risk, not rate. *Playtest
hypothesis.*

**Heal rooms (8,6)/(28,20)** — *pass with note.* 5×3 rooms, three doors, no
overlap with any objective. One Null blankets the whole room → heal denial
is real counterplay; three doors keep it escapable. Heals are home-quarter
(9 from own reactor, 13 from centre): recovery near home compounds defense,
not centre control; deep healing at the *enemy* heal is a measured 26-step
desperation continuation. *Turtling hypothesis flagged.*

**Roof galleries and deep fields (y1/y25 bands)** — *pass as designed.*
1-wide, 13-long galleries over the workshops with 1-wide crossings
((11,2)/(25,24) pinches) — committed flanks with real payoffs at both ends
(skylight, slip lanes, deep raid). Mine/burst/hook geometry is strong at the
pinches; allied blocking makes them single-file by rule.

**Mid corridors (x8–13 / x23–28)** — *pass.* 5-tall staging cross-sections
at x12/x24 narrowing to 3-wide mouths; chunky teeth deny through-rails and
give Prism Screen (3 frontal segments) exactly one corridor-filling play
with side pockets as the answer.

**Home pads** — *pass.* Reactor inside the pad as on Tapline; delivery
shoulder is the defended ground in front. Single chiral exit per side plus
the delivery face; no protected post projects anywhere that matters.

## 7. Signature-affordance matrix

Live values (grammar 3, combat scale ×10, strike windup 1): Dash 4 · Toss
r5 fly3 · Exchange r6 · Rail r12 dmg20 windup1 · Hardlight 30t hull30 ·
Prism Screen 3-seg hull30 6t · Trip hull10 dmg20 reveal2 · Sentinel r4
dmg10 2t-cadence 30t bolts · Star r8 5-tile cross tell2 · Hook r6 pull3 ·
Paint r7 3×+10 · Smoke r6 rad2 10t · Flare r8 reveal4 8t team-scoped ·
Null rad3 5t tell1 · Burst push1 · Repair r4 10hp/2t max20.

P primary · S situational · C counter · D dominance-watch · – none.

| Signature | Workshops | Centre | Well rooms | Heals | Galleries | Corridors | Homes |
| --- | :-: | :-: | :-: | :-: | :-: | :-: | :-: |
| Vector Dash | S | S | S | – | **P** | P | – |
| Arc Toss | S | S | **P** | – | **P** | P | S |
| Exchange | **P** (trainee rescue) | S | P (carrier rescue) | S | C | S | – |
| Hardlight | **P**/C (skylight, slips) | S | P (seams) | S | **P** (pinches) | S | – |
| Prism Screen | S | P | S | – | – | **P**/D (3-wide mouths; side pockets answer) | S |
| Trip Node | **P** (slips, mouths) | S | **P** (mine turns) | S | **P** (pinches) | S | – |
| Sentinel | **P** (one mouth each) | S | **P** | S | S | S | C |
| Rail Line | **P**/C (band + skylight lanes) | S (5-cap on Well) | S | – | **D-watch** (13 lanes, 1-wide, self-blocking) | S | – |
| Falling Star | **P** (antechamber clusters) | **P** | S | S | – | S | C |
| Tractor Hook | S | S | P (pull guard off post) | – | **P** (pull off the roof) | S | – |
| Target Paint | S | **P** (two allied arcs exist) | S | – | – | P | – |
| Smoke | **P** (mouth denial) | **P** | P | S | **P** (crossing cover) | S | C |
| Survey Flare | **P** (pre-raid scout, anti-smoke) | S | **P** | S | P | S | – |
| Null Field | S/D-watch (38-tile blanket, 13-tile ring) | S (20-tile ring) | S | **P**/D (blankets the room) | – | S | C |
| Kinetic Burst | P (shove off slips) | S | S | S | **P** (shove off the roof) | S | – |
| Repair Beam | **P** ((15,5) pocket, r4 to trainee, raidable) | S | P | C-adjacent | – | S | S |

Every family has ≥ 2 credible homes; no site requires one exact class to stay
playable (each workshop mouth family works with guns alone). The two D-watch
cells are deliberate and carry authored answers (see cards).

## 8. Named play proofs (eight slots accounted)

Each proof names a legal squad (eight slots, at least seven distinct
classes, at most one duplicate) and accounts for every slot: bodies not in
the play hold a named job, never "idle". Where a proof lists fewer than
eight bodies, the remainder are stated in place.

1. **Centre split and pincer.** Approach: 4-body main (Longshot, Sunder-slot,
   Patchbay, Palisade) stages at the x12 column; 2-body arm (Switchback,
   Kestrel) slips NW junction → forks (15–16,9); 2 stay on own-Well custody.
   Setup: posts (12,12)/(12,14) facing east, Longshot (11,12) railing the
   y12 lane (capped at x17 by the inner cross). Trigger: enemy crosses the
   plaza mid (visible through the W mouth). Payoff: mouth line holds the
   front; the arm enters at (15–16,10) onto rear arcs (×2). Continuation:
   push the E mouth, or the arm rotates to the N workshop (11). Counter:
   west-facing Prism Screen plus Null at the W mouth freezes the front; the
   south forks pincer back.
2. **Outer-Well raid, two real returns.** 3 raiders (Kestrel carrier, Veil-
   slot, Towline-slot) commit: exit (4,9) → gallery → slip x23 → W mouth
   (23,6–8); two hold own-Well custody, three keep centre presence.
   Smoke the guard post (27,5), hook the guard off it, grab.
   Fast return 25 through the plaza (risky, crosses the defense); shadow
   return 37 over the roof with a re-smoke at the (11,2) pinch; wounded
   return 28 through own heal. Counter: east's delivery is 5 steps from the
   S mouth; trip node at (25,9); sentinel on the raid mouth.
3. **Train and screen.** Trainee on (18,4); Mason + Palisade screen; the
   other five split centre presence and own-Well custody. Mason
   answers the band rail by blocking (13,4) or seals the skylight when the
   roof is contested; Palisade screens the antechamber. Trigger: flare or
   gallery contact. Payoff: uninterrupted XP. Continuation: trained body
   raids the adjacent Well (8) or reinforces centre (11); screen rotates
   with it. Counter: proof 4.
4. **Two-route workshop disruption.** 2 via NE field → E mouth; 2 via forks
   (four defenders hold home and centre);
   → S slips. Families merge only at the room. The defender's facing audit
   (one mouth per facing) forces a choice before the second arm reveals.
   Counter for the trainer: trip node at (23,3), Hardlight one slip, screen
   the other; the disruptors' own counter-counter is to trade into the
   S workshop instead — a map trade, not a rematch.
5. **Workshop feint into outer raid.** 2 bodies posted visibly at
   (17,7)/(19,7) draw the Well guard toward the antechamber; 2 staged on the
   roof at (20–21,1) drop through the slip into the lightened W mouth.
   The feint pair falls back through the forks to the plaza — a useful job,
   not idling. Counter: east refuses the rotation and answers on the other
   band (S workshop pressure), which is exactly the intended trade fabric.
6. **Exact-post exit ambush with continuation.** Three bodies guard the own
   Well (five stay forward): west guards its Well
   against east's raid: guard (9,21) facing east, mine on the inner turn
   (11,17), Longshot at the N mouth watching (13,18–20). Trigger boundary:
   the raider becomes visible entering the E mouth — one full move before
   contact. Payoff: mine + crossfire inside the 5-wide room. Continuation:
   pursue through the SE junction *or* swing to the S workshop (8 steps).
   Counter: east flares the room first, smokes the guard, or enters through
   the 1-wide S seams instead.
7. **Patrol and re-fortify.** Minesmith + Nest patrol (six hold the usual
   theatres); the pair walks a closed loop: NW
   junction → gallery west end → skylight check → avenue → heal S mouth →
   junction. Mines live at the (11,2) pinch and the x13 column tooth; the
   sentry seeds (12,2) covering the gallery entry (team vision). The loop
   keeps a breach (the loop crossing) and a counter-route (avenue).
   Counter: burst the sentry post, or commit the far band while the patrol
   is mid-loop.
8. **Core convoy through a side theatre.** A raided 4-core comes home over
   the roof (four escort, four hold mid and home): Palisade leads (screen forward), Relay carries, Patchbay
   trails inside repair range, Longshot posts *below* the west gallery end
   at (12,2) — off the single-file lane so allied blocking never wastes the
   overwatch. At (16,1)→(21,1) the Relay can Arc Toss the core five tiles
   past a contested seam. Counter: hook or burst the carrier off the roof at
   a pinch; the convoy's answer is the drop at (12,2) into friendly ground.

## 9. Authorability

Every post, area, patrol point, via and signature mark above is a legal open
tile expressible in the map-first Tactics editor: rooms are rectangles, all
patrol loops close, no via crosses an unenterable region, and every zone's
largest corner-strict connected set exceeds the bodies sent (the 1-wide
slips are used as vias, never as multi-body posts). Trigger conditions used
(contact, enemies-at-zone, core-loose/-taken, held) are all observable team
facts in the current condition vocabulary. The slot-named-posts feature keeps
per-bot posts stable across group refactors. No raw JSON, no hidden anchors.

## 10. Hard failures, accepted tradeoffs, hypotheses

Hard failures: **none open** — all gates in §5 pass with measurements. One
hard failure existed transiently during design and is disclosed for the
record: two late anti-rail walls placed directly north and south of the
centre Well ((18,12)/(18,14)) reduced the Well to exactly two corner-strict
exit tiles, so two Hardlights could entomb it for 30 ticks. A radius-3 seal
audit missed it; the widened radius-5 all-sites audit (now permanent in
`verify.py`) caught it, and the breaks moved off-axis to (23,12)/(13,14).

Accepted tradeoffs (deliberate, documented):
1. Roof galleries are 13-long rail lanes — 1-wide, self-blocking, reachable
   from both ends; they are the committed-flank identity.
2. Own-Well run is 8 (Tapline 12): banking is safer; income stays
   recharge-gated; interception moves to the raid theatres.
3. Workshops carry a 3-step supply-side approach bias per team (15 vs 18).
   Genuinely neutral by symmetry across the pair; measured, not hidden.
4. Heals are home-quarter: strong for defensive recovery, nothing for a
   deep attacker except the 26-step desperation raid on the enemy heal.
5. Reactor→reactor is 32 (was 38): faster home pressure under 30-tick
   respawn waves; the pads' zero-projection audit keeps it honest.

Playtest hypotheses (geometry cannot decide): deathball-versus-trades income
balance under the 46-tick recharge; workshop camping rate once workshop
rules exist; heal-turtle viability; whether the 1-wide slips read fairly in
live play.

## 11. Geometry evidence versus balance claims

Everything above is terrain measurement and authored-play existence proof.
Nothing here is population-balance evidence: steps are not ticks (carriers
relocate every 2 ticks; traffic, combat, facing and signatures add more),
and a geometrically available play may still be too weak, too strong or too
hard to author well. Next step if the owner likes the shape: approve the
width-cap platform prerequisite, then deterministic match probes on a
Broadloom build — before any workshop-rules tuning.

## Verification appendix

```
python3 docs/map-candidates/broadloom-37x27/build.py      # symmetric, connected,
                                                          # 0 corner-touch, 0 cut vertex, 681 open
python3 docs/map-candidates/broadloom-37x27/verify.py     # spawn LOS none; radius-5 seal audit over
                                                          # 7 sites: no disconnect, detours bounded;
                                                          # one-mouth-per-facing; lanes 8/7; spans ok
python3 docs/map-candidates/broadloom-37x27/make-atlas-spec.py
python3 .claude/skills/analyze-arc-relay-map/scripts/map_analysis.py \
  docs/map-candidates/broadloom-37x27/atlas.spec.json \
  --json-out .../analysis.json --html-out .../atlas.html  # valid: true, errors: []
python3 .claude/skills/analyze-arc-relay-map/scripts/test_map_analysis.py  # 15 tests OK
```
