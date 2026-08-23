# Arc Relay Tapline depth — sheet strategy-space catalog

Status: **candidate author briefs, not implemented sheets or balance claims**.
This catalog assumes the five isolated candidates in
[`EXPERIMENTAL-ARC-RELAY-TAPLINE-DEPTH.md`](EXPERIMENTAL-ARC-RELAY-TAPLINE-DEPTH.md):
team-relative `1 / 2 / 4` scoring, veteran command, synchronized respawn
waves, one duplicate-class wildcard, and durable deployment control.

The purpose is to answer a stricter question than “can we name 30 sheets?”:

> Can at least 30 sheets make materially different commitments that give the
> opponent a materially different best reply?

The answer at the concept level is **yes**. The 36 briefs below were produced
without adding another game system or reviving a named-play state machine.
They are not proof that the game is fun, balanced, or capable of supporting a
healthy metagame. That proof requires authored sheets, readable replays, and a
counter-web. They do show that the proposed rules expose substantially more
macro-strategy than one nearest-Core race.

## What counts as distinct

A roster swap, renamed route, different threshold, or cosmetic order change
does not count. A brief earns a separate slot here only when it changes at
least one of these commitments and therefore changes sensible counterplay:

1. the charge recipe or Core source it expects to win with;
2. the scarce asset it develops or protects: Core, veteran, wave, or
   deployment;
3. where it deliberately concedes map access;
4. when it attacks, retreats, kills, or replaces a deployment; or
5. what opposing strategy it is designed to punish.

Every roster sketch contains exactly eight bodies, at least seven distinct
classes, no triple, and at most one duplicated class. The sketches assume all
classes are unlocked and are starting points, not class-balance verdicts.

## Narrow authorability audit

Most of the strategy language already exists in Orders v2: roles, priority
branches, `when`/`until`, shared orders, capacities, route/zone/anchor plans,
role following, Well facts, and score facts. The five-mechanic brief already
adds `reactor-pulses`, `role-highest-level`, ace/experienced-ally selection,
`deployment-active`, and the hostile `deployment` fight target.

Writing the concepts honestly exposes the following additional narrow leaves
and facts. They belong to the five candidate mechanics; none is another
top-level play grammar.

| Addition | Exact purpose |
| --- | --- |
| body fact `self-carried-core-bank-value` | Returns `0`, `1`, `2`, or `4` for this body, so a sheet may risk, toss, or escort a premium Core differently. |
| team fact `enemy-reactor-charge` | Exposes the enemy's public `0..2` remainder. Existing `reactor-integrity` already reveals its Pulse count. |
| move target `highest-bank-value-known-loose-core` | Selects by value to this team, then terrain distance, then stable Core ID. It uses only the existing team objective ledger. |
| hostile target `veteran` for `fight` and compatible `use` | Selects the highest-level visible enemy above level 1, then reachable distance and stable unit ID. It makes the required bounty counter authorable. |
| team facts `ticks-until-respawn-wave`, `live-enemies`, `friendly-respawns-next-wave`, `enemy-respawns-next-wave` | Turn the public wave clock and public stable-slot lifecycle into sheet decisions rather than viewer-only decoration. |
| body facts `deployment-lifetime-percent`, `deployment-durability-percent` | Let a builder preserve, refresh, or relocate its one stable-slot-owned deployment intentionally. Both return `0` when none exists; a non-expiring active Trip Node has 100% lifetime. |

Flat named conditions remain sufficient. Score has only nine pre-win charge
states, the wave clock has a 30-tick period, and the executor keeps all target
resolution and collision micro. No arithmetic expression language, formation,
phase machine, or hidden squad memory is needed.

## 36 candidate sheets

### A. Charge-route doctrines

#### 01 — Nine Small Steps

- **Roster:** Relay x2; Palisade, Patchbay, Mason, Hush, Lantern, Towline.
- **Thesis:** Service the own Well for nine 1-charge banks, keep both Relays
  alive, and turn the home approach into the safest logistics line on the map.
  The Mason also guards the same Core source that would be worth 4 to the
  enemy.
- **Why it earns a sheet:** It accepts the slowest possible clock in exchange
  for short routes and dense support. The opponent should race five centre
  Cores or raid this supposedly safe Well, not mirror its home posture.
- **Failure/counterplay:** Losing even one late birth can strand it below nine;
  a faster plan can simply make its safety irrelevant.

#### 02 — Centre Dividend

- **Roster:** Repulsor x2; Relay, Patchbay, Palisade, Hush, Lantern, Sunder.
- **Thesis:** Own the centre long enough to bank five 2-charge Cores. Twin
  Repulsors break clustered contests while the remaining cast sustains and
  reveals the central fight.
- **Why it earns a sheet:** Its resource is repeatable centre tenure rather
  than courier distance or a single heist. It creates a readable “move the
  champions off the ring” match.
- **Failure/counterplay:** Split pressure makes eight centre-oriented bodies
  expensive; deep raiders can win with fewer deliveries while it keeps paying
  for the same ground.

#### 03 — Two Heists and a Tip

- **Roster:** Kestrel x2; Relay, Veil, Lantern, Hush, Towline, Patchbay.
- **Thesis:** Bank two enemy-Well Cores for `4 + 4`, then close with one own
  Core. Twin Kestrels create two raid timings; smoke, information, suppression,
  hook, and repair support extraction rather than a standing frontline.
- **Why it earns a sheet:** It tries to win in only three banks and makes each
  far pickup a major public event. Every death or intercepted Core matters far
  more than in a nine-bank plan.
- **Failure/counterplay:** A prepared home guard, body block, or lost premium
  Core can collapse the entire arithmetic; the opponent should force it to
  play for 2s and 1s with a roster built for raids.

#### 04 — Bridge and Heist

- **Roster:** Towline x2; Relay, Switchback, Patchbay, Palisade, Lantern,
  Sunder.
- **Thesis:** Secure one 4-charge raid, two centre banks, and a safe closer for
  `4 + 2 + 2 + 1`. One Towline works the far theft while the other controls
  the centre bridge.
- **Why it earns a sheet:** Unlike the all-in heist, this is a four-bank
  portfolio with one premium commitment and a planned fallback. The opponent
  has to decide whether to over-defend the far Well or break its centre income.
- **Failure/counterplay:** It can be merely adequate everywhere and dominant
  nowhere; concentrated centre control can deny both its 2s after surviving
  the first raid.

#### 05 — Exact Change

- **Roster:** Switchback x2; Relay, Kestrel, Lantern, Patchbay, Hush, Towline.
- **Thesis:** Re-evaluate the cheapest available source after every bank. At 5
  total charge seek 4, at 7 seek 2, and at 8 seek 1; in earlier states compare
  the next complete recipe with source availability rather than defaulting to
  the nearest Core. The two Switchbacks rescue whichever runner currently
  carries the needed denomination.
- **Why it earns a sheet:** The plan is defined by score arithmetic rather
  than a favored lane. Denying one denomination can force a visible route
  change, so the opponent can manipulate its next commitment.
- **Failure/counterplay:** Adaptation costs travel and can produce indecisive
  rotations; pressure two sources at once and make the “correct” value too
  slow to collect.

#### 06 — Premium Lockout

- **Roster:** Mason x2; Nest, Minesmith, Hush, Relay, Patchbay, Lantern.
- **Thesis:** Treat the own Well first as the enemy's 4-charge source and only
  second as this team's 1-charge source. Hold it with two durable barriers and
  a deployment net while the Relay works centre and opportunistic home Cores.
- **Why it earns a sheet:** This is value denial, not ordinary home camping:
  it tries to force the opponent to make nine safe banks or win repeated
  centre fights while protecting the single source that enables three-bank
  wins.
- **Failure/counterplay:** It spends many slots preventing hypothetical value.
  Ignore the fortress, clear the centre, and win before its low-throughput
  route pays back.

### B. Theft, recovery, and carrier logistics

#### 07 — Turncoat Intercept

- **Roster:** Towline x2; Sunder, Longshot, Relay, Lantern, Hush, Kestrel.
- **Thesis:** Let the enemy collect its nearby 1-charge Core, cut the return
  route with hooks and focused fire, then reverse the dropped Core across the
  map for 4 charge.
- **Why it earns a sheet:** The enemy performs the dangerous pickup and first
  leg of transport for you. Its timing target is a carrier leaving the Well,
  not control of the Well itself.
- **Failure/counterplay:** Escort the cheap Core, use another return lane, or
  deliberately leave it loose; the interceptors otherwise wait in the wrong
  place and concede centre.

#### 08 — Loose Gold Recovery

- **Roster:** Lantern x2; Kestrel, Relay, Towline, Veil, Patchbay, Repulsor.
- **Thesis:** Maintain a mobile recovery pair whose first claim is the
  highest-value known loose Core, wherever an earlier fight dropped it. Twin
  Lanterns keep the objective ledger fresh while Kestrel reaches the recovery
  before a conventional runner.
- **Why it earns a sheet:** It treats a failed 4-charge raid as persistent
  strategic state rather than a discarded attempt. Battles can have a second
  act around the same valuable object.
- **Failure/counterplay:** Bait the recovery team with a visible premium Core,
  then bank elsewhere; smoke, stale information, and simultaneous loose Cores
  stretch its specialist slots.

#### 09 — Double Relay Airbridge

- **Roster:** Relay x2; Palisade, Patchbay, Lantern, Hush, Mason, Kestrel.
- **Thesis:** Stage two distinct catch points and move a 4-charge Core through
  two bounded Arc Toss legs rather than one continuous carrier path. The
  Mason and Palisade protect the public catch geometry.
- **Why it earns a sheet:** Its weak points are receivers and landing lanes,
  not the carrier's entire walk. The opponent can win by breaking one scheduled
  catch rather than grinding through a convoy.
- **Failure/counterplay:** Occupy a landing tile, Hush either Relay, destroy the
  forward barrier, or force a rushed neutral drop between stages.

#### 10 — Exchange Catch

- **Roster:** Switchback x2; Relay, Patchbay, Palisade, Towline, Lantern,
  Kestrel.
- **Thesis:** When a carrier is trapped, Exchange it with a waiting
  Switchback. The Core intentionally drops at departure; the arriving
  Switchback becomes the recovery body while the damaged carrier escapes.
- **Why it earns a sheet:** It converts Exchange's mandatory carrier drop into
  a designed custody change instead of treating the skill as failed transport.
  The public endpoint tell creates an understandable rescue puzzle.
- **Failure/counterplay:** Occupy either endpoint, damage the link, cover the
  departure Core, or make the low-hull Switchback win the follow-up fight.

#### 11 — Hook and Reverse

- **Roster:** Towline x2; Sunder, Relay, Repulsor, Patchbay, Hush, Lantern.
- **Thesis:** Pull a carrier into a prepared short-range pocket, scatter its
  escorts, paint the carrier, and give the released Core to Relay for the
  counter-bank.
- **Why it earns a sheet:** It is not denial for denial's sake: the entire
  formation exists to turn a hostile possession into this team's next scoring
  attempt.
- **Failure/counterplay:** Prism cover or a bodyguard can intercept Hook's declared ray, Hush can remove the
  combo, and a disciplined carrier can route outside the pocket and make the
  formation idle.

#### 12 — Blackout Convoy

- **Roster:** Veil x2; Relay, Lantern, Palisade, Patchbay, Hush, Mason.
- **Thesis:** Escort one premium Core through overlapping smoke windows, using
  Lantern to preserve the team's own read and durable cover to protect the
  exit when smoke ends.
- **Why it earns a sheet:** It trades map breadth for an information asymmetry
  and a single protected 4-charge journey. The duel is “reveal, wait, or enter
  blind,” not merely focus-fire the carrier.
- **Failure/counterplay:** Twin Lantern coverage, adjacency, Hush, area damage,
  or attacking the conceded Wells can make two Veils an expensive luxury.

### C. Veteran command doctrines

#### 13 — Crowned Rail

- **Roster:** Patchbay x2; Longshot, Sunder, Mason, Lantern, Palisade, Relay.
- **Thesis:** Longshot is the authored ace. Sunder helps it secure kills,
  Patchbays repair its role specifically, and Mason/Palisade protect its firing
  lane while it invests damage, reach, and vitality.
- **Why it earns a sheet:** A fragile artillery body becomes a visible
  long-horizon asset whose growth changes how aggressively the team can cover
  a premium raid.
- **Failure/counterplay:** Dive or smoke the rail line, force the supports off
  objectives, or collect the veteran bounty before its investment repays the
  slots protecting it.

#### 14 — Hook Captain

- **Roster:** Towline, Sunder, Patchbay, Hush, Relay, Kestrel, Mason,
  Repulsor.
- **Thesis:** A singleton Towline begins as a centre interceptor, farms
  ordinary combat XP, and at a level threshold becomes the captain of a
  far-route hunting group. Support follows the role, not whichever ally is
  currently weakest.
- **Why it earns a sheet:** Veterancy changes this body's job mid-match—from
  local control to premium-carrier hunter—so protecting or killing it changes
  the team's geography.
- **Failure/counterplay:** Deny early kills, bait the hook, or attack the lane
  it abandons after promotion.

#### 15 — Carrier Crown

- **Roster:** Patchbay x2; Relay, Palisade, Mason, Lantern, Kestrel, Hush.
- **Thesis:** Relay first fights and invests to become a durable veteran, then
  becomes the designated 4-charge carrier with two medics and role-following
  escorts. It must choose quiet investment ticks before picking up because
  carrying still blocks investment.
- **Why it earns a sheet:** It makes “cash out the veteran as a courier” a
  deliberate transition and exposes a real tempo choice between scoring now
  and upgrading first.
- **Failure/counterplay:** Force an early pickup, focus the ace before the
  transition, or contest two Wells so the escort stack cannot follow both.

#### 16 — Emergent Standard

- **Roster:** Kestrel, Palisade, Towline, Patchbay, Lantern, Hush, Relay,
  Sunder.
- **Thesis:** No body is pre-crowned. The Patchbay and screen use
  `most-experienced-ally` only after someone reaches level 2, making the first
  successful fighter the team's temporary standard-bearer.
- **Why it earns a sheet:** Its protagonist emerges from the match rather than
  the JSON. Different early kills produce different formations and replay
  stories without nondeterminism.
- **Failure/counterplay:** Trade evenly so no leader separates, kill the new
  standard immediately, or exploit the support rotation when experience
  changes hands.

#### 17 — Twin Pretenders

- **Roster:** Longshot x2; Patchbay, Mason, Sunder, Lantern, Relay, Hush.
- **Thesis:** One Longshot works each outer corridor. Both may become ace, but
  the central support group rallies to the highest-level candidate; an equal
  veteran tie resolves by the declared distance and stable-ID rules.
- **Why it earns a sheet:** The wildcard becomes redundancy and an internal
  race rather than raw stacking. Pressure in one theater can crown the other
  candidate and redirect the whole team.
- **Failure/counterplay:** Keep both candidates below level 2, attack the
  unsupported one after the rally, or induce rapid lead changes that waste
  support travel.

#### 18 — Succession Line

- **Roster:** Patchbay x2; Longshot, Towline, Mason, Relay, Lantern, Hush.
- **Thesis:** Longshot is the primary ace and Towline the named heir. Supports
  stay with the primary while its role is live, then immediately fall through
  to the heir's orders on death rather than searching for a new emergent
  leader.
- **Why it earns a sheet:** It pays some peak power for predictable resilience.
  Killing the ace wins a window but does not erase the team's command plan.
- **Failure/counterplay:** Damage the heir before killing the ace, or force the
  primary to retreat alive so the fallback never activates.

### D. Respawn-wave doctrines

#### 19 — Green-Window Diver

- **Roster:** Kestrel x2; Repulsor, Towline, Relay, Lantern, Hush, Patchbay.
- **Thesis:** The interceptors may make sacrificial carrier dives only while
  enough time remains to die, satisfy the 15-tick minimum, and return on the
  next wave. Near the eligibility cutoff they screen or retreat instead.
- **Why it earns a sheet:** Death becomes a scheduled resource. The opponent
  can stall a diver across the cutoff rather than merely kill it faster.
- **Failure/counterplay:** Kite the dive until it misses eligibility, then take
  the objective during its 44-tick absence.

#### 20 — Cutoff Execution

- **Roster:** Sunder x2; Longshot, Towline, Hush, Lantern, Patchbay, Relay.
- **Thesis:** Hold or kite damaged enemies while they can still qualify for the
  next wave; once `ticks-until-respawn-wave` falls below the 16-tick eligibility
  window, paint and focus them so a death misses that wave and waits for the
  following one.
- **Why it earns a sheet:** The same kill has different strategic value based
  on when it lands. The public countdown makes the intent readable and lets
  the victim retreat before the execution window.
- **Failure/counterplay:** Force an early fight, disengage as the bell passes,
  or use the pre-bell restraint to bank uncontested.

#### 21 — Beat-the-Bell Courier

- **Roster:** Relay x2; Kestrel, Switchback, Palisade, Patchbay, Lantern,
  Hush.
- **Thesis:** When enemy bodies are due on the next wave, the carrier and its
  minimum escort take urgent routes and spend transport cooldowns to bank
  before those slots return.
- **Why it earns a sheet:** It converts a temporary numbers edge into an
  explicit race against a visible timer rather than an indefinite deathball.
- **Failure/counterplay:** Body-block or displace the courier for a few ticks;
  once the wave arrives, its spent cooldowns and exposed route become liabilities.

#### 22 — Wavefront Eight

- **Roster:** Palisade x2; Patchbay, Mason, Sunder, Relay, Lantern, Hush.
- **Thesis:** Survivors withdraw to a rally while several allies are due, then
  all eight launch from the same wave behind two projectile screens and one
  durable route block.
- **Why it earns a sheet:** It deliberately gives up short-term map tempo for
  a legible full-strength push. The regroup condition is public and can be
  punished rather than happening through hidden formation code.
- **Failure/counterplay:** Harvest two Wells during the rally, seed the exit,
  or split the wavefront across routes its concentrated protection cannot
  cover.

#### 23 — Pre-Wave Evacuation

- **Roster:** Switchback x2; Kestrel, Patchbay, Veil, Relay, Palisade,
  Lantern.
- **Thesis:** When multiple enemies are about to respawn, preserve the live
  veteran and carrier: exchange weak allies out, smoke the retreat, and concede
  the current fight before fresh bodies arrive.
- **Why it earns a sheet:** It values continuity and veteran survival over one
  more contested tick. A wave becomes a reason to stop fighting, not only to
  start.
- **Failure/counterplay:** Chase the evacuation, occupy Exchange endpoints, or
  take the conceded premium Core without following into smoke.

#### 24 — Open-Window Raid

- **Roster:** Kestrel x2; Relay, Veil, Towline, Hush, Lantern, Repulsor.
- **Thesis:** If several enemies are absent and few or none return on the next
  bell, the two Kestrels abandon routine coverage and attack the enemy Well;
  otherwise they remain elastic reserve.
- **Why it earns a sheet:** It spends a measured manpower window on the
  highest-value source rather than simply pushing the nearest fight.
- **Failure/counterplay:** Keep one durable home guard alive, leave a sentry,
  or trade bodies so the apparent opening ends on the next wave.

### E. Persistent terrain and deployment doctrines

#### 25 — Double Mason Lockbox

- **Roster:** Mason x2; Nest, Minesmith, Hush, Relay, Patchbay, Lantern.
- **Thesis:** One Mason protects the own Well from a 4-charge theft while the
  other bends the centre return route. Both preserve their 30-tick barriers
  until damaged, expiring, or strategically obsolete.
- **Why it earns a sheet:** The wildcard buys simultaneous ownership of two
  specific tiles, but only at the cost of slow bodies and little direct
  pressure. Barrier state persists long enough for an opponent to plan a
  demolition or third-lane answer.
- **Failure/counterplay:** Hush one lock, shoot the other, lob over it, or raid
  the deliberately unbuilt lane.

#### 26 — Leapfrog Wall

- **Roster:** Relay x2; Mason, Palisade, Patchbay, Lantern, Hush, Kestrel.
- **Thesis:** A single Mason deliberately replaces its still-active barrier at
  successive forward anchors as a premium convoy advances. The previous safe
  tile is abandoned each time.
- **Why it earns a sheet:** `deployment-active` becomes a relocation choice,
  not an automatic “do not cast” guard. Every recast visibly trades rear
  security for forward tempo.
- **Failure/counterplay:** Attack from behind after replacement, force an early
  recast at a bad anchor, or destroy the Mason before the next leap.

#### 27 — Rearguard Wall

- **Roster:** Mason x2; Relay, Towline, Patchbay, Veil, Lantern, Switchback.
- **Thesis:** The Masons place behind a withdrawing carrier, alternating two
  barriers so the egress closes in stages while the front remains physically
  open to the courier.
- **Why it earns a sheet:** It uses the same deployment as Double Mason and
  Leapfrog but reverses the geometry and timing: the investment protects
  distance already won rather than territory ahead.
- **Failure/counterplay:** Flank the barrier chain, use Falling Star, or force
  the carrier to retreat through its own blocked line.

#### 28 — Wall-Mine Fork

- **Roster:** Minesmith x2; Mason, Mortar, Nest, Lantern, Hush, Relay.
- **Thesis:** Mason closes the clean return gate, each Minesmith watches an
  alternate, and Mortar punishes bodies that stop to dismantle the visible
  choice.
- **Why it earns a sheet:** It authors a route dilemma with three different
  counters rather than stacking damage on one tile. The opponent chooses to
  break, trigger, suppress, or reroute.
- **Failure/counterplay:** A coordinated Hush breach, long-range demolition,
  or an uncaptured third route can make the entire network miss the carrier.

#### 29 — Sentinel Lease

- **Roster:** Nest x2; Mason, Minesmith, Hush, Lantern, Relay, Patchbay.
- **Thesis:** Two Nests seed long-lived sentries at centre approaches, then
  leave most bodies free to work outer Wells while `deployment-active` and
  remaining tenure say the lease still holds.
- **Why it earns a sheet:** Deployments substitute bounded time for body labor.
  The sheet's core decision is whether to preserve remote coverage or return
  and renew it before the next centre birth.
- **Failure/counterplay:** Clear a sentry immediately after its owner leaves,
  suppress it during the scoring window, or attack the outer force while Nests
  travel back to renew.

#### 30 — Demolition March

- **Roster:** Longshot x2; Mortar, Hush, Lantern, Sunder, Relay, Kestrel.
- **Thesis:** Treat hostile deployments as the first target of a premium raid:
  Lantern reveals, Hush suppresses, Longshots or Mortar clear, and only then
  does Relay enter the defended lane.
- **Why it earns a sheet:** It is a counter-sheet whose specialist labor has a
  concrete conversion into a 4-charge attempt. A builder opponent can rotate
  its construct and make the demolition line chase.
- **Failure/counterplay:** Fight with base verbs, place in another theater, or
  attack while the fragile demolition cast is aimed at objects rather than
  bodies.

### F. Hybrid, adaptive, and counter-meta doctrines

#### 31 — Crown Bait

- **Roster:** Relay x2; Palisade, Patchbay, Kestrel, Veil, Hush, Lantern.
- **Thesis:** Build a visible, high-vitality Palisade veteran at centre and
  give it just enough support to look like the strategic centerpiece. The two
  Relays and Kestrel use the attention to threaten a far 4-charge route.
- **Why it earns a sheet:** The ace is deliberately a decoy and tank, not the
  win condition. The opponent must decide whether the bounty is worth leaving
  the actual scorers.
- **Failure/counterplay:** Ignore the crown, attack the runners, and force the
  expensive veteran to win the objective rather than merely survive.

#### 32 — Bounty Hounds

- **Roster:** Sunder x2; Towline, Kestrel, Longshot, Lantern, Hush, Relay.
- **Thesis:** When a visible enemy reaches level 2, the hunting group elevates
  `veteran` above carrier and nearest targets, using paint, hook, and rail to
  erase the investment and claim its XP bounty.
- **Why it earns a sheet:** It attacks the opponent's long-horizon asset
  directly and makes veteran protection part of both sheets' strategy.
- **Failure/counterplay:** Hide or retreat the ace, present it as bait, or race
  safe score while the hounds abandon Wells for a non-scoring kill.

#### 33 — Scoreboard Gearbox

- **Roster:** Relay x2; Kestrel, Towline, Patchbay, Hush, Lantern, Mason.
- **Thesis:** When ahead in total public charge, preserve the own Well and
  collect safe 1s; when behind, release the home group and attack 4, with a
  centre fallback when neither side has a decisive threshold.
- **Why it earns a sheet:** Its risk posture changes with the game state rather
  than at a fixed tick. The same roster visibly shifts from protecting a lead
  to buying comeback variance.
- **Failure/counterplay:** Pressure during the travel-heavy gear change, or
  maintain a score state that keeps it oscillating between incomplete plans.

#### 34 — Ghost Fort Rotation

- **Roster:** Mason x2; Nest, Veil, Relay, Kestrel, Lantern, Hush.
- **Thesis:** Build a conspicuous durable fort on one outer route, show enough
  bodies to sell the commitment, then leave the constructs as a cheap threat
  while smoke and fast bodies rotate to centre or the opposite Well.
- **Why it earns a sheet:** Long deployment tenure creates a visible feint that
  outlasts the bodies who placed it. The opponent chooses whether the fort is
  an active plan or stranded capital.
- **Failure/counterplay:** Count bodies, ignore the static objects, and attack
  the rotation before it establishes new cover.

#### 35 — Three-Well Auction

- **Roster:** Kestrel x2; Relay, Towline, Lantern, Patchbay, Hush, Palisade.
- **Thesis:** Treat each announced birth as an auction scored by bank value,
  outstanding Core state, local enemy presence, and wave manpower. A two-body
  elastic reserve goes to the best current lot while fixed minimum crews keep
  the other two theaters from collapsing.
- **Why it earns a sheet:** It is breadth with explicit opportunity cost, not
  “everyone takes the nearest Core.” The opponent can overbid one Well to pull
  the reserve away from another.
- **Failure/counterplay:** Concentrate enough force that a two-body response is
  always late, or feint presence at one birth and immediately rotate.

#### 36 — Forked Finish

- **Roster:** Relay x2; Kestrel, Palisade, Patchbay, Towline, Veil, Lantern.
- **Thesis:** At 8 total charge, split into two simultaneous winning threats:
  one small group takes the nearby 1 while the main group threatens a far 4.
  At 7, pair a centre 2 threat with the far route instead.
- **Why it earns a sheet:** Score arithmetic creates a late-game fork rather
  than a single obvious final Core. The defender must allocate against two
  legal wins with different travel and escort requirements.
- **Failure/counterplay:** Intercept before the split, retain an elastic reserve,
  or force the attackers to regroup by dropping one threat's carrier.

## Why hundreds or thousands are plausible

The 36 briefs are authored points, not the limit. A sheet can select one
meaningful choice from several largely orthogonal axes:

| Axis | Conservative macro choices |
| --- | ---: |
| score posture / charge recipe | 8 |
| veteran doctrine | 6 |
| respawn-wave stance | 6 |
| deployment/logistics package | 6 |
| wildcard declaration, including no duplicate | 17 |

The raw product is `8 x 6 x 6 x 6 x 17 = 29,376` combinations before map
routes, upgrade paths, thresholds, role sizes, and target priorities. Many of
those combinations will be incoherent, dominated, or cosmetically equivalent;
the number is not a content promise. It does show that thousands do not require
thousands of mechanics. If the axes actually interact, a modest surviving
fraction already gives a four-digit macro-build space.

The important validation is not combinatorics but **interaction**. A promoted
variant should demonstrate all three of the following:

1. changing one axis changes at least one role's orders, not only its class;
2. the opponent's best response changes in replay; and
3. the choice creates a real sacrifice elsewhere on the map or clock.

If authored representatives converge back to nearest-Core collection despite
these differences, the count has failed and the rules need more work.

## First authoring tranche

Do not author all 36 before learning whether the primitives work. Start with
eight maximally separated representatives:

1. **03 Two Heists and a Tip** — pure 1/2/4 raid pressure;
2. **07 Turncoat Intercept** — source-relative theft and reversal;
3. **13 Crowned Rail** — fixed ace and explicit support;
4. **16 Emergent Standard** — match-created ace;
5. **20 Cutoff Execution** — offensive wave timing;
6. **23 Pre-Wave Evacuation** — defensive wave timing;
7. **25 Double Mason Lockbox** — durable territory ownership; and
8. **30 Demolition March** — direct answer to that ownership.

The first gate is compile/explain plus trace-first representative exhibits,
not a balance sweep. Only after the strategies visibly execute as written
should a separate owner-requested measurement ask whether they form a useful
counter-web, whether any wildcard pair is mandatory, and whether 1/2/4 makes
far raids too swingy.
