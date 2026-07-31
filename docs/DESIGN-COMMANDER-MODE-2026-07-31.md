# Commander mode — the passive manager layer (2026-07-31)

Owner-directed design thread, captured from the 2026-07-31 discussion.
Direction is ruled (DECISIONS #195); individual mechanics in here are
DESIGN, not commitments, except where a DECISIONS number says otherwise.

## The vision in one paragraph

nilbots' top tier is and remains coding minds. Commander mode is the
on-ramp: a non-coder plays a PASSIVE manager game — they author a
sheet, their entrant fights on the ladder while they live their life,
and the game is really played in the reading and adjusting afterward.
No live input, no sessions, no pause machinery: authorship happens
between matches, matches run blind and headless exactly as today.
The owner's ruling that fixed the shape: drawn plans are PER-SHEET —
a saved plan the bot executes blind — not live redrawing. Football
Manager, not Mechabellum.

## Why the mind architecture makes this possible

Before the mind (#190) there was no honest shape for non-coder play:
eight bodies, eight brains. Now a player IS a mind, and a mind can be
parameterized. The non-coder does not write a mind; they CONFIGURE a
stock one. A configured stock mind compiles to an ORDINARY artifact
with the config baked in — matches stay deterministic and
replay-identical, the ladder does not know or care how an entrant was
produced, and nothing about "what a match is" changes. The entry path
changes how you PRODUCE an artifact, not what the platform does.

Leaning (owner not yet ruled): sheet-players and C#-minds share ONE
ladder. Stock minds have an honest skill ceiling; climbing past it
means opening the hood — the conversion funnel to coding. The ramp is
continuous: pick a bot -> tune knobs -> fork template-grade source ->
write your own. Each step small.

## The sheet — what the player actually decides

Grounded in the current warpath numbers (tranches +2 at tick 150 and
+3 at 300, veins ticking 8 scrap, six-tier board, channel captures):

- STABLE PICK: which classes (from the player's stable) this sheet
  fields, and which stock mind drives them.
- COMPOSITION PLAN: chassis per slot at opening and per tranche.
- UPGRADE PRIORITY: an ordered spend list with a reserve trigger
  ("buy when bank >= cost + 6").
- ECONOMY POLICY: vein contention, courier assignment, bank floor.
- CAPTURE POSTURE: which positions, channel-only-when-escorted vs
  solo poke, recapture priority.
- ROLES: per-slot role assignments (rendered via the existing
  cosmetic role-tag layer).
- GAMBITS: an ORDERED list of in-match conditionals, FF12-style —
  "IF enemy fields >=4 bulwarks by tick 300 -> tranche 300 becomes
  1x fabricator + 2x striker". First match wins.
- DRAWINGS: paths, zones, rally lines (next section).

Structural consequence of passive play: matchmaking is BLIND — the
sheet never sees the opponent pre-game — so the gambit block carries
ALL counter-play. In-match conditionals are not a nice-to-have; they
are the core adaptive mechanism, and the stock mind's config schema
must make them first-class. The depth audit's sharpest question
follows directly: do gambit-bearing sheets beat static ones?

## Drawn spatial annotations

The player draws on the map: patrol paths, hold zones, rally lines,
advance arrows. Owner-ruled PER-SHEET (saved plans executed blind).

Architecturally free: a drawing is spatial config data (waypoint
lists, tile sets, anchor-relative offsets) that the STOCK MIND
interprets. Formations and formation-keeping are doctrine — bot
behavior — never engine rules. Zero engine change, determinism
untouched, the drawing UI writes config, not commands.

The real cost: formation-keeping quality is the stock mind's hardest
doctrine requirement. Path-following, spacing under fire, and
reforming must be GOOD or drawn plans feel ignored — the feature
lives or dies on this, not on the UI.

## Classes: the stable and the launch band

Owner direction: many more classes; a player fields a STABLE of ~5
drawn from the full roster. Target a launch band of ~10-12
MECHANICALLY DISTINCT classes (not stat shuffles), grown seasonally —
with a stable of 5 and eight slots, the composition space is already
enormous; tens of classes at launch is a content treadmill.

Prime dissolution (#194) is what makes classes cheap: a class is now
a chassis — one statline, a kit, one signature mechanic — not a
prime/child lifecycle pair. And the engine already holds dormant
mechanics that are class identities waiting to happen:

- anchor/turret forms  -> siege class
- Split                -> swarm class
- projectile deflection -> warden class
- MUSTER (dormant #186) -> objective specialist
- ground healing        -> possibly a MEDIC class kit rather than a
                           global rule (re-rule when healing returns
                           from the paused set)
- optic/vision axis     -> scout class
- team auras            -> support class (owner liked team-wide buffs
                           in the capture design window)

Balance implication: the triangle read becomes a matchup web; the
harness scales to it (that is what the campaign built).

## Rewards: breadth, never power

Unlockables: new classes (the stable widens), new stock minds, SHEET
PARTS (a third gambit slot, conditional tranche plans, per-map
overrides — FF12 literally sold gambit slots), cosmetics via the
role-tag layer. The hard principle: unlocks are BREADTH — more
options, never stronger options — so one ladder stays honest (the
Magic-the-Gathering answer to progression vs fairness).

The loop closes with passive play: overnight matches earn the
currency, so the MORNING REPORT is simultaneously results-reading and
the reward moment. Its contents: results, decisive-moment replays,
loss attribution in plain words, and COUNTERFACTUAL COACHING —
determinism makes re-sims cheap and exact, so the report can say "with
plate prioritized over edge, this loss becomes a win" because we ran
it. No manager game has ever had that.

Ladder ecology is a depth source in its own right: rotating opponent
pools, seed and map rotation (deterministic bots vs the same opponent
replay the same match forever — the campaign's N-seeds law becomes a
product requirement), seasons, new stock minds, coder minds drifting
into the pool. A shifting meta keeps sheets from being solved.

## Map scale

Owner instinct, endorsed: the current frontline (~a dozen useful
tiles wide, 8-9 bodies a side) has no "where" — one contact blob;
spatial authorship needs distinct theaters, multiple viable routes,
and travel time as a real cost. Three things line up behind a bigger
map:

1. The mind removed the old penalty: per-life bots paid a
   reorientation tax every death; persistent memory flips big maps
   into an asset (scouting compounds, optic gets more valuable). Map
   scale was already threaded into the mind design as headroom.
2. The wider roster needs it: scout/siege/swarm classes only FEEL
   different with space to be different in. Big map and big roster
   are one decision wearing two hats.
3. The machinery exists: map generation 3 (named spawns, typed
   regions/tags) — regions are lanes and theaters waiting to be
   authored. Re-rule vein geography, the capture lattice, and the
   horizon; no new engine systems.

## The depth audit — the empirical go/no-go

The honest question under all of this: with doctrine held fixed (both
sides run the SAME stock mind), is the sheet-choice space a real game
— non-transitive, cycling — or does it collapse to one dominant
sheet? Not debatable; measurable with the existing harness:

- Fix stock mind v0. Enumerate a sheet grid (compositions x upgrade
  orders x postures x gambit variants).
- Run the tournament; read the payoff matrix for cycles vs dominance
  and for sensitivity (do sheet changes move outcomes at all?).
- Sharpest single question: do gambit-bearing sheets beat static
  ones? That is the whole commander-mode thesis in one measurement.
- If thin: add sheet dimensions where measurement says (the paused
  FOUNDRY tempo-as-policy, healing, MUSTER, map features) rather
  than guessing.

## Sequencing

1. One-chassis production package lands (task #48, building now:
   prime dissolution + headless fabricator + slot-scoped
   composition).
2. STOCK MIND v0: map-agnostic, sheet-first config schema
   (composition plans, priorities, policies, gambits, drawings),
   formation-keeping doctrine. Also the first mind-native bot —
   useful groundwork regardless of where commander mode goes.
3. Depth audit + map-scale prototype run TOGETHER on stock mind v0
   (the wave-8 cohort cannot carry either read — doctrines baked to
   the small map, priorities baked into code).

## Ruled, leaning, and open

- RULED (#195): the direction itself; drawings are per-sheet; no live
  decision points or interactive sessions — the Mechabellum-style
  variant is dead.
- RULED earlier (#194): build the game before more bots; prime
  dissolution + composition green-lit; FOUNDRY tempo, home delivery,
  and ground healing PAUSED for design — when they return, design
  them sheet-first (policy-expressible), which serves the mind API
  equally.
- LEANING (owner not yet ruled): one shared ladder; curated stock
  minds first, community-published minds with exposed tunables as the
  follow-on (bot authors become content creators).
- OPEN: reward economy specifics; class launch-band contents; map
  target size; whether ground healing returns as a rule or a medic
  class.
