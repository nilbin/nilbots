DECISION NEEDED: Approve **Arc Relay** as the game concept for Phase B and approve the recommended 16-class launch band, or name substitutions from the registered alternates below. Default recommendation: Arc Relay with Kestrel, Palisade, Towline, Patchbay, Lantern, Mortar, Minesmith, Hush, Relay, Switchback, Longshot, Mason, Sunder, Repulsor, Veil, and Nest.

RESULT: Phase A recommends replacing Frontline's slowly moving territorial bar with a larger-map logistics-combat sport. Three separated Wells produce physical Arc Cores; minds route, carry, hand off, escort, intercept, steal, and bank them. Three deliveries charge a visible reactor Pulse, and three Pulses win the match. The recurring carrier stories create legible momentum and reversals without live input. A pool of 28 one-signature class kits was designed for this game and culled to a mechanically distinct 16-class launch band. No implementation, map, numeric balance, gallery, or claim that the game is fun has been made; those remain behind the owner's gates.

EVIDENCE:

# Gate 1 — concept and roster

## 1. Scope and confidence

This is a design recommendation, not play evidence. It executes only Phase A of
`HANDOVER-CODEX-GAME-REDESIGN-2026-07-31.md`:

- game concept first;
- candidate class kits second;
- roster cull third;
- owner gate now.

It deliberately does **not** specify final map tiles, cooldowns, damage values,
spawn timings, wire contracts, renderer code, SDK shapes, or balance arms. Those
belong to later phases after the owner accepts the direction.

The design treats these as fixed constraints:

| Constraint | Arc Relay answer |
| --- | --- |
| Deterministic, blind play | All spawns, decisions, travel, passes, collisions, and scoring resolve from the immutable match contract plus submitted mind decisions. There is no live input. |
| Mind-native | One participant mind assigns carriers, escorts, interceptors, scouts, and reserves across every body. The game rewards coherent army plans rather than per-life agreement work. |
| Commander sheets remain | A sheet chooses composition, role priorities, routes, hold/avoid zones, rally lines, and ordered state-based gambits. |
| Bigger map | Three separated theaters and several return routes require materially more travel space than current Frontline. Exact generation waits for Phase B. |
| More interesting core | The contested object moves. A lead is exposed on a carrier until delivery, and possession can reverse through combat or interception. |
| Fun to watch | Every scoring attempt has a bright object, named carrier, route, escort, pursuers, handoff arc, drop, and reactor payoff. Fun itself remains owner-judged from later galleries. |
| 2D is required | Every core fact has a cheap Canvas2D read: icons, trails, arcs, countdowns, pips, fields, and short telegraphs. No kit requires 3D animation to understand. |
| 15–20 classes | The recommended launch band is 16 classes selected from 28 candidates. |
| One signature skill | Every class below has exactly one exclusive signature. Move, turn, basic fire, pick up, hand off, and drop are shared game verbs. |
| Breadth, never power | Classes widen sheet options. Unlocking another class never raises the numeric ceiling of an existing one. |

## 2. Curated concept fork

Three concepts were compared before class design. Arc Relay is recommended; the
other two stay registered as coherent fallbacks rather than being silently lost.

| Concept | Core picture | Strength | Failure risk | Verdict |
| --- | --- | --- | --- | --- |
| **Arc Relay** | Physical neutral cores move from three Wells through carriers and handoffs to home reactors. Deliveries fire reactor Pulses. | Recurring, visible possession stories; route drawings matter; interception creates reversals before score becomes permanent. | Could collapse into one best convoy or home camping if Wells/routes are poorly authored. | **Recommend.** Best fit for commander sheets and watchability with the least phase overhead. |
| **Crown Heist** | Collect several charges to open the enemy vault, steal its Crown, then extract it home. | Strong single climax and an unmistakable final object. | Two rule phases, defensive turtling around a vault, and long resets when the Crown repeatedly returns. | Registered alternative if the owner wants one larger climax instead of recurring scores. |
| **Circuit Siege** | Hold linked relay nodes to launch visible energy packets toward the enemy reactor; packets can be intercepted. | Team-wide momentum is extremely legible; no carrier AI required. | Stationary node control risks recreating Frontline's dullness with more lights, and drawn routes have less authorship value. | Hold. Use only if carrying proves too difficult to make robust. |

## 3. Recommended game concept: Arc Relay

### 3.1 The promise

Arc Relay is a deterministic team sport about **getting a dangerous object
home through a battlefield**. A spectator should be able to answer four
questions without opening a panel:

1. Where are the live Arc Cores?
2. Who is carrying each one?
3. Which route and escort are protecting it?
4. How close is either reactor to its next Pulse and to victory?

The game is not won by standing on a progress tile. Space matters because
objects, bodies, vision, threats, and routes move through it.

### 3.2 Match loop

The Phase A rules hypothesis is:

1. A large, mirror-fair map has three separated theaters: north, centre, and
   south. Each contains a neutral **Well** with at least two viable approaches
   and at least two return routes.
2. Wells produce physical **Arc Cores** on a public, staggered schedule. More
   than one Core can be live, so a team cannot solve the match by sending the
   whole army to one marker.
3. A body touching a loose Core picks it up. A carrier is unmistakable and
   gives up its basic gun while carrying. It may move, drop, make the shared
   adjacent handoff, or use its one class signature subject to the common
   objective-safety rule below.
4. A handoff is an authored action, not an automatic collision. It transfers
   one Core to an adjacent ally and cannot be chained through several bodies
   in the same tick. This makes relay formations possible without turning a
   body line into instant transport.
5. Destruction makes the Core drop on the death tile. The Core is immediately
   neutral: either team may recover it. A successful interception therefore
   changes possession visibly rather than merely subtracting health.
6. Entering the team's reactor socket banks the Core immediately. There is no
   stationary capture channel at home.
7. Three banked Cores charge one visible **Pulse**. A Pulse removes one of the
   enemy reactor's three integrity segments and resets only the charging pips;
   loose and carried Cores remain in play.
8. The third Pulse destroys the enemy reactor. At the match horizon, reactor
   integrity ranks first, stored charge second, and unresolved equality is a
   draw. Exact counts and horizon are Phase B hypotheses, not frozen numbers.

The rhythm is therefore:

`forecast → allocate → contest → possess → route → intercept/handoff → deliver → Pulse`

That loop repeats several times per match. Each repetition can tell a different
story because Well timing, composition, surviving bodies, known routes, and the
score state differ.

### 3.3 Shared verbs and the one-skill law

Every body receives the same small base grammar:

- move and turn;
- basic visible projectile fire;
- pick up a loose Arc Core by entering its tile;
- hand off a carried Core to one adjacent ally;
- drop a carried Core;
- wait;
- use its class's **one** signature skill.

Statlines vary only within declared bands:

- **Hull:** light (3), standard (4), heavy (5);
- **Mobility:** swift, standard, deliberate;
- **Basic gun:** short-fast, medium-steady, or long-slow.

Those numbers are a comparison language for the roster, not final tuning.
Damage remains simple and readable; class identity comes from the signature,
not a pile of passives.

One common objective-safety rule prevents movement skills from becoming hidden
scoring exceptions: unless a signature explicitly manipulates a Core, a skill
that voluntarily displaces a carrier drops its Core on the departure tile
before movement resolves. Relay's Arc Toss is the one launch-roster skill
explicitly authored to move a Core without moving its carrier.

### 3.4 Lifecycle and comeback shape

Bodies are stable slots controlled by one mind. Destruction removes pressure,
drops any carried Core, and starts a bounded home return. It does not eliminate
the participant from ordinary play. That preserves counter-attack capacity and
makes a wipe a large tempo win rather than an abrupt ending.

Arc Relay gives no permanent combat power for scoring and has no required scrap
economy or in-match stat ladder. A leading team has score and tempo, not stronger
guns. A trailing team can reverse a delivery by attacking a carrier, and can
allocate more bodies to the next announced Well without fighting through a
numeric snowball.

### 3.5 The three simultaneous decisions

At useful density, a mind cannot maximize all three:

1. **Acquire:** arrive at the next Well first or ambush the opponent leaving it.
2. **Protect:** form an escort and preserve a carrier's route home.
3. **Disrupt:** hunt an opposing carrier, cut a handoff lane, or force its
   escort away from the next Well.

A fourth layer, **information**, decides how confidently the mind can split.
That creates class and sheet value for scouts, smoke, sentries, mines, and
route control without making any of them another score channel.

### 3.6 Why a bigger map is part of the concept

The target is roughly two to three times current Frontline's useful floor area,
with three genuine theaters rather than one contact blob. The final dimensions
are a Phase B map decision, but the functional requirements are already clear:

- a north, centre, and south Well cannot all fit inside one ordinary vision
  union;
- each Well has multiple entry and exit headings;
- return routes cross enough to permit interception but not so much that every
  carrier follows one central lane;
- lateral connectors let a reserve switch theaters at a real travel cost;
- home approaches give defenders choices without permitting one spawn camp to
  cover every delivery route;
- drawn paths, zones, rally lines, and fallback routes remain legible at the
  sheet level.

The large map is not scenery. It is what makes allocation, scouting, route
choice, and delayed reinforcement distinct decisions.

### 3.7 Commander-mode expression

A commander sheet for Arc Relay can express strategy without live input:

- **Stable and composition:** fill the company's slots from a stable of about
  five unlocked classes; duplicates and exact slot count wait for Phase B.
- **Stock mind:** choose the curated doctrine that interprets the sheet; the
  saved sheet changes its priorities and spatial plan without becoming a live
  controller.
- **Opening allocation:** which bodies pressure each Well, which body stays as
  reserve, and which route each group takes.
- **Carrier policy:** safest, fastest, healthiest, nearest, or explicitly named
  role; when to hand off; when to abandon a Core.
- **Escort grammar:** screen ahead, flank cover, rear guard, or loose spread;
  minimum escort size before committing home.
- **Interception policy:** chase threshold based on enemy distance-to-bank,
  carried Core count, and the next Well timer.
- **Drawings:** outbound paths, return paths, ambush zones, avoid zones, handoff
  points, and cross-theater rally lines.
- **Ordered gambits:** for example, “IF behind by one Pulse AND two enemy Cores
  are carried → collapse north/south interceptors onto the nearer return line”;
  or “IF our carrier enters the final third with hull ≤2 → Switchback replaces
  it at rally beta.”

Blind matchmaking remains honest: the sheet sees public match state only after
the match begins, through pre-authored ordered conditions. No opponent-specific
pregame counterpick or live redraw is required.

The morning report has unusually concrete attribution material: which Well
allocations won possession, where each Core changed hands, whether the chosen
return path met its drawn route, which escort broke, which gambit fired, and
what preceded every Pulse. Deterministic counterfactual re-sims can compare a
different route, carrier policy, composition, or gambit ordering without
pretending one stat caused the result.

Commander rewards remain breadth-only: classes, stock minds, sheet/gambit
capacity, map-specific sheet variants, and cosmetics may widen authorship.
Nothing unlocked raises a class's hull, damage, travel, cooldown, Core value,
or reactor score inside an otherwise identical match.

### 3.8 What the spectator sees

Canvas2D presentation is part of the mechanic definition:

- Wells show a neutral icon, a compact countdown ring, and a birth flare.
- Loose Cores use a high-contrast neutral glyph and a slow pulse.
- A carrier gets a tall team-colour beam, a trailing Core ribbon, and a role
  tag; the carrier remains readable under effects and fog.
- Handoffs draw a short, arcing transfer ribbon with clear source and target.
- Dropped Cores produce a radial crack and immediately lose the prior team
  colour.
- Each reactor shows three integrity segments and three charge pips. A delivery
  fills one pip; a Pulse crosses the arena as a broad team-colour wave and
  removes one enemy integrity segment.
- Signatures use one consistent tell, active shape, and cooldown read. Nothing
  depends on skeletal animation or camera angle.

The intended replay beats are visible without score disclosure: a split to
three Wells, a carrier emerging, an escort forming, a route cut, a handoff under
fire, a possession reversal, a desperate final delivery, and the Pulse.

### 3.9 Principal risks to carry into Phase B

These are not solved by assertion. They are the first design questions after
the owner accepts the concept:

| Risk | Failure picture | First isolation to register later |
| --- | --- | --- |
| One convoy is always correct | Both teams form one deathball and ignore two Wells. | Stagger/overlap of Well production and travel-time geometry. |
| Home camping dominates | Interceptors wait near the enemy reactor instead of contesting Wells. | Home approach multiplicity, spawn safety, and carrier route width. |
| Passing erases travel | A chain of bodies moves a Core across the map instantly. | One-transfer-per-Core-per-tick and receiver commitment. |
| Swift chassis monopolize carrying | Every composition needs the same runner. | Carrier mobility burden and signature-vs-carry interaction. |
| Safe score snowballs | First Pulse predicts the match too strongly. | No combat upgrades; read lead reversals and delivery steals before adding comeback rules. |
| Too many simultaneous Cores confuse | Viewer cannot tell which possession matters. | Bound live Core count and prioritize near-bank threat in overlays/highlights. |
| Matches run long | Repeated drops never become deliveries. | Well cadence, return distances, respawn delay, and Pulse thresholds—numbers before new mechanics. |

## 4. Candidate class pool

### 4.1 Cull rules

Candidates were judged against the game rather than against existing engine
inventory. A launch class should:

1. change at least one sheet-level decision;
2. have exactly one signature skill with a single sentence rule;
3. have visible anticipation, execution, and counterplay in Canvas2D;
4. remain useful in more than one game state;
5. avoid direct score deletion or unavoidable Core transport;
6. occupy a distinct counter-web position;
7. not require another class merely to make its mechanic legible;
8. leave final numeric balance to measurement.

Status terms:

- **Launch:** recommended in the 16-class band.
- **Alternate:** coherent and retained, but overlaps a launch class or needs a
  sharper Phase B proof.
- **Hold:** attractive but too expensive or ambiguous for the initial game.
- **Reject:** its central interaction works against watchability or earned
  outcomes.

### 4.2 Summary slate

| # | Class | Signature | Primary sheet question | Status |
| ---: | --- | --- | --- | --- |
| 1 | Kestrel | Vector Dash | Where is rapid response worth abandoning the Core? | **Launch** |
| 2 | Palisade | Prism Wall | Which firing lane must the convoy blank? | **Launch** |
| 3 | Towline | Tractor Hook | Which body should be pulled out of formation? | **Launch** |
| 4 | Patchbay | Repair Beam | When is preserving tempo worth taking a gun offline? | **Launch** |
| 5 | Lantern | Survey Flare | Which theater needs certainty before committing bodies? | **Launch** |
| 6 | Mortar | Falling Star | Where will a static escort or sentry still be two ticks later? | **Launch** |
| 7 | Minesmith | Trip Node | Which route should become expensive rather than impossible? | **Launch** |
| 8 | Hush | Null Field | When does denying signatures beat dealing damage? | **Launch** |
| 9 | Relay | Arc Toss | Where is a risky long handoff better than walking? | **Launch** |
| 10 | Switchback | Exchange | Which ally should take over the dangerous position now? | **Launch** |
| 11 | Longshot | Rail Line | Which corridor can be made unsafe with a public charge? | **Launch** |
| 12 | Mason | Hardlight Block | Which route should be bent for a few ticks? | **Launch** |
| 13 | Sunder | Target Paint | Which enemy justifies team focus? | **Launch** |
| 14 | Repulsor | Kinetic Burst | Is local formation control worth entering adjacency? | **Launch** |
| 15 | Veil | Smoke Canister | Where is uncertainty worth giving up open sightlines? | **Launch** |
| 16 | Nest | Sentinel Seed | Which theater deserves a persistent but killable guard? | **Launch** |
| 17 | Mirror | Reversal Plate | Is one reflected shot clearer than a broader barrier? | Alternate |
| 18 | Breakhorn | Ram | Does a committed shove add more than dash plus burst already do? | Alternate |
| 19 | Ghost | Phase Step | Can wall traversal stay readable and map-safe? | Alternate |
| 20 | Gatekeeper | Twin Gates | Can paired portals avoid invalidating route authorship? | Hold |
| 21 | Decoy | False Signal | Can deception remain fun when the spectator must understand truth? | Reject |
| 22 | Leech | Backfeed | Should a skill erase already banked score? | Reject |
| 23 | Swarm | Fission | Is extra body topology worth the visual and control cost? | Hold |
| 24 | Chrona | Recall | Can a rewind preserve causal clarity? | Hold |
| 25 | Locksmith | Seal Well | Is disabling the source ever more fun than contesting it? | Reject |
| 26 | Conductor | Shared Circuit | Can split damage read clearly enough in a crowded fight? | Alternate |
| 27 | Fuse | Critical Mass | Is self-destruction a strategy rather than a cheap trade exploit? | Hold |
| 28 | Magnet | Core Draw | Does loose-Core manipulation add play without becoming mandatory? | Alternate |

## 5. Recommended launch briefs

### 5.1 Kestrel — rapid interceptor

- **Fantasy:** a light pursuit bot that commits to a visible vector and arrives
  before the rest of the formation.
- **ONE signature — Vector Dash:** after a one-tick arrow telegraph, Kestrel
  surges in a straight line up to the declared distance, stopping at the first
  blocked tile. A carried Core drops on the departure tile.
- **Statline band:** hull 3 / swift / short-fast gun.
- **Sheet choices:** hold as cross-theater reserve, pre-position on an ambush
  line, or spend the dash to finish a damaged carrier. A route drawing can name
  dash corridors and no-dash Core legs.
- **Counterplay:** the vector is public; step aside, occupy the landing lane,
  put a Trip Node on it, raise Hardlight, or force Kestrel to dash without the
  Core.
- **Screen read:** long arrow tell, compressed silhouette, bright straight
  streak, hard stop flare.
- **Why launch:** gives a large map response time and creates pursuit without a
  teleport or hidden movement.

### 5.2 Palisade — convoy shield

- **Fantasy:** a heavy projector that turns one chosen line of fire into cover.
- **ONE signature — Prism Wall:** projects a short directional barrier on an
  adjacent edge for a fixed duration. The wall blocks projectiles but not
  bodies, and Palisade may maintain only one.
- **Statline band:** hull 5 / deliberate / short-fast gun.
- **Sheet choices:** screen the carrier, protect a handoff point, cover a
  retreat, or angle the wall to split enemy fire while allies still pass.
- **Counterplay:** walk through or around it, attack from another heading, lob
  Falling Star over it, disable the projector with Hush, or wait out the
  visible timer.
- **Screen read:** thick team-colour edge, three crack stages, clear facing
  arrow and expiry ring.
- **Why launch:** produces the most literal escort picture in the roster while
  preserving flank counterplay.

### 5.3 Towline — formation disruptor

- **Fantasy:** a utility tug built to rescue allies and tear one enemy out of a
  screen.
- **ONE signature — Tractor Hook:** fires a visible straight tether; the first
  body hit is pulled a fixed number of legal tiles toward Towline. It treats
  allies and enemies identically.
- **Statline band:** hull 4 / standard / medium-steady gun.
- **Sheet choices:** shorten an allied carrier's exposed walk, pull a damaged
  ally behind cover, drag an enemy carrier away from escort, or break a sentry
  line.
- **Counterplay:** body-block the tether, leave its straight lane, use a wall,
  punish Towline's medium range, or bait it into pulling the wrong body.
- **Screen read:** cable line, target clamp, tile-by-tile pull ticks, impact
  spark at the final legal tile.
- **Why launch:** one symmetric rule creates rescue and disruption without
  needing separate friendly/enemy modes.

### 5.4 Patchbay — tempo medic

- **Fantasy:** a field-repair bot that trades its own pressure for an ally's
  continued run.
- **ONE signature — Repair Beam:** channels a visible beam into one allied body,
  restoring hull over time. Moving, taking hostile damage, losing line of
  sight, or changing target breaks the channel.
- **Statline band:** hull 4 / standard / short-fast gun.
- **Sheet choices:** keep a carrier alive, preserve a Palisade or Nest position,
  rotate damaged bodies through a repair rally, or abandon the channel to add
  fire now.
- **Counterplay:** pressure Patchbay, sever line of sight with smoke or a block,
  force the protected body to move, or use Sunder to make focus exceed repair.
- **Screen read:** segmented repair cable, rising hull pips, obvious break
  snap; no ambient regeneration.
- **Why launch:** provides attrition recovery through an interruptible set-piece
  rather than a global healing rule.

### 5.5 Lantern — information scout

- **Fantasy:** a survey chassis that turns one uncertain theater into a public
  team picture.
- **ONE signature — Survey Flare:** launches a non-damaging flare to a legal
  tile; after its travel it reveals a bounded area for a fixed duration,
  including bodies inside Veil smoke.
- **Statline band:** hull 3 / swift / short-fast gun.
- **Sheet choices:** inspect the next Well before allocating, reveal an ambush
  route, support Longshot, or hold the flare until the enemy carrier disappears.
- **Counterplay:** move after the flare lands, bait it into the wrong theater,
  destroy the exposed Lantern, or wait until the clearly timed reveal ends.
- **Screen read:** arcing flare, expanding scan circle, temporary grid shimmer,
  revealed silhouettes outlined rather than recoloured.
- **Why launch:** a larger map needs an active information choice and a direct
  answer to Veil.

### 5.6 Mortar — displacement artillery

- **Fantasy:** a slow fire-support platform that attacks where a formation is
  planning to stay.
- **ONE signature — Falling Star:** marks a visible target footprint; after a
  fixed two-tick delay, an arcing shell lands over walls and damages bodies in
  that small footprint.
- **Statline band:** hull 3 / deliberate / medium-steady gun.
- **Sheet choices:** break static escort geometry, clear a handoff point, punish
  a Nest, or deny the shortest return route long enough to redirect a carrier.
- **Counterplay:** leave the marker, spread the formation, dash after the tell,
  pressure Mortar, or silence the launch with Hush before it completes.
- **Screen read:** floor reticle counts down in two beats, overhead dot and
  shadow converge, compact impact ring.
- **Why launch:** prevents defensive formations from becoming permanent while
  remaining wholly avoidable.

### 5.7 Minesmith — route tax

- **Fantasy:** a patient trapper that makes one predicted tile dangerous.
- **ONE signature — Trip Node:** places one visible-to-allies, hidden-until-near
  enemy mine on an adjacent legal floor tile. Triggering deals a bounded burst;
  placing another removes the first.
- **Statline band:** hull 4 / standard / short-fast gun.
- **Sheet choices:** protect a Well exit, cover a fallback path, punish a dash
  corridor, or deliberately leave the obvious route unmined and trap the flank.
- **Counterplay:** Lantern reveals it, nearby enemies see it, basic fire can
  destroy it, alternate routes exist, and only one tile is taxed at once.
- **Screen read:** allied rune, enemy proximity reveal, rising spike tell,
  compact detonation with no lingering invisible damage.
- **Why launch:** makes drawn route prediction matter without allowing a mine
  carpet.

### 5.8 Hush — signature counter-tech

- **Fantasy:** a disruption bot that temporarily makes nearby chassis ordinary.
- **ONE signature — Null Field:** emits a short-lived circular field; enemy
  signatures cannot start inside it and active maintained signatures end.
  Movement, handoff, and basic fire remain legal.
- **Statline band:** hull 4 / standard / medium-steady gun.
- **Sheet choices:** break a protected convoy, disable a Nest before a push,
  escort a carrier through a mined or walled junction, or save the field for an
  enemy's final defensive cooldown.
- **Counterplay:** spread signatures across theaters, bait the field, fight with
  base verbs, focus Hush, or wait outside its clear radius and duration.
- **Screen read:** desaturated ring, signature icons visibly crossed out,
  collapsing maintained effects.
- **Why launch:** keeps a 16-class roster from resolving into stacked ability
  scripts while never preventing ordinary play.

### 5.9 Relay — objective passer

- **Fantasy:** a dedicated ball-handler whose best move separates the Core from
  its own body.
- **ONE signature — Arc Toss:** while carrying, throws the Core along a visible
  straight arc up to the declared range. An allied body on the landing tile
  catches it; otherwise it lands loose and neutral.
- **Statline band:** hull 4 / swift / short-fast gun.
- **Sheet choices:** use authored catch points, throw over a threatened tile,
  switch the carrier without clustering, or hold the toss because an enemy can
  intercept the landing.
- **Counterplay:** occupy or fire through the telegraphed landing lane, force a
  rushed throw, block line of travel, kill the receiver, or Hush the passer.
- **Screen read:** target landing glyph appears first, Core leaves the carrier
  beam, bright parabolic ribbon, catch or neutral-drop burst.
- **Why launch:** makes “relay” more than the game title and directly rewards
  drawn handoff routes, but the Core always remains contestable.

### 5.10 Switchback — emergency substitution

- **Fantasy:** a position-switching support that replaces the body in danger
  rather than healing or shielding it.
- **ONE signature — Exchange:** after a one-tick link tell, Switchback and one
  visible allied body exchange legal positions. Bodies retain their own hull,
  cooldown, and role; a targeted carrier drops its Core at its departure tile
  before the exchange.
- **Statline band:** hull 3 / standard / medium-steady gun.
- **Sheet choices:** extract a weak carrier while Switchback takes over beside
  the dropped Core, put Palisade on the threatened edge, trade a reserve into a
  handoff point, or fake an exchange tell to force enemy retargeting.
- **Counterplay:** break visibility, occupy either destination, destroy or Null
  the link source, or punish the low-hull Switchback after it arrives.
- **Screen read:** two endpoint rings, crossing ribbons, silhouettes swap in a
  single beat, and any carrier beam collapses into a neutral drop before motion.
- **Why launch:** creates spectacular saves with exact, visible endpoints and
  no free health or score.

### 5.11 Longshot — corridor threat

- **Fantasy:** a fragile rail platform whose power comes from making one long
  line public before firing.
- **ONE signature — Rail Line:** charges for two ticks along a fixed heading,
  then fires a piercing shot through bodies until terrain stops it.
- **Statline band:** hull 3 / deliberate / long-slow gun.
- **Sheet choices:** cover a carrier route, force an escort to spread, pair with
  Lantern sight, or hold a lane while the rest of the company contests another
  Well.
- **Counterplay:** leave the beam line, enter smoke, place Hardlight, dash onto
  Longshot, interrupt with damage if Phase B adopts it, or use the charge time
  to route elsewhere.
- **Screen read:** thin aiming filament brightens in two stages, then a single
  wide flash and sequential hit sparks.
- **Why launch:** supplies long-map pressure whose threat is stronger than its
  surprise.

### 5.12 Mason — temporary terrain

- **Fantasy:** a construction bot that bends one route without permanently
  rewriting the authored map.
- **ONE signature — Hardlight Block:** creates one temporary, destructible,
  one-tile obstacle on adjacent empty floor. A second placement removes the
  first.
- **Statline band:** hull 5 / deliberate / short-fast gun.
- **Sheet choices:** close the shortest intercept, protect a catch tile, split a
  formation, buy a Patchbay channel, or place a misleading block that sends the
  opponent toward a mine.
- **Counterplay:** destroy it, wait out the timer, route around, pull Mason out
  of position, or lob Falling Star over it.
- **Screen read:** tile wireframe rises, solidifies with a health/timer rim, and
  fractures before disappearing.
- **Why launch:** makes route drawings adaptive while respecting map authority
  and preserving an obvious state.

### 5.13 Sunder — focus coordinator

- **Fantasy:** a target-designation bot that makes one enemy the centre of the
  team's next exchange.
- **ONE signature — Target Paint:** marks one visible enemy for a fixed duration;
  the next bounded number of allied basic-projectile hits on that target deal a
  small bonus, then the mark breaks.
- **Statline band:** hull 4 / standard / medium-steady gun.
- **Sheet choices:** finish a carrier, crack Palisade, punish Patchbay's patient
  target, or withhold the mark until enough allied firing angles exist.
- **Counterplay:** break sight before application, retreat until expiry, use
  Prism Wall or smoke to waste the marked window, spread attackers, or Null the
  application.
- **Screen read:** high-contrast target brackets with a small number of breakable
  segments; each empowered hit removes one.
- **Why launch:** turns mind-level focus fire into a public commitment the
  opponent and spectator can understand.

### 5.14 Repulsor — local space controller

- **Fantasy:** a heavy kinetic chassis that breaks clusters by entering the
  dangerous centre of them.
- **ONE signature — Kinetic Burst:** pushes every adjacent body one legal tile
  directly away from Repulsor; blocked bodies stay and take no hidden substitute
  effect.
- **Statline band:** hull 5 / standard / short-fast gun.
- **Sheet choices:** scatter an escort, eject a defender from a catch tile,
  rescue an surrounded carrier, create a Longshot line, or hold the burst for a
  Well pickup race.
- **Counterplay:** keep spacing, use terrain to make the push inert, focus the
  approaching Repulsor, pull it early with Towline, or Null it in adjacency.
- **Screen read:** contracting ring warning followed by cardinal knockback
  arrows and individual blocked sparks.
- **Why launch:** provides objective-space interaction without a stationary
  capture rule or invisible weight arithmetic.

### 5.15 Veil — vision controller

- **Fantasy:** a covert support that turns one obvious route into uncertain
  space.
- **ONE signature — Smoke Canister:** deploys a bounded smoke field that blocks
  normal vision and target acquisition through it for a fixed duration. Bodies
  at adjacency remain visible; Survey Flare reveals through it.
- **Statline band:** hull 3 / swift / short-fast gun.
- **Sheet choices:** hide a handoff, cross Longshot's lane, screen a retreat,
  fake which Well the reserve joined, or blind an enemy Nest before committing.
- **Counterplay:** Lantern reveals it, enter adjacency, fire at predicted exits,
  wait for expiry, route around, or use area attacks on the visible field.
- **Screen read:** translucent dithered cloud with exact tile boundary, hidden
  silhouettes disappear cleanly, Survey outlines punch through.
- **Why launch:** gives the larger map an information game and a direct scout
  counter pair.

### 5.16 Nest — persistent theater guard

- **Fantasy:** a deployment chassis that leaves one small automated gun behind
  while the mind reallocates elsewhere.
- **ONE signature — Sentinel Seed:** deploys one stationary, low-hull,
  short-range sentry on an adjacent legal tile. It uses a deterministic nearest
  visible enemy priority; deploying another replaces it.
- **Statline band:** hull 4 / deliberate / medium-steady gun.
- **Sheet choices:** guard a Well between spawn beats, watch a return connector,
  protect a handoff point, or force the enemy to reveal which route it clears.
- **Counterplay:** outrange, flank, smoke, Null, destroy, or simply use another
  theater. Its target rule and range are public.
- **Screen read:** seed unfolds in one beat, range ring appears on selection,
  target line flashes before each shot, low hull is always visible.
- **Why launch:** supplies bounded persistence without creating another mobile
  body or fabrication economy.

## 6. Alternates, holds, and rejects

### 6.1 Mirror — projectile reversal (alternate)

- **Fantasy:** a duelist that turns one frontal shot back on its sender.
- **ONE signature — Reversal Plate:** raises a facing plate until the first
  hostile projectile contact or expiry; that projectile reverses ownership and
  heading.
- **Statline band:** hull 4 / standard / medium-steady gun.
- **Sheet choices:** lead a convoy through a sniper lane or bait a high-value
  shot.
- **Counterplay:** flank, use a low-value shot, wait, approach physically, or
  fire from two headings.
- **Screen read:** narrow facing arc with one charge pip and an explicit return
  streak.
- **Cull:** coherent and proven as a visual grammar, but overlaps Palisade's
  projectile protection in the first launch band. First replacement if the
  owner prefers reactive dueling over broader escort cover.

### 6.2 Breakhorn — committed ram (alternate)

- **Fantasy:** a heavy breacher that turns momentum into one shove.
- **ONE signature — Ram:** after a straight-line tell, advances until the first
  body and pushes it one tile if legal.
- **Statline band:** hull 5 / deliberate / short-fast gun.
- **Sheet choices:** break a catch formation or knock a carrier off its safest
  line.
- **Counterplay:** sidestep, wall, mine, bait, or occupy the destination.
- **Screen read:** ground chevrons and a single collision burst.
- **Cull:** its parts are already covered by Kestrel's commitment and
  Repulsor's shove. Retain if testing shows a dedicated breacher is needed.

### 6.3 Ghost — wall phaser (alternate)

- **Fantasy:** a skirmisher that takes one impossible shortcut.
- **ONE signature — Phase Step:** crosses exactly one adjacent wall into the
  first legal tile beyond it; a carried Core drops before phasing.
- **Statline band:** hull 3 / swift / short-fast gun.
- **Sheet choices:** scout, flank a sentry, escape a route trap, or threaten a
  shortcut that forces reserve coverage.
- **Counterplay:** cover the known exit tiles, attack before cooldown, use open
  ground, or force it to abandon the Core.
- **Screen read:** wall-side entry/exit glyphs and a bright through-wall line.
- **Cull:** mechanically distinct, but its value and fairness depend too much
  on a map not yet designed. Reconsider after the Phase B map exists.

### 6.4 Gatekeeper — paired portals (hold)

- **Fantasy:** a logistics engineer that establishes a temporary shortcut.
- **ONE signature — Twin Gates:** first use places an entrance, second use an
  exit; allied bodies may traverse while both persist.
- **Statline band:** hull 3 / deliberate / short-fast gun.
- **Sheet choices:** build a return route or cross-theater reserve line.
- **Counterplay:** destroy either gate, camp the exit, Null placement, or wait.
- **Screen read:** linked rings and a full-map connector line.
- **Cull:** one nominal skill creates a two-stage state machine and can erase
  the travel-time premise of the entire game. Hold until route play proves too
  slow, not merely because the capability is exciting.

### 6.5 Decoy — false carrier (reject)

- **Fantasy:** a deception bot that creates a false Arc Core carrier.
- **ONE signature — False Signal:** projects a moving duplicate of a chosen
  allied body and its carrier beam.
- **Statline band:** hull 3 / swift / short-fast gun.
- **Sheet choices:** split pursuit or fake a return route.
- **Counterplay:** proximity, damage, or scan reveals the false image.
- **Screen read:** by design, it tries to look real.
- **Cull:** the spectator must understand possession immediately. A deception
  whose success depends on making the central match fact visually false works
  against the watchability requirement. Reject the premise, not just the tune.

### 6.6 Leech — score theft (reject)

- **Fantasy:** a saboteur that drains charge already stored in an enemy reactor.
- **ONE signature — Backfeed:** channels at the enemy reactor to remove one
  banked charge pip.
- **Statline band:** hull 3 / standard / short-fast gun.
- **Sheet choices:** deep raid versus ordinary interception.
- **Counterplay:** defend home and interrupt the channel.
- **Screen read:** reverse beam from reactor to Leech.
- **Cull:** it erases progress after the risky carrier story has concluded,
  weakens the meaning of a delivery, and promotes home turtling. Reversals
  should happen while the Core is physically contestable. Reject.

### 6.7 Swarm — body fission (hold)

- **Fantasy:** a chassis that divides into two weak bodies and later recombines
  by timeout.
- **ONE signature — Fission:** replaces the body with two half-hull bodies
  sharing one slot budget for a fixed duration.
- **Statline band:** hull 4 / standard / short-fast gun before split.
- **Sheet choices:** scout two exits, screen two lanes, or contest a pickup with
  more contact points.
- **Counterplay:** area damage, focus one half, force separation before expiry.
- **Screen read:** one silhouette divides with linked life bars and a reunion
  timer.
- **Cull:** changes topology, observation count, targeting, collision, and
  renderer density for one kit. Hold until the base game and body budget are
  proven; do not make it a launch dependency.

### 6.8 Chrona — state rewind (hold)

- **Fantasy:** a time-skirmisher that returns to its earlier position and hull.
- **ONE signature — Recall:** after a tell, restores the body's position and
  hull from a fixed number of ticks earlier if the destination remains legal;
  carried Cores drop before Recall.
- **Statline band:** hull 3 / standard / medium-steady gun.
- **Sheet choices:** bait focus, scout then return, or rescue a failed flank.
- **Counterplay:** occupy the return tile, wait out the memory window, force a
  Core drop, or Null the cast.
- **Screen read:** visible ghost at the recorded return state and a rewind
  ribbon.
- **Cull:** potentially readable, but it makes authoritative damage and movement
  causality harder to explain than any launch kit. Hold for a later season.

### 6.9 Locksmith — Well denial (reject)

- **Fantasy:** a saboteur that temporarily shuts a neutral Well.
- **ONE signature — Seal Well:** channels beside a Well to delay its next Core.
- **Statline band:** hull 4 / standard / short-fast gun.
- **Sheet choices:** trade a body for schedule control.
- **Counterplay:** interrupt the channel or contest another Well.
- **Screen read:** countdown freezes behind a lock icon.
- **Cull:** the most exciting public beat in the game is a Core arriving. A
  class built to prevent that beat creates downtime and denial before
  interaction. Reject.

### 6.10 Conductor — damage link (alternate)

- **Fantasy:** a support that links two allies so incoming damage is divided.
- **ONE signature — Shared Circuit:** maintains a visible tether between two
  allied bodies; damage to either is split deterministically while linked.
- **Statline band:** hull 4 / standard / medium-steady gun.
- **Sheet choices:** protect a carrier, preserve a Palisade, or distribute
  repair value.
- **Counterplay:** separate the pair, break line of sight, attack both, Null the
  tether, or focus Conductor.
- **Screen read:** bright cable and paired damage ticks.
- **Cull:** strategically sound, but split damage can obscure why a distant body
  lost hull and overlaps Patchbay's preservation role. Alternate if repair
  proves too interruption-sensitive.

### 6.11 Fuse — self-destruction (hold)

- **Fantasy:** a volatile breacher that turns its remaining hull into a public
  blast.
- **ONE signature — Critical Mass:** begins a long, cancelable self-destruct;
  on completion the body dies and damages a compact radius.
- **Statline band:** hull 4 / standard / short-fast gun.
- **Sheet choices:** break a packed escort, deny a nearly lost Well, or force
  evacuation.
- **Counterplay:** spread, leave the radius, Towline it away, Hush it, or destroy
  it before the high-damage completion if that cancels the blast.
- **Screen read:** unmistakable escalating rings and final silhouette collapse.
- **Cull:** excellent spectacle but vulnerable to degenerate trade arithmetic,
  deliberate Core dropping, and awkward “kill it or do not kill it” rules.
  Hold for a later explicit sacrifice study.

### 6.12 Magnet — loose-Core controller (alternate)

- **Fantasy:** an objective specialist that changes where a dropped Core must
  be fought over.
- **ONE signature — Core Draw:** pulls the nearest visible loose Core along a
  straight legal line toward Magnet by a bounded number of tiles.
- **Statline band:** hull 4 / deliberate / short-fast gun.
- **Sheet choices:** recover a dangerous drop without stepping into focus,
  steal a Core from the edge of an enemy screen, or move it toward a handoff
  point.
- **Counterplay:** pick up the Core first, block the line with bodies/terrain,
  threaten Magnet, or Null the draw.
- **Screen read:** magnetic line locks, then the Core advances tile by tile
  while remaining neutral.
- **Cull:** clean and objective-native, but risks becoming mandatory in every
  sheet because it uniquely changes loose-Core geometry. Keep as the first
  objective specialist to test if Relay's passing does not create enough Core
  play.

## 7. Why these 16 launch together

### 7.1 Mechanical coverage

| Strategic function | Launch classes | What remains a player decision |
| --- | --- | --- |
| Rapid response and pursuit | Kestrel, Switchback | commit mobility now, preserve it for the next theater, or trade position with an ally |
| Carrier logistics | Relay, Towline, Switchback | walk, adjacent handoff, long toss, pull, or exchange—and where the receiver waits |
| Escort and sustain | Palisade, Patchbay, Mason | block fire, repair attrition, or bend the route; each loses to a different counter |
| Interception and formation break | Towline, Repulsor, Sunder | displace, scatter, or focus; none directly deletes a Core or score |
| Information | Lantern, Veil | buy certainty or create uncertainty; direct counter pair with wider uses |
| Route and theater control | Minesmith, Mason, Nest | tax one tile, block one tile, or guard one area; all bounded and destroyable |
| Fire support | Mortar, Longshot, Sunder | delayed area, public line, or team focus |
| Counter-tech | Hush | spend a body and cooldown to turn signatures off locally while base play continues |

### 7.2 Counter web

The roster does not claim a solved balance triangle. It supplies several
different answer paths so later native minds can discover a web:

| Pressure | Visible answers |
| --- | --- |
| Palisade projectile cover | Mortar over it; Towline/Repulsor displace around it; Hush ends it; bodies walk through it. |
| Mortar/Longshot fire support | Kestrel closes; Veil denies target lines; Mason blocks rail; Hush interrupts signatures; route elsewhere. |
| Veil uncertainty | Lantern reveals; adjacency spots; area attacks threaten the known field; wait or reroute. |
| Nest/Minesmith control | Lantern detects; Longshot/Mortar clears; Hush disables; alternate theater makes persistence miss the fight. |
| Relay/Switchback carrier saves | public target/endpoint tells; Sunder focus; Hush; occupy catch/swap destination; pressure the receiving body. |
| Patchbay sustain | break line, move the target, damage Patchbay, Sunder the target, or force a multi-theater split. |
| Kestrel pursuit | mines, blocks, occupied landing lanes, Palisade line cover, or a deliberate Core drop before dash. |
| Hush counter-tech | bait the field, spread cooldowns/theaters, use base verbs, focus Hush, or wait outside the radius. |

No launch class is the sole answer to another. That is intentional: an
unlockable class may broaden the response, but an opponent who does not own one
specific answer must still have positional and base-verb counterplay.

### 7.3 Honest cull rationale

The launch band is 16 rather than the earlier commander's illustrative 10–12
because the later commission explicitly asks for 15–20 classes and because the
recommended set reaches 16 without stat-only duplicates. It stops at 16 because
the remaining candidates each add one of four avoidable burdens:

- overlap (Mirror, Breakhorn, Conductor);
- map dependency (Ghost);
- topology or causality cost (Gatekeeper, Swarm, Chrona, Fuse);
- damage to the core watchability/earned-score premise (Decoy, Leech,
  Locksmith);
- risk of becoming a mandatory objective specialist (Magnet).

This leaves clear seasonal growth without withholding an answer required for
the launch counter web.

## 8. Gate 1 audit

| Handover requirement | Evidence in this report | Status |
| --- | --- | --- |
| Design the game before classes | Sections 2–3 define and compare the full Arc Relay concept before the class pool. | Met |
| Core loop and win condition | Sections 3.2–3.4 define Core production, possession, handoff, drops, delivery, Pulses, reactor destruction, timeout ranking, and lifecycle. | Met at Phase A design fidelity |
| Bigger-map intent | Section 3.6 defines three theaters, multiple routes, lateral connectors, and travel as a cost without prematurely authoring map tiles. | Met |
| Commander-mode player layer retained | Section 3.7 maps stable, composition, policies, drawings, and ordered gambits into the game. | Met |
| Fun to watch is first-class | Sections 3.1 and 3.8 define the visible story and Canvas2D language; Section 3.9 states galleries remain the only fun authority. | Met as design; unproven as felt experience |
| Candidate kits well beyond 20 | Sections 4–6 contain 28 candidates. | Met |
| One signature per class | Every brief names exactly one signature; common verbs are defined once in Section 3.3. | Met |
| Each brief covers fantasy, statline, sheet choices, counterplay, screen read | Sections 5–6 use those exact fields for all 28 candidates. | Met |
| Cull to recommended launch band | Section 7 recommends 16 and records alternates, holds, rejects, and reasons. | Met |
| Do not enter Phase B | No map file, rules numbers, runtime/API contract, implementation, or balance run was produced. | Met |
| Report format | `DECISION NEEDED`, `RESULT`, `EVIDENCE`, and `NEXT` appear in the required order, with codenames spelled out. | Met |
| No new DECISIONS numbers | Existing rulings are referenced only through the handover context; no decision log was edited or number minted. | Met |

There are intentionally no replay counts, distinct-outcome counts, galleries,
or artifact hashes for this gate. Phase A produced a design document, not an
implemented candidate. Reporting invented play evidence would be weaker than
stating the boundary.

NEXT: none without owner input. If Arc Relay and a roster are approved, Phase B may author the larger map and core-mechanics brief. If the concept or roster changes, revise this gate first. Do not implement anything from this report until the owner rules.
