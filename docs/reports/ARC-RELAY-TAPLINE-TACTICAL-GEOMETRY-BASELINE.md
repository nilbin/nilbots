# Arc Relay Tapline tactical-geometry baseline

Date: 2026-08-22  
Baseline: `ArcRelayLoopProfile.Current` / Tapline15  
Scope: analysis and candidate requirements only; no terrain or rules change

## Verdict

Tapline has useful local geometry, but its strategically productive geometry is
too concentrated around the three Wells. The centre is the safest general
answer because it combines immediate scoring access, the map's broadest
formation space, short access to both healing quarters, and acceptable onward
routes. The outer shadow corridors and several good ambush/guard pockets exist,
but spending bodies there normally gives up the only objectives that win the
match. That is the structural reason sophisticated setup play struggles to
compete with a well-executed eight-body march.

The workshop addition should be used as the occasion to question the entire
map, not to bolt two rooms onto Tapline15. Widening is still a sound hypothesis,
but Well placement, Centre geometry, home modules, healing sites, shadow
corridors, wall grammar, cover cadence and the strategic route graph should all
be reopened. The first candidate must be judged as encounter geometry, not
merely as a distance table: route independence, body capacity, firing lanes,
sight boundaries, temporary seals, signature affordances, counter-routes, and
useful continuations all become acceptance evidence.

The reusable audit is now part of the repository map-analysis skill at
`.claude/skills/analyze-arc-relay-map/references/tactical-geometry.md`.

## Evidence boundary

This report can establish where plays are geometrically possible and whether a
counter can reach them. It cannot establish population balance, optimal
workshop timing, or long-term fun. Those require deterministic match probes and
human play after a candidate passes the geometry audit.

The audit deliberately has no aggregate score. A candidate can have attractive
distances and still fail because one Mason seals it, one Longshot owns every
entrance, allied bodies invalidate the advertised firing formation, or the
winning setup has no useful continuation.

## Live baseline

Tapline15 is a 31×27, 180-degree rotationally symmetric map with 517 open tiles
in one corner-strict connected component. It has no false diagonal passage and
no meaningful cut vertex under the existing two-tile stub allowance. Scoring
occurs on the exact reactor tiles, not on the historical delivery-shoulder
annotation.

West-team landmark coordinates are:

| Landmark | Coordinate |
| --- | ---: |
| West reactor | `(2,5)` |
| West owned Well | `(11,6)` |
| Centre Well | `(15,13)` |
| East owned Well | `(19,20)` |
| East reactor | `(28,21)` |
| North healing tile | `(21,7)` |
| South healing tile | `(9,19)` |

Current base combat scale is vision 5, ordinary gun ranges 3/4/6, and Rail
range 12. Living allies stop allied projectiles without taking damage;
friendly deployments remain transparent. Consequently, route width and exact
posts are real combat mechanics: a one-body front can remove the value of a
rear gun even when a distance ruler says the target is in range.

Current composition requires seven distinct classes in eight slots. One class
may therefore be duplicated; any objective intended to remain contestable must
be reviewed against two Masons, not only one.

## Strategic distance baseline

The following are exact corner-strict terrain steps. They do not include the
Core carrier's slower relocation cadence, traffic, facing, combat, or signature
animation.

| From | To | Shortest steps |
| --- | --- | ---: |
| Own Well | Own reactor | 12 |
| Own Well | Centre Well | 22 |
| Centre Well | Either reactor | 19 |
| Centre Well | Either owned Well | 22 |
| Centre Well | Either healing tile | 8 |
| Enemy Well | Own reactor | 31 |
| Enemy Well | Centre Well | 22 |
| Reactor | Opposing reactor | 38 |
| Own reactor | Near healing tile | 16 |
| Own reactor | Far healing tile | 24 |

Two purposeful return comparisons illustrate the opportunity cost:

| Core origin and commitment | Steps to west reactor |
| --- | ---: |
| Centre, unconstrained fast return | 19 |
| Centre, through south healing tile | 24 |
| Centre, through north healing tile | 32 |
| Enemy Well, unconstrained return | 31 |
| Enemy Well, through south healing tile | 33 |
| Enemy Well, through Centre | 41 |

These route names are literal. “Healing-side” without an exact gate is not a
measurement and should not be used as a pinned contract.

## Three-scale read of the current map

### Map scale

The three scoring sources lie on one chiral strategic line. Sealing the direct
Centre-to-outer-Well openings was useful: an outer raid now asks for a real
route commitment. It did not create a second kind of productive map control.
The force holding Centre still sits on the 2-value source, eight steps from
either heal, and in the best general rotation position.

The top and bottom shadow corridors are genuine flanks, but they are expensive
committed flanks. Their outer row is effectively a one-tile travel lane with
three access seams. That creates good route reading, Dash/Toss spans, mine
predictions, smoke breaks, and Rail headings. It also creates queues, weak rear
fire, and high opportunity cost. With no objective or persistent advantage in
that theatre, the rational default is to keep the squad nearer Wells.

This means the deathball problem is not primarily “damage numbers are wrong.”
Numbers may affect it, but geometry currently gives side allocations too little
productive work.

### Site scale

| Site | Useful geometry now | Structural limitation | Status |
| --- | --- | --- | --- |
| Centre Well and staggered cover | Broad formation floor; several approach angles; cover permits short flanks; supports fighting, pincer release, smoke, Null, Star and Burst | It is both the most important neutral score source and the best general assembly point; eight bodies lose little by staying here | **Needs an external strategic counterweight** |
| Outer-Well rooms | Defensible mouths, meaningful raid commitment, good Hardlight/mine/Sentinel guard play and alternate Core returns | Still score-only geometry; a squad away from the active Well has no accumulating reason to hold adjacent pockets | **Pass locally; narrow strategically** |
| Healing rooms | Natural recovery contest and temporary sustain pocket; useful ambush continuation | Combining veterancy training with healing would compound sustain and advancement, making one already valuable tile solve too many needs | **Keep distinct from workshops** |
| Top/bottom shadow corridors | Strong patrol identity, long aimed lanes, predictive traps, flanking and safe-ish Core logistics | One-tile stretches magnify blocking and suppress squad fire; no independent payoff repays the travel | **Good route, weak theatre** |
| Home pads/reactors | Clear spawn safety and exact scoring; defenders can fall back from shoulder cover | A neutral objective visible or attackable from protection would become local-team property | **Exclude workshops from projection** |

### Tile and formation scale

Use these readings when drawing the new sites:

- one tile is a queue and a single-body or single-Hardlight seal;
- two tiles permit passing but make backline fire fragile;
- three tiles support a screen/front/carrier shape only with carefully offset
  gun posts;
- four or more tiles permit real crossfire, overtaking and rotation.

Narrow terrain is valuable when it creates a deliberate control opportunity
with a bypass. It is a failure when every contest for a neutral objective is
resolved by occupying or walling the same tile.

## Signature-aware geometry findings

The map should not give all sixteen signatures equal value at every site. It
should provide multiple credible homes and counters for each family across the
whole board.

| Family | Geometry that creates play | Dominance/failure check for a workshop candidate |
| --- | --- | --- |
| Dash, Arc Toss, Exchange | Straight 2–5-tile progress spans, legal landings/catches, flank or rescue posts, and onward routes | No skill should bypass the entire contest or land in an unavoidable trap; Toss needs positive route progress rather than decorative range |
| Hardlight, Prism Screen, Trip Node, Sentinel | Predictable mouths, frontal convoy lanes, turns worth mining, destructible guard posts | One Mason cannot seal the site; two Masons still leave a breach or bounded detour; a three-segment screen needs a side answer; one protected Sentinel must not cover everything |
| Rail, Falling Star, Tractor Hook, Target Paint | Long but interruptible headings, clustered-value zones with spread tiles, pull destinations, and several allied firing lanes | No protected Rail/Hook post sees trainee plus every mouth; the Star cross and Null radius cannot cover the entire usable room; allied bodies must not invalidate the advertised focus fire |
| Smoke, Survey Flare, Null Field, Kinetic Burst | Valuable sightline breaks, alternate observation angles, bounded setup clusters, walls that make pushes directional | A single field cannot solve the whole site; leaving or flanking its footprint must be credible; Flare is evaluated by its live anti-smoke semantics, not its name |
| Repair Beam | A protected-but-raidable pair of posts with a plausible two-tick channel | Neither a completely safe sustain bunker nor a room with no channel opportunity is useful |

The most sensitive current values for geometry are: Dash 4, Toss 5, Exchange
6, Hardlight one adjacent tile for 30 ticks, Prism three frontal segments,
mine reveal 2, Sentinel range 4, Rail 12, Star 8 with a five-tile cross, Hook 6,
Paint 7, Smoke radius 2, Null radius 3, and base vision 5. The candidate audit
must re-read source before plotting them; this list is not a substitute for the
rules contract.

## Design posture for the first candidate

This is a **whole-map redesign**, not “Tapline15 plus workshops.” Existing
sheets may require migration; that is preferable to preserving geometry that
does not support the intended game. The current map remains the measured
baseline and regression comparator, not the candidate template.

### Presumptive game invariants

These remain unless the owner separately changes the mode:

- two teams with eight stable squad slots;
- exact-reactor scoring and the existing 1/2/4 Core values;
- three Wells, synchronized respawns, current carrying cadence and current
  combat/signature rules;
- neutral workshop opportunity distinct from healing;
- deterministic corner-strict movement and sight;
- fair 180-degree rotation, unless a later explicit asymmetry proposal proves
  equal opportunity by another method.

Coordinates and terrain are not invariants. In particular, the redesign may
move every Well, reactor, heal, spawn exit and corridor; change Centre size and
cover; replace the shadow lanes; and change every current route length.

### Whole-map questions that must be answered

| Component | Question for the redesign | Current default |
| --- | --- | --- |
| Strategic graph | What can the opponent profitably take when eight enemies occupy one theatre? | Design trade opportunities before walls |
| Centre Well | Should the 2-value Well also be the best assembly, heal pivot and rotation junction? | Reduce its all-purpose centrality |
| Outer Wells | Can each support raid, guard and distinct return play without becoming isolated chores? | Preserve tiering, reopen placement and routes |
| Workshops | Do they create development contests and useful onward choices rather than mandatory camping? | Neutral upper/lower theatres, placement open |
| Healing | Does recovery create a risky detour instead of compounding workshop or Well control? | Keep mechanically and spatially distinct |
| Reactors and homes | Can defenders respond without projecting protected power onto neutral sites? | Preserve safety function, reopen shapes/coordinates |
| Shadow/outer routes | Do committed flanks contain useful encounters, patrol loops and exits rather than long one-tile queues? | Rebuild if needed; do not preserve the current seam automatically |
| Centre walls and cover | Do obstacles split firing lanes and create angles without making one deployment tile decisive? | Redraw from signature and friendly-fire requirements |
| Site count | Can eight bodies contest or trade among the available theatres without the match becoming empty travel? | Validate allocation at whole-squad scale |
| Continuations | After a fight, score, heal or training completion, are at least two useful next decisions available? | Required at every major site |

The map should be designed graph-first. Place reactors, Wells, heals,
workshops, major junctions and committed flanks as nodes; define desired route
families, merges, response costs and profitable trades; only then draw rooms and
walls around those relationships.

## Requirements for the first widened whole-map candidate

### Platform prerequisite

The live engine currently rejects either dimension above 32 tiles. Tapline is
already 31×27, so the existing contract permits only one additional column and
cannot express the meaningful widening under discussion.

The recommended search space is therefore **37×27** rather than the earlier
39×27 sketch. It adds six midfield columns, improves the editor aspect ratio,
keeps an odd rotational centre, and contains 999 tiles—still below the current
32×32 maximum total of 1,024. It nevertheless requires an explicit platform
contract change, for example a bounded width of at least 37 while retaining a
1,024-cell total limit. Engine validation, observation/wire budgets, replay,
renderer scale, editor geometry and their tests must all be reviewed. This
prerequisite is not a terrain or workshop-balance change and must not be hidden
inside the map delta.

Within that search space, the 27-row height and odd rotational centre are useful
starting hypotheses. No current home-module shape, landmark coordinate, wall or
route distance is protected. Use the additional width and a fresh strategic
graph to make scoring, development, recovery, guarding, raids, ambushes and
rotations compete for bodies. The dimension remains a candidate hypothesis,
not a promoted contract.

### Hard gates

1. Satisfy the explicit dimension/total-cell contract and all observation,
   replay, renderer and editor checks it entails.
2. Preserve exact 180-degree terrain, landmark, spawn and workshop symmetry.
3. Preserve one corner-strict connected floor, no false diagonal passage, no
   meaningful new dead end, and no body-sized cut vertex.
4. Give each workshop at least two genuinely independent route families. Two
   visible mouths that merge at one upstream choke count as one family.
5. A single Hardlight cannot seal a workshop. With the allowed duplicate
   wildcard, two simultaneous Hardlights must still leave a breach or a
   measured, bounded detour.
6. Keep workshops neutral: outside spawn protection, separate from Wells and
   healing tiles, reachable for counterplay, and not automatically owned by the
   locally nearer respawn.
7. Provide room for a trainee, protectors, a crossfire, spread against area
   tools, and a breach. At least one useful cross-section should be four tiles
   wide; every mouth and internal pinch receives its own measured width.
8. Provide a visible response boundary. The attacker must be observable before
   interruption, while defenders must not receive perfect advance vision of
   every approach from one facing.
9. Give attackers at least one line that can interrupt training, but reject any
   protected Longshot/Hook/Sentinel post that covers the trainee and every
   entrance.
10. Give every resolved workshop contest at least two useful continuations—for
   example Centre, an outer Well, heal, shadow flank, or safe disengagement.
11. Make the intended play authorable with current map-first Tactics: legal
    distinct posts, facings, routes/patrols, signature points and a veterancy
    transition, without raw JSON or an excessive chain of hidden anchors.

### Candidate measurements

First publish the abstract landmark/route graph and its intended tradeoffs.
Then pin a complete strategic distance matrix among both reactors, all three
Wells, both healing tiles, both respawn clusters, both workshops and every major
junction. For every major site—not only workshops—record entrance widths, route
merges, one- and two-tile removal cuts, first-observation tiles, exposure depth,
ordinary 3/4/6 coverage, Rail/Hook/Star/control footprints, and one-/two-Mason
seal detours.

Distances alone must not decide neutrality. A side can arrive equally quickly
while one side owns the only useful firing post or approaches with its back to
the defending wedge.

## Required named-play proofs

Before simulation, the candidate geometry must let the current editor express
at least these plays with real squad slots:

1. **Centre split and pincer:** groups approach on independent paths, fight
   without collapsing into one firing queue, then choose different useful
   continuations.
2. **Outer-Well raid and return:** a raider commits through a readable route,
   defenders can intercept or trade elsewhere, and the carrier has meaningfully
   different fast/risky and safe/long returns.
3. **Train and screen:** a trainee and small guard establish the workshop,
   interrupt an incoming line, then rotate toward Centre or an outer Well.
4. **Two-route workshop disruption:** attackers approach through separate families and
   converge only after the defender must choose a facing.
5. **Workshop feint into outer raid:** visible workshop pressure draws bodies;
   the raiding group uses a different route and the workshop group has a useful
   fallback rather than idling.
6. **Exit ambush:** exact posts watch an objective continuation,
   spring after a visible trigger boundary, then move to a scoring or recovery
   objective.
7. **Patrol and re-fortify:** a patrol uses mine/sentry/wall marks at selected
   locations while retaining a breach and a counter-route.
8. **Core convoy through a side theatre:** screen/support posts and firing
   lanes allow a carrier to traverse without requiring Arc Toss or one exact
   class.

Each proof is written as **approach → setup → trigger → payoff → continuation**
plus the opposing **counter**. If the continuation is “return to Centre by the
same route,” the new theatre probably has not created enough strategic choice.

## Framework/tooling follow-up

The existing analyzer already provides pinned routes, landmark completeness,
corner-strict connectivity, false-corner and cut-vertex checks, base vision,
and ordinary range overlays. The tactical framework adds the required manual
site cards and signature matrix.

For the first candidate, the analyzer should also report or accept explicit
tactical-site data for:

- route-family mouths and merge points;
- one- and two-tile seal/removal tests;
- posts, facing, first-observation boundaries and line coverage;
- signature footprints and protected-post warnings;
- named setup/counter/continuation overlays.

Automating these checks is desirable, but a candidate is not permitted to skip
them while automation is incomplete. The first candidate report must contain
the preserved invariants, abstract strategic graph, exact terrain delta, a
complete route/range atlas, topology findings, site cards for the whole map,
signature-affordance matrix, authorability findings, hard failures, accepted
tradeoffs, and explicitly labelled playtest hypotheses.

## Recommended next step

Approve the bounded map-width prerequisite, then design the 37×27 abstract
strategic graph before placing any walls. Create one whole-map geometry
candidate from that graph with no promoted workshop-rules change, then run the
complete audit before tuning workshop channel time, downtime, XP rate, or other
balance numbers. If the geometry cannot produce the named plays, trades and
counters, discard or redraw it before simulation.
