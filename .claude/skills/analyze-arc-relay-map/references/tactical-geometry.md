# Arc Relay tactical-geometry audit

Use this audit when creating or reviewing walls, rooms, corridors, neutral
objectives, ambush sites, guard posts, patrol loops, or convoy routes. The
distance atlas remains the source for route mathematics. This reference adds
the encounter and signature questions that distance cannot answer.

The audit produces design evidence, not a balance verdict. A geometrically
available play may still be too weak, too strong, too difficult to author, or
rare in real matches.

## Read the live game first

Before applying this framework, inspect the active profile, map builder, mode,
signature definitions, signature runtime, movement cadence, visibility, and
first-party tactical executor. In particular, verify rather than assume:

- map width, height, total-tile, observation, replay, and renderer limits;
- base vision, point-blank vision, and corner-strict occlusion;
- the short, medium, and long ordinary gun ranges and projectile headings;
- living-body collision and allied-projectile contact;
- chassis movement cadence and Core-carrier relocation cadence;
- each signature's range, footprint, tell, duration, cooldown, placement,
  suppression, visibility, replacement, and custody restrictions;
- exact scoring and spawn-protected tiles;
- what the player can express with posts, facing, routes, patrols, zones,
  signature marks, phase conditions, and Core custody.

Old reports are useful provenance but not authoritative mechanics.

## Three scales, one encounter sentence

Audit at all three scales:

1. **Tile scale (1–6 tiles):** facing, corner movement, body lanes, cover,
   placement legality, ordinary guns, and compact signature footprints.
2. **Site scale (roughly one engagement):** entrances, posts, concealment,
   crossfire, flanks, retreat, deployment locations, and formation capacity.
3. **Map scale:** objective opportunity cost, rotations, reinforcements,
   alternate Core routes, workshop/well coupling, and whether one force can
   project onto too many objectives.

Describe every proposed play using the same five-part sentence:

> **Approach → setup → trigger → payoff → continuation**, with an explicit
> **counter** that the opposing player can author and reach in time.

Reject a setup description that stops at its payoff. An ambush that kills four
bodies still needs a useful next route; a workshop defence still needs an exit
when training completes; a breached guard site still needs somewhere to fall
back.

## Whole-map redesign discipline

When a new objective or rule exposes a macro-geometry problem, do not assume the
existing map plus a new room is the correct candidate. Separate:

- **game invariants:** mode identity and rules the owner intends to preserve;
- **platform constraints:** dimensions, total-cell budget, replay, observation,
  renderer, editor, and pathfinding limits;
- **geometry hypotheses:** every landmark coordinate, wall, corridor, room,
  route length, cover pattern, protected region, and local approach advantage.

Only the first two categories are presumptively fixed. Every geometry
hypothesis must earn its place in the candidate.

Before drawing individual walls, draw an abstract route graph whose nodes are
reactors, spawn exits, Wells, healing sites, neutral objectives, major junctions
and committed flank seams. Annotate:

- shortest and purposeful committed distance bands;
- route-family independence and merge points;
- reinforcement and retreat directions;
- Core-carry and unloaded travel implications;
- which nodes one squad can project onto without a meaningful rotation cost;
- what the opponent can profitably trade for instead of fighting a deathball.

Reject a graph where the most valuable objective is also the best assembly
point, best healing pivot, best reinforcement junction, and best route to every
other objective. Reject the opposite extreme as well: eight bodies cannot make
interesting choices among so many isolated sites that most encounters become
empty travel.

For a whole-map candidate, explicitly question and report:

- canvas dimensions and aspect ratio;
- symmetry model and any intended local approach bias;
- reactor and spawn-protection geometry;
- all Well positions, values, return routes, and inter-Well rotations;
- centre size, openness, cover cadence, and strategic centrality;
- healing position and whether recovery compounds another reward;
- neutral-objective placement and contest routes;
- outer/shadow lanes, dead space, patrol loops, and flank commitment;
- room and corridor widths under allied projectile blocking;
- sightline length, observation boundaries, facing burden, and dominant posts;
- deployment legality, one-/two-Mason seals, and persistent ground value;
- useful continuations after every major site resolves.

Compare the candidate to the promoted baseline, but do not preserve a baseline
distance or wall merely to reduce migration. Existing sheets are compatibility
cost, not evidence that the old geometry is strategically correct.

## Map vocabulary

Use these terms consistently:

- **Site:** a room, junction, objective bay, or route segment intended to host
  an engagement.
- **Mouth:** a contiguous set of tiles through which one route family enters a
  site. Two adjacent tiles are one mouth, not two independent entrances.
- **Route family:** a purposeful approach whose meaningful path does not merge
  with another family before the response decision.
- **Post:** a legal resting tile plus intended facing and responsibility.
- **Kill space:** the tiles a setup intends to expose to overlapping attacks or
  signatures.
- **Break route:** the counter-route used to flank, disengage, reinforce, or
  continue after the setup changes state.
- **Seal tile:** one temporary body-blocking deployment tile whose occupation
  removes a route family.
- **Exposure depth:** movement steps between first plausible team observation
  and the intended payoff area.

## Hard topology gates

These are blockers unless the owner explicitly accepts the exception:

- the candidate fits the current map/package limits, or any required platform
  limit change is separately explicit and retains a bounded total-cell budget;
- terrain and gameplay landmarks preserve the required symmetry;
- all required floor and landmark tiles are reachable under corner-strict
  movement, with no false diagonal passages;
- no new meaningful dead end or body-sized cut vertex is introduced;
- every neutral objective has at least two real route families, not two mouths
  fed by the same upstream one-tile choke;
- a single Hardlight Block cannot completely seal a neutral objective;
- if duplicate Masons are allowed and the objective must remain contestable,
  two simultaneous seal tiles must still leave a breach or bounded detour;
- no spawn-protected position attacks or observes a neutral objective without
  leaving protection;
- no mandatory Core route is silently turned into a deployment-forbidden or
  body-capacity trap;
- no side objective shares the healing tile merely to save map space;
- all exact posts, facing anchors, routes, patrol points, and signature marks
  required by the intended play are legal and distinct.

For seal analysis, test removal of every plausible mouth tile and relevant
upstream choke tile. Adjacent mouths do not establish counterplay if one tile
farther back disconnects both.

## Site card

Create one card for every objective bay, major junction, ambush pocket, guard
line, and distinctive route segment affected by the candidate.

```text
Site / purpose:
Tiles and landmark:
Route families and mouths:
Nearest strategic origins, in terrain steps:
Local floor area and useful formation width:
Posts and intended facings:
First-observation boundary / exposure depth:
Ordinary short / medium / long coverage:
Signature affordances:
Seal and detour sensitivity (one Mason; two Masons):
Setup 1 → counter → continuation:
Setup 2 → counter → continuation:
Setup 3 → counter → continuation:
Authoring burden:
Hard failures, tradeoffs, and playtest hypotheses:
```

Do not award an aggregate score. Mark the card **pass**, **needs revision**, or
**experiment**, and preserve the reasons.

## Geometry measurements that matter

### Access and response

- Inventory mouths by route family and measure their minimum widths.
- Identify where nominally separate paths merge.
- Find single-tile and two-tile cuts between the site and each strategic
  origin. For a neutral objective, path independence matters more than the
  number of lines drawn on the atlas.
- Measure attacker, defender, centre, Well, reactor, heal, and respawn routes
  separately. Equal whole-map symmetry does not make one local contest neutral.
- Measure break routes after the objective resolves, not only approaches.

### Formation capacity and friendly firing lanes

Living allies block allied shots in the current product. Corridor and post
width therefore determine whether a squad is a firing formation or a queue.

- one tile is a hard queue and a single body/Hardlight seal;
- two tiles permit passing but make backline fire fragile;
- three tiles can support carrier/screen/frontline arrangements with limited
  firing lanes;
- four or more tiles allow real crossfire, rotation, and overtaking, subject to
  local walls and facing.

These are readings, not universal minimums. Narrow routes are useful when they
have a bypass and are intentionally vulnerable to control.

For each setup, draw the actual body posts and target lines. Check that one
front body does not invalidate every rear gun, Repair Beam, Hook, Rail, or
Sentinel line.

### Sight, concealment, and facing

- Plot full base vision and wall-occluded vision from authored posts.
- Record which route tile first reveals each post and the objective.
- A useful ambush gives an attacker a response boundary; zero-warning corner
  contact can be as shallow as full advance visibility.
- Check whether one facing observes every mouth. Sites that require rotation or
  multiple spotters create information play; sites solved by one universal
  facing do not.
- Find firing posts that cover several mouths with ordinary long range or Rail.
  A dominant post is acceptable only if it is flankable, smokeable,
  suppressible, destructible, or expensive to occupy.
- Treat team-shared contact separately from a body's own legal firing wedge.

### Cover cadence and room scale

- Mark uninterrupted wall/smoke-sensitive lines at ordinary ranges and at
  signature super-ranges.
- Record whether cover permits meaningful advances rather than alternating
  invulnerable corners.
- Very compact sites amplify Null Field, Smoke, Falling Star, Kinetic Burst,
  mines, and body blocking. Very open sites amplify Longshot, Towline, Sunder,
  Sentinel, and focus fire.
- Include escape and spread tiles so area/control tools are powerful choices,
  not automatic answers.

## Signature-affordance audit

Read the current definitions from code, then fill a matrix with sites as rows
and signatures as columns. Use:

- **P — primary affordance:** geometry naturally supports a deliberate use;
- **S — secondary:** useful but situational;
- **C — counter:** the signature answers a setup enabled there;
- **D — dominance risk:** the signature can solve or lock the site too easily;
- **– — no meaningful relation.**

Every major site should support several signature families and several
counters. Do not force all sixteen signatures into every room. Across the full
map, each family should have more than one credible use and no site should
require one exact class to remain playable.

### Mobility and logistics

- **Kestrel / Vector Dash:** find straight 2–4-tile route spans with legal
  landing and a meaningful destination. A Dash line should accelerate or flank,
  not bypass the whole encounter or end in a trap. Recheck Core restrictions.
- **Relay / Arc Toss:** find straight catches within current Toss range that
  make positive route progress. Walls should create placement choices without
  making every legal toss clip unpredictably.
- **Switchback / Exchange:** identify visible source/destination pairs where a
  rescue or positional swap has both value and counter-risk. Avoid universal
  safe escape posts.

### Fortification and persistent ground

- **Mason / Hardlight Block:** test every mouth and friendly route for seal,
  detour, block exposure, replacement, and two-Mason interaction. A strong
  funnel is desirable; an unanswerable neutral-objective lock is not.
- **Palisade / Prism Screen:** test frontal convoy widths and flank angles. A
  corridor exactly filled by the moving screen needs a reachable side or close
  response.
- **Minesmith / Trip Node:** mark turns and mouths where a mine predicts path
  choice. An unavoidable one-tile mine route needs a bypass or enough approach
  information to answer it.
- **Nest / Sentinel:** draw exact firing rays and range from candidate posts.
  It should guard selected space without covering every mouth from one protected
  tile. Include allied-body obstruction and hostile destruction routes.

### Ranged setup and focus fire

- **Longshot / Rail Line:** draw exact headings out to current signature range,
  cover breaks, friendly blockers, team-spotting positions, and rooted response
  routes. Flag any post that owns the objective and all approaches.
- **Mortar / Falling Star:** overlay the cross on likely clustered posts and
  mouths. Provide response/spread tiles during the tell.
- **Towline / Tractor Hook:** draw clear bolt lines, interceptors, pull
  destinations, and the path back into or out of the fight. Flag a protected
  post that can repeatedly pull a trainee/carrier with no flank.
- **Sunder / Target Paint:** check whether allies can plausibly spend the focus
  window from distinct firing lanes; isolated paint geometry is not an
  affordance.

### Information, denial, and displacement

- **Veil / Smoke:** find valuable sightline breaks and verify that smoke does
  not automatically sabotage every friendly lane. Show alternate observation
  angles.
- **Lantern / Survey Flare:** audit against its current authoritative sensing or
  anti-smoke semantics, not its name. Show which real allied sightlines benefit.
- **Hush / Null Field:** overlay the frozen radius on setup clusters,
  deployments, and escape tiles. A whole site inside one field is a dominance
  risk unless leaving it is a credible response.
- **Repulsor / Kinetic Burst:** preview every adjacent push destination. Walls,
  posts, Core direction, and route mouths decide whether displacement helps or
  harms.

### Sustain

- **Patchbay / Repair Beam:** find protected but raid-accessible channel pairs
  within current range and line of sight. A completely safe sustain pocket is a
  snowball risk; a pocket with no two-tick channel opportunity is dead geometry.

## Named play audit

At minimum, try to author these play families against the actual site geometry:

- exact-post ambush with a visible trigger boundary;
- hold/guard with bounded chase and return;
- two-route pincer whose paths stay separate until engagement;
- patrol loop with useful facing and deployment/signature points;
- convoy or Core return using screen, support, and firing lanes;
- raid with a different useful escape/continuation route;
- workshop train/protect/disrupt cycle when workshops are present.

For each, place the actual eight squad slots or explicitly account for the
slots assigned elsewhere. Verify that the map-first Tactics editor can express
it without raw JSON, excessive anchor chains, hidden timing assumptions, or a
condition the team cannot observe.

## Neutral workshop requirements

Apply these additional gates to a proposed workshop site:

- workshops are neutral opportunities, never extensions of spawn protection,
  an owned Well, or a healing tile;
- the pair is globally symmetric and local approach bias is measured;
- the training tile has multiple route families and cannot be sealed by the
  currently allowed Mason composition;
- enemy contest can reach a meaningful interrupt position without occupying
  the exact training tile;
- at least one attack lane can interrupt training, but no single protected post
  covers every approach and the trainee simultaneously;
- there is enough floor for a trainee plus protectors, crossfire, spread, and a
  breach; stacking bodies must not accelerate training;
- workshop-to-centre, workshop-to-Wells, workshop-to-heals,
  workshop-to-reactors, workshop-to-respawn, and workshop-to-workshop routes are
  all measured;
- claiming or exhausting the workshop has at least two useful continuations;
- the player can author training with posts/routes and a veterancy transition,
  and can observe readiness, contest, progress, completion, and cooldown.

## Candidate report contract

A proper candidate review contains:

1. preserved game invariants, platform constraints, and reopened geometry
   hypotheses;
2. the abstract strategic route graph and projection/trade analysis;
3. exact source geometry and delta from the promoted map;
4. landmark-complete route/range atlas with pinned distances;
5. topology findings, including cut and seal sensitivity;
6. site cards for every major encounter site, not only new objectives;
7. the signature-affordance matrix;
8. named setups, counters, and continuations drawn with real posts/routes;
9. authorability findings for the current Tactics UI/runtime;
10. hard failures, accepted tradeoffs, and unresolved playtest hypotheses;
11. explicit separation between geometry evidence and balance claims.

Only after this audit passes should a candidate be promoted to deterministic
simulation or population balance testing.
