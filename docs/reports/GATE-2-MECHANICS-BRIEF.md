DECISION NEEDED: Approve the **Threefold** map and the Arc Relay Phase B numbers as the Phase C implementation hypothesis; default: approve unchanged, with every number below remaining provisional until rules-native play and the owner gallery.

RESULT: Arc Relay now has a buildable mechanics hypothesis rather than only a concept. Threefold is an exact 31×23, three-theater, mirror-fair map with 525 walkable tiles; eight bodies per side fight over at most three live Arc Cores. A 25-tick global production beat, three Cores per reactor Pulse, three Pulses to win, a 600-tick horizon, a 20-tick respawn delay, and bounded hull, handling, gun, vision, carrier, and signature values create the first testable ruleset. These are registered starting values, not balance findings, and no implementation or claim of fun has been made.

EVIDENCE:

# Gate 2 — Arc Relay mechanics brief

## 1. Scope and confidence

This report executes only Phase B of
`HANDOVER-CODEX-GAME-REDESIGN-2026-07-31.md`. Gate 1 already approved Arc
Relay, its no-economy / no-score-to-power stance, and its sixteen-class launch
band. This report therefore:

- authors the larger map;
- makes the shared Core, delivery, Pulse, lifecycle, combat, and information
  rules precise enough to implement;
- supplies the deferred Well cadence, Pulse thresholds, match horizon,
  respawn delay, statline bands, field size, and composition shape;
- gives first-pass answers and test triggers for all seven Gate 1 risks;
- preserves commander sheets, blind deterministic play, the participant-scoped
  mind, and Canvas2D as the required experimental renderer;
- stops before Phase C.

This is design evidence. There are no replays, distinct outcomes, artifact
hashes, dynamics tables, or galleries because the candidate does not exist in
code. Every value labelled **H0** is the primary registered hypothesis. The
named alternatives are future isolation arms, not an invitation to build a
factorial sweep or silently tune after seeing results.

## 2. The H0 ruleset at a glance

| Surface | H0 hypothesis | Why this is the first value, not a claim |
| --- | --- | --- |
| Match format | head-to-head; one participant mind and one scoring team per side | Matches the approved passive/blind commander layer and the implemented mind boundary. |
| Fielded bodies | 8 fixed unit slots per team, all live at tick 0 | Three Wells need meaningful acquire/protect/disrupt allocation; the unified eight-slot infrastructure already exists. |
| Composition | fill all 8 slots directly from the player's unlocked classes; each class may appear once or twice | The slot count and two-copy cap already bound composition. There is no redundant five-class stable filter at a 16-class roster. |
| Map | **Threefold**, generation 1, map format 3, 31×23, 525 open tiles | 2.25× the current 233-tile field while remaining inside the current 32×32 engine ceiling. |
| Wells | centre first at tick 25, north at 50, south at 75; each repeats every 75 ticks through tick 525 | Produces one public beat every 25 ticks (5 seconds at normal speed) while staggering allocation pressure. |
| Live-Core bound | at most one unresolved Core per Well; at most one pending charge per Well | Caps the central visual state at three Cores without deleting a contested Core. |
| Ordinary Core locomotion | a Core may change tiles through ordinary carrying, handoff, or forced displacement at most once every 2 ticks | Gives interception time and makes the limit travel with the object instead of favouring a chassis or pass chain. |
| Pulse | 3 delivered Cores fill a Pulse | Three pips are readable and one delivery matters without immediately damaging the reactor. |
| Victory | the third Pulse destroys the opposing reactor | Nine deliveries give repeated possession stories and three large score beats. |
| Horizon | 600 ticks = 120 seconds at the viewer's normal 5 ticks/second | Long enough for the 31×23 field and nine-delivery ceiling, still a bounded morning-report replay. |
| Respawn | `respawnDelayTicks = 20` at the body's fixed home anchor | A wipe creates four seconds plus travel of lost pressure, but every unit slot returns. |
| Power progression | none | Deliveries and Pulses grant score only: no scrap, purchases, tiers, reinforcement tranches, or score-derived combat modifier. |

## 3. Threefold: the authored larger map

### 3.1 Collision blueprint

Threefold's candidate identity is `arc-relay-threefold-01`. Coordinates are
zero-based, `#` is collision wall, and `.` is walkable floor. These are the
exact proposed tile rows, not a mood sketch:

```text
    0000000000111111111122222222223
    0123456789012345678901234567890
00  ###############################
01  #....#...................#....#
02  #.............................#
03  #....#....###.....###....#....#
04  #....#....###.....###....#....#
05  #....#....###.....###....#....#
06  #.............................#
07  #....#...................#....#
08  #....#...................#....#
09  #.............................#
10  #....#....###.....###....#....#
11  #....#....###.....###....#....#
12  #....#....###.....###....#....#
13  #.............................#
14  #....#...................#....#
15  #....#...................#....#
16  #.............................#
17  #....#....###.....###....#....#
18  #....#....###.....###....#....#
19  #....#....###.....###....#....#
20  #.............................#
21  #....#...................#....#
22  ###############################
```

The board is reflected exactly by `x -> 30 - x` for team fairness and by
`y -> 22 - y` for north/south neutrality. The repeated three-tile cover
islands create two equal approaches around each Well. The open vertical strips
between them are cross-theater connectors: a reserve can rotate, but doing so
still costs seven moves between adjacent Wells and fourteen between the outer
Wells.

### 3.2 Named gameplay facts

| Fact | West / neutral coordinates | East mirror or role |
| --- | --- | --- |
| North Well | `(15,4)` | neutral `well-north` objective region |
| Centre Well | `(15,11)` | neutral `well-centre` objective region |
| South Well | `(15,18)` | neutral `well-south` objective region |
| Reactor socket | west `(2,11)` | east `(28,11)` |
| Home pad | west `x=1..3, y=8..14` | east `x=27..29, y=8..14` |
| North approach gates | west `(5,2)`, `(5,6)` | east `(25,2)`, `(25,6)` |
| Centre approach gates | west `(5,9)`, `(5,13)` | east `(25,9)`, `(25,13)` |
| South approach gates | west `(5,16)`, `(5,20)` | east `(25,16)`, `(25,20)` |

The eight west spawn anchors, in unit-slot order, are `(1,8)`, `(2,8)`,
`(3,9)`, `(1,10)`, `(1,12)`, `(3,13)`, `(1,14)`, and `(2,14)`, all facing
east. East anchors are their exact x-reflections and face west. The sheet
assigns its eight declared classes to these fixed slots before the match; a
respawning slot returns to its own anchor.

The home pads carry `SpawnProtected`: opposing ground bodies cannot enter, but
the tag gives no damage immunity and stops no projectile. The sockets and
Wells carry a candidate `SignaturePlacementForbidden` tag so a Hardlight Block,
Trip Node, Sentinel, or future placed signature cannot replace, occupy, or seal
the match's required source and scoring tiles. Phase C may express that as one
new generic tile-tag kind or as equivalent Arc Relay mode validation; it must
be canonical contract data either way.

Map format 3 already supplies stable named spawns, regions, and tags. H0 uses
the existing generic `Objective` region kind for the three Wells and
participant region-role assignments for `own-home-pad` and `own-reactor`.
Phase C needs a new typed `ArcRelay` mode-map binding; it does not need a new
map format.

### 3.3 Scale and route checks

The structural calculations for the exact rows above are:

| Check | Threefold | Comparison / consequence |
| --- | ---: | --- |
| Total tiles | 713 | current Frontline Labs field: 345 |
| Walkable tiles | **525** | current legion field: **233**; Threefold is **2.25×** larger |
| Opening bodies | 16 | eight per team, no reinforcement tranche |
| Open tiles per body | **32.8** | current legion endgame: 13.7; original six-body tuning field: 38.8 |
| Shortest home-pad edge to any Well | 12 moves | equal for all three theaters and both teams |
| Shortest Well to own reactor | 13 moves | equal for all Wells and both teams |
| Ordinary carried travel floor, Well to reactor | **26 ticks** | Core relocation recovery doubles the geometric floor before combat, blocks, and handoffs |
| Adjacent-Well distance | 7 moves | a reserve can rotate; a whole convoy cannot be everywhere at once |
| Outer-Well distance | 14 moves | north and south are genuinely separated theaters |

The six gates into each home basin are the anti-camp geometry. Each theater has
two direct return choices, and the connector strips allow a carrier to switch
to another pair after revealing its initial line. No single body or short
projectile lane can occupy all six gates. Cover never creates a one-tile
mandatory corridor, so Palisade, Mason, Minesmith, and Nest tax a choice rather
than delete it.

### 3.4 Sheet-space map grammar

Threefold is authored for saved, blind sheets rather than live drawing. A
sheet may name:

- one outbound path and up to two return paths per Well;
- opening groups for north, centre, south, and reserve;
- handoff/catch points and escort rally lines;
- ambush, hold, avoid, smoke, mine, and Hardlight zones;
- a cross-theater reserve route and a fallback reactor gate;
- ordered gambits that switch among those already drawn plans from public and
  observed state.

Drawings are coordinates and waypoint lists interpreted by the stock mind.
They do not change collision, command bodies live, or create engine-owned
formation rules. Threefold's repeated geometry gives the stock mind equivalent
north/centre/south vocabulary without making the paths interchangeable.

## 4. Core mechanics, made exact

### 4.1 Tick-order commitments

Phase C must preserve the platform's frozen-observation and deterministic
chronology. Arc Relay adds these relative-order commitments to the existing
generic tick phases:

1. Due respawns, signature completions, Well rearming, and scheduled Core
   births occur at tick start before observations are frozen.
2. A body already standing on a Well when its Core is born picks it up before
   observation. Otherwise, an unoccupied birth is observed as a loose Core.
3. Minds decide once from the frozen state. Movement conflicts retain the
   generic session's canonical simultaneous resolution.
4. A body ending movement on a loose Core picks it up. A Core picked up through
   movement cannot be handed off on that same tick.
5. Signature movement/displacement and their published tells resolve in their
   declared phases. Any Core relocation updates the Core-owned recovery clock.
6. Drop and adjacent handoff resolve after movement. A handoff requires the
   named adjacent receiver to remain in place and submit `Wait`; it consumes
   the source's action and the receiver's otherwise unused tick.
7. Projectiles, signature damage, destruction, and destruction-caused Core
   drops resolve before banking.
8. A surviving carrier on its own reactor socket banks after combat. Thus a
   shot landing on the arrival tick can still force a visible last-tile drop.
9. Deliveries fill charge, Pulses resolve, then terminal mode-objective and
   horizon checks follow the platform's existing completion precedence.

This order prevents invisible “the UI showed a hit, but the Core had already
scored” outcomes. Phase C should reuse the existing sixteen generic phases and
insert Arc Relay kernels at the matching boundaries, not build a parallel
session chronology.

### 4.2 Well production and the live-Core bound

The public H0 schedule is:

| Well | Scheduled birth ticks |
| --- | --- |
| Centre | `25, 100, 175, 250, 325, 400, 475` |
| North | `50, 125, 200, 275, 350, 425, 500` |
| South | `75, 150, 225, 300, 375, 450, 525` |

This is a 75-tick cadence per Well and one arena-wide scheduled beat every 25
ticks. The first centre birth lands after a shortest opening route can arrive;
the final south birth leaves 75 ticks before the 600-tick horizon.

Each Core carries a stable identity `(sourceWellId, sourceOrdinal)`. A Well may
own at most one unresolved Core. If another scheduled beat occurs while that
Core is loose, carried, or in flight, the Well stores one visible **pending
charge**; further missed beats do not stack. After the unresolved Core banks,
a pending Well shows a 10-tick rearm ring and births on completion. If no Core
is outstanding, the scheduled beat births immediately.

Consequences are intentional:

- no contested object expires, teleports home, or disappears to make room;
- no more than three Cores can be live, one with each source glyph;
- stalling a Core delays that source but never accumulates a burst of several
  hidden births;
- all countdown, outstanding, pending, and rearm state is public from tick 0.

### 4.3 Core possession and relocation

A body carries zero or one Core. Cores do not block a floor tile, projectile,
or line of sight. A loose Core is neutral and may be picked up by either team.

The Core, not the body, owns `nextRelocationTick`:

- movement while carrying, an adjacent handoff, a forced pull/push of the
  carrier, and an Arc Toss landing all change the Core's tile;
- after such a change on tick `T`, another Core relocation is legal first on
  tick `T + 2`;
- pickup changes possession without changing location but still starts the
  same two-tick Core recovery; drop keeps the existing recovery and can never
  shorten it; the body still spent the action or movement that caused it;
- handing off never resets or shortens the clock;
- a Core can therefore never cross an ordinary chain on consecutive ticks;
  Relay's telegraphed Arc Toss is the one bounded long-transfer exception.

A carrier may turn, wait, drop, or use a legal non-movement signature while the
Core recovers, but may not move the Core. It cannot use its basic gun. Vector
Dash and Exchange explicitly drop an owned Core on the departure tile before
their body movement. Physical external displacement from Tractor Hook or
Kinetic Burst moves the carrier and Core together and starts the same Core
recovery; that is bounded, public formation control, not a hidden score
teleport.

`drop` consumes the carrier's action and leaves the Core neutral on its current
tile. Destruction does the same without consuming a future action. Old-life
projectiles keep their ordinary causality; a dead shooter's later hit may still
destroy a carrier and cause a drop.

### 4.4 Adjacent handoff

`handoff(targetUnitId)` is a shared objective action, not a collision side
effect:

- source has a Core ready to relocate;
- target is a live ally on an adjacent tile at tick start and remains there;
- target carries no Core and submitted `Wait`;
- both bodies are still live when handoff resolves;
- that Core has not changed possession or tile earlier this tick.

Success transfers the Core to the target and starts the two-tick relocation
clock. Failure is `Blocked`, moves nothing, and remains visible in the replay.
The receiver commitment makes relay formations authored set-pieces rather than
an instantaneous bucket brigade.

### 4.5 Delivery, Pulse, and completion

The west and east reactor sockets are one-tile participant-bound regions. A
surviving carrier ending the tick on its own socket banks immediately. Banking
removes that Core, increments the team's charge by one, and starts any pending
source-Well rearm on the next tick.

At three charge pips:

1. charge resets to zero;
2. one segment is removed from the opposing reactor's three-segment integrity;
3. a broad Pulse is rendered across the arena;
4. **nothing else happens**: no damage, heal, push, cooldown reset, respawn
   acceleration, stat increase, or Well ownership.

The third Pulse destroys the opposing reactor and ends the match. If both
teams deliver a third Pulse on the same tick, both reactors are destroyed and
the result is a draw; canonical participant order never decides a symmetric
score race.

At the 600-tick horizon, rank by:

1. more opposing reactor segments removed;
2. then more current charge pips;
3. otherwise draw.

Loose Cores, carried Cores, distance-to-socket, damage, surviving hull, and
possession time are deliberately not tiebreakers. They are pressure toward a
future delivery, not banked score.

### 4.6 Lifecycle and spawn safety

All eight fixed unit slots are live at tick 0. There are no mid-match slot unlocks,
fabrication, body purchases, or score-funded reinforcements in H0. Destruction
on tick `D` queues the same slot at its fixed anchor with
`respawnDelayTicks = 20`; under the existing lifecycle convention the new life
may act on `D + 1 + 20`.

Respawn creates a fresh life and runtime-local life state while the
participant mind remains match-long. It returns with its declared class and
full hull. The protected home pad prevents an opponent from occupying the
anchor, not from firing into it. A wipe therefore gives the opponent roughly
four seconds plus outward travel, but never removes the mind or grants the
opponent permanent power.

## 5. Registered numeric hypotheses

### 5.1 Primary pacing registry

| Family | H0 | Named alternatives | What moves it away from H0 |
| --- | ---: | --- | --- |
| Per-Well cadence | **75 ticks**; offsets 25/50/75 | `hot-60` (global beat 20), `spacious-90` (global beat 30) | Deathballs despite overlap -> 60; unreadable unresolved pressure -> 90. |
| Pending rearm | **10 ticks** | `rearm-5`, `rearm-15` | A queued second birth feels like an ambush -> 15; source downtime stalls play -> 5. |
| Ordinary Core relocation interval | **2 ticks** | `free-carry-1`, `heavy-carry-3` | Carrier catches never form -> 3; routes feel laborious despite active counters -> 1. |
| Cores per Pulse | **3** | `pulse-charge-2` | Healthy possession play but too few large score beats before horizon. |
| Pulses to destroy reactor | **3** | `reactor-2`, `reactor-4` | Replay is structurally good but ends consistently too late or too early. |
| Horizon | **600 ticks** | `horizon-500`, `horizon-700` | Use only after cadence/threshold attribution; duration alone is not a fun score. |
| Respawn delay | **20 ticks** | `return-16`, `return-24` | Wipes give no delivery window -> 24; one wipe predicts the game -> 16. |
| Active Core cap | **3, one per Well** | no larger H0 arm | Raise only if galleries prove three-object state legible and play still has objective drought; never as a pacing shortcut. |

The first build should expose these through immutable `GameRules` and map/mode
definitions, with H0 carrying one experimental identity. Alternatives are
isolated only after the H0 failure picture is observed. A changed number mints
a distinct ruleset/map fingerprint; it is never relabelled as H0.

### 5.2 Field and composition registry

| Family | H0 | Registered alternative | Failure trigger |
| --- | --- | --- | --- |
| Unit slots | **8 per team** | `company-6` | Viewer density or mind authoring is confused even with the Core cap and role tags. |
| Class source | **any currently unlocked launch class; no match-stable pre-filter** | `roster-25-stable` only after the full roster grows past roughly 25 | The unlock set already constrains early players and eight slots constrain a sheet. Revisit a stable only when roster-scale browsing/authoring becomes the measured problem. |
| Duplicate bound | **1–2 copies of any fielded class** | one copy / free duplicates | One-copy if signature stacking dominates; free duplicates only for a deliberate later unrestricted playlist, never by accident. The H0 cap implies 4–8 distinct classes. |
| Opening deployment | **all 8 live** | phased `4+4` | Only if opening allocation is unreadable or produces deterministic first-contact congestion. No economy or score condition may unlock phase two. |

An early account must have enough breadth to fill eight legal slots under the
two-copy cap; its unlocked set is the candidate list. A separate stable is
registered only as (a) the `roster-25-stable` scale response if the seasonal
roster grows beyond roughly 25 and direct selection becomes unwieldy, or (b)
an explicit draft/tournament format rule. Neither belongs in ordinary H0 sheet
structure.

## 6. Statline bands

### 6.1 Shared combat and information floor

Every body receives one action per tick. Ordinary movement is one adjacent
eight-way tile; bodies never gain two-tile passive movement. A basic shot
travels along one of eight headings, advances two ordered tiles per tick, deals
one hull on first hostile-body contact, and stops on bodies or terrain unless a
signature explicitly says otherwise.

Normal vision is a wall-occluded facing quadrant to range 7 plus omnidirectional
adjacency. Strict corners block sight. A mind receives the frozen union of its
team's current sensors and retains only the memory its own deterministic code
stores. Well schedule/state and reactor integrity/charge are public; a Core's
current tile or carrier follows normal team visibility. The omniscient replay
viewer may show every Core, while selected-mind fog remains available for
forensics.

### 6.2 Exact bands

| Band | H0 value | Meaning on screen and in play |
| --- | --- | --- |
| Light hull | **3** | three ordinary hits |
| Standard hull | **4** | four ordinary hits |
| Heavy hull | **5** | five ordinary hits |
| Swift handling | move to any adjacent legal tile and face the movement heading | reorients while travelling; it does **not** move farther per tick |
| Standard handling | move to any adjacent legal tile while preserving facing | strafes, but changing aim uses the shared turn action |
| Deliberate handling | move only along current facing; turning is a separate action | route commitment for artillery/heavy geometry, using the existing facing-locked grammar |
| Short-fast gun | range **4**, next fire at `T + 2` | escort and close-control pressure |
| Medium-steady gun | range **6**, next fire at `T + 3` | general formation pressure |
| Long-slow gun | range **9**, next fire at `T + 5` | corridor pressure; exceeds own vision and rewards allied spotting |

The names “swift / standard / deliberate” describe **handling**, not passive
travel speed. Every carrier has the same Core-owned relocation clock. That is
the first answer to the swift-carrier monopoly risk: Kestrel is a rapid
interceptor because of Vector Dash, not because its chassis silently scores
faster.

### 6.3 Launch-band projection

Gate 1's approved assignments become these exact shared profiles:

| Class | Hull | Handling | Basic gun |
| --- | ---: | --- | --- |
| Kestrel | 3 | swift | short-fast |
| Palisade | 5 | deliberate | short-fast |
| Towline | 4 | standard | medium-steady |
| Patchbay | 4 | standard | short-fast |
| Lantern | 3 | swift | short-fast |
| Mortar | 3 | deliberate | medium-steady |
| Minesmith | 4 | standard | short-fast |
| Hush | 4 | standard | medium-steady |
| Relay | 4 | swift | short-fast |
| Switchback | 3 | standard | medium-steady |
| Longshot | 3 | deliberate | long-slow |
| Mason | 5 | deliberate | short-fast |
| Sunder | 4 | standard | medium-steady |
| Repulsor | 5 | standard | short-fast |
| Veil | 3 | swift | short-fast |
| Nest | 4 | deliberate | medium-steady |

No class has a hidden resistance, score weight, pickup priority, carrier
speed, passive aura, or second skill.

## 7. First implementation envelope for the sixteen signatures

These values make the approved signatures executable for the first native
cohort. They are H0 tuning, not sixteen separately approved balance verdicts.
“Cooldown T+N” means the next start is legal first at that absolute tick; each
tell and active state is public and replayed.

| Class / one signature | H0 envelope |
| --- | --- |
| Kestrel — **Vector Dash** | 1-tick straight arrow tell; surge up to 4 tiles, stopping before first block; carried Core drops before the surge; cooldown `T+12` from completion. |
| Palisade — **Prism Wall** | Place three contiguous projectile-blocking edge segments beside Palisade for up to 8 ticks or 3 projectile contacts; bodies pass through; one wall per Palisade; cooldown `T+16` from placement. |
| Towline — **Tractor Hook** | Straight range 6; first body hit pulls up to 3 legal tiles toward Towline, stopping before terrain/body; a carried Core follows and takes Core recovery; cooldown `T+12`. |
| Patchbay — **Repair Beam** | Range 4 channel; restore 1 hull after each 2 uninterrupted channel ticks, maximum 2 hull per activation; movement, hostile damage, lost sight, full target, or target change breaks it; cooldown `T+10` from end. |
| Lantern — **Survey Flare** | Target within 8; visible arcing travel at 2 tiles/tick; reveal a Chebyshev-radius-4 area through smoke for 8 ticks; cooldown `T+16` from launch. |
| Mortar — **Falling Star** | Target visible tile within 8; 2-tick floor reticle; damage 1 on centre plus four cardinal adjacent tiles over walls; cooldown `T+12` from launch. |
| Minesmith — **Trip Node** | Place adjacent on legal floor; one node per Minesmith; hull 1, trigger damage 2, enemy proximity reveal at range 2, basic fire can destroy; cooldown `T+12` from placement. |
| Hush — **Null Field** | Chebyshev radius 3 for 5 ticks; hostile signatures cannot start, maintained signatures end, and hostile signature constructs inside are visibly suppressed rather than destroyed; base verbs remain legal; cooldown `T+18`. |
| Relay — **Arc Toss** | Carrier names a straight landing tile within 5; 1-tick landing tell, then Core flight at 2 tiles/tick; walls stop it on the preceding tile; an ally present catches, otherwise neutral drop; landing starts Core recovery; cooldown `T+12` from launch. |
| Switchback — **Exchange** | Visible ally within 6; both endpoints hold through a 1-tick tell and target submits `Wait`; positions exchange if both remain legal; targeted carrier drops before exchange; cooldown `T+16`. |
| Longshot — **Rail Line** | Fixed heading, 2-tick charge, range 12, damage 2 through every body until terrain; hostile damage during charge cancels and applies an 8-tick half-cooldown; successful fire cooldown `T+18`. |
| Mason — **Hardlight Block** | Adjacent legal floor; hull 3, lifetime 12 ticks, one block per Mason, second placement replaces first; cannot occupy placement-forbidden tiles; cooldown `T+14`. |
| Sunder — **Target Paint** | Visible enemy within 7; lasts 8 ticks or 3 allied basic-projectile hits; each of those hits deals +1 hull and removes one public mark segment; cooldown `T+16`. |
| Repulsor — **Kinetic Burst** | 1-tick contracting-ring tell; push every adjacent body one legal tile directly away, blocked bodies stay without substitute damage; carried Cores follow and take recovery; cooldown `T+12`. |
| Veil — **Smoke Canister** | Target within 6; Chebyshev-radius-2 field for 10 ticks, clipped by terrain; blocks normal sight and acquisition through it, adjacency still sees, Survey Flare reveals; cooldown `T+18`. |
| Nest — **Sentinel Seed** | Deploy adjacent; one sentry per Nest, hull 2, range 4, damage 1, fire cooldown 3, deterministic nearest-visible target with canonical identity tiebreak; lifetime 30 ticks or until destroyed/replaced; cooldown `T+18`. |

Signature-created constructs never carry, pick up, hand off, bank, provide
objective weight, reserve spawn tiles, or count as stable bodies. Hush
suppression publishes a crossed-out state and pauses their action while
overlapped; health is unchanged and the ordinary lifetime continues to elapse.
These common rules prevent every placed signature from inventing its own
objective semantics.

## 8. Where the decisions and visible drama come from

### 8.1 Decisions per minute at sheet level

At normal playback, H0 schedules twelve arena-wide Well beats per minute. Not
every beat produces immediately because each source is capacity-one, but every
beat changes a public countdown or pending state. Between them, the mind must
continually choose:

1. which of three theater groups or the reserve receives the next body;
2. whether to contest a birth, ambush its exit, or concede and prepare the
   next staggered Well;
3. who carries, who gives up a tick to receive, and where the next catch point
   is;
4. which of two direct gates or one cross-theater detour the carrier uses;
5. whether an escort protects the carrier or peels off to deny the next Core;
6. whether an interceptor chases the visible threat or preserves position for
   a pending birth;
7. which one-signature cooldown is spent now and which is reserved for the
   next delivery attempt;
8. which ordered gambit overrides the base allocation after a Pulse, a double
   enemy possession, a wipe, or a route failure.

The one-action receiver commitment, carrier recovery tick, and public
signature tells create decisions even along a chosen route. The plan is not
“go to marker”; it is a sequence of allocations and exposed commitments.

### 8.2 What the spectator reads

Canvas2D is part of H0, not later polish:

- the three Wells use distinct source glyphs (north triangle, centre ring,
  south diamond), countdown arcs, pending pips, and 10-tick rearm rings;
- every Core retains its source glyph; loose is neutral white, carried gains
  a team-colour beam and ribbon, recovery greys one motion notch;
- handoff shows both committed bodies, then one short transfer arc;
- Arc Toss shows its landing tile before the Core enters a visible flight;
- drops remove team colour immediately and use the source glyph's crack;
- reactor HUDs show three charge pips inside three integrity segments;
- a Pulse crosses the whole field but has no gameplay impact animation on
  bodies, preventing a false power read;
- zoomed overview overlays prioritise the Core closest in route distance to a
  reactor, but never hide the other two;
- every class signature keeps Gate 1's tell -> active shape -> ending grammar,
  and body role tags expose `carrier`, `screen`, `intercept`, and `reserve`
  when the mind elects to publish them.

The intended repeated replay sentence is: **birth announced -> allocation
splits -> possession forms -> route commits -> counter appears -> handoff or
drop reverses it -> surviving carrier delivers -> Pulse changes the score.**
Only a later owner gallery can establish that the sentence is enjoyable.

## 9. First-pass answers to Gate 1's seven risks

| Gate 1 risk | H0 design answer | Evidence to collect / registered failure trigger |
| --- | --- | --- |
| **One convoy is always correct** | Wells overlap in time: one beat every 25 ticks while ordinary minimum Well-to-reactor carriage is 26 ticks. Three capacity-one sources stay spatially separated, so a convoy completing one route concedes at least one other allocation beat. | Report bodies per theater, concentration around each carrier, uncontested births, source-specific pickup/delivery share, and win rate by allocation entropy. If low-split doctrines dominate every independent lineage, isolate `hot-60` before adding a mechanic. |
| **Home camping dominates** | Six spread gates enter each protected home basin; three cover-separated return theaters and connectors let a carrier change approach. Campers earn no Core, power, or score, and cannot enter the socket/pad. | Report opponent-final-third body-ticks, kills/drops by distance to reactor, camp-to-delivery conversion, and counter-deliveries conceded. If camping predicts wins, first alter gate/cover geometry as a new map fingerprint; do not add turret immunity. |
| **Passing erases travel** | Handoff consumes the source action and an adjacent receiver's `Wait`; the Core-owned two-tick relocation clock survives possession changes. One Core cannot transfer twice in a tick or move on consecutive ordinary ticks. | Hard invariants verify relocation chronology. Report handoffs per delivery, Core path length versus geometric distance, and Arc Toss share. Any illegal fast chain is an implementation failure, not balance. |
| **Swift chassis monopolize carrying** | Swift is handling, not speed. Every chassis moves one tile and every Core owns the same relocation recovery. Kestrel drops before Dash; Relay's bounded, public Toss is its approved objective signature rather than a passive speed bonus. | Report carry ticks, pickups, handoffs, and deliveries by class against field availability. If one non-Relay class carries far above availability across lineages, inspect gun/hull/handling opportunity cost before adding a carrier modifier. |
| **Safe score snowballs** | A delivery fills score only; Pulse is visual plus reactor damage and grants no combat, spawn, cooldown, vision, or production benefit. Outstanding Cores remain. A leading team must expose a carrier again for every later pip. | Report first-Pulse winner share, behind-to-ahead Pulse reversals, possession steals after first Pulse, and combat-state deltas across Pulse ticks. If first-Pulse conversion exceeds 70% in a diverse native cohort, diagnose map/respawn/cadence before considering any comeback rule. |
| **Too many simultaneous Cores confuse** | One unresolved Core per source hard-caps live state at three. Each has a persistent source glyph; Wells expose outstanding/pending state; overview threat ordering emphasises without hiding. | Owner's outcome-blind gallery is authoritative. Also report live-Core occupancy and time with 1/2/3 Cores. Any replay where the owner cannot identify carrier/source/near-bank threat returns to presentation or cadence, not a numeric “fun” pass. |
| **Matches run long** | H0 supplies 21 scheduled beats, bounded pending state, a 600-tick horizon, a four-second respawn, last birth at 525, and `3 × 3` visible progress. Timeout uses only banked achievement. | Report median/p90 seconds, MaxTicks and draw rates, delivery intervals, unresolved-Core age, drops per delivery, and time from final birth to score. Starting alert: >20% MaxTicks or >10% draws with otherwise active play; isolate cadence/threshold/horizon/respawn in that order before inventing a rule. |

The 70% first-Pulse and 20% MaxTicks values are diagnostic starting alerts,
not retroactive ship gates. Safety, replay integrity, and the later frozen
product scorecard remain separate.

## 10. Contract and presentation shape for Phase C

This is an implementation boundary inventory, not implementation:

### 10.1 Engine and immutable contract

- Add one typed `ArcRelay` game mode and `ArcRelayModeMapBindingDefinition`
  binding ordered Well region IDs and per-participant reactor/home roles.
- Put every H0 value in immutable rules/map/topology data; never in session,
  bot, viewer, or CLI constants.
- Add canonical Core, Well, reactor, Pulse, and pending/rearm state plus stable
  Core IDs and events.
- Add shared `handoff` and `drop` actions and the sixteen class-bound signature
  action/form/construct definitions. Pickup and banking remain world
  interactions, not magic bot decisions.
- Reuse participant-scoped mind execution, eight fixed unit slots, fixed
  per-slot `classId`, fresh lives on respawn, exact keyed decisions, and the
  generic chronology.
- Mint a new experimental rules identity, topology profile, map ID, mode
  binding kind, and replay fingerprints. Frozen Duel and Frontline generations
  remain byte-exact.

### 10.2 SDK and observations

The mind needs exact public contract facts before tick 0 and exact current
facts per tick:

- Well schedule, capacity, outstanding Core ID, pending flag, and rearm tick;
- public reactor charge/integrity by scoring team;
- own and currently visible Core location, source, possession, carrier, flight,
  and `nextRelocationTick`;
- legal target/range/cooldown state for handoff, drop, and the body's one
  signature;
- eight slot records with fixed class IDs and lifecycle state;
- signature constructs/effects subject to ordinary visibility, plus public
  telegraphs where their counterplay requires it.

No observation reveals a hidden enemy carrier merely because it holds a Core.
The omniscient broadcast replay is a presentation surface, not a bot
capability.

### 10.3 Replay and required 2D presentation

Replay 3 must carry mind decisions and authoritative Core/Well/reactor state,
then validate every pickup, relocation lock, handoff receiver, drop, bank,
charge reset, Pulse, and simultaneous terminal result. `replay --summary` and
dynamics tooling need Arc Relay outcomes and mechanic counts.

Canvas2D ships with the mechanic: map landmarks, Core lifecycle, Wells,
reactors, Pulses, signature tells/effects, cooldown reads, and role tags. The
parked 3D viewer only has to continue compiling and consume the version-neutral
replay fallback; Phase C must not extend it per Arc Relay mechanic. The root
renderer guidance in `CLAUDE.md` is amended when the first presentation change
lands, as the handover requires—not in this documentation-only gate.

## 11. Evaluation plan after an approved build

Arc Relay is a substantial change to objective, actions, observations, map,
combat timing, and survival pacing. Historical Frontline bots are only
infrastructure sentinels. The first product read requires Arc-Relay-native
minds.

### 11.1 Population and study roles

1. Build deterministic mechanic instruments for every shared Core transition
   and each signature's advertised counterplay.
2. Commission at least four independent Arc-Relay-native doctrines with equal
   authoring budgets: split-control, convoy, interception, and information/
   route-control are starting coverage cells, not forced solutions.
3. Build stock mind v0 around sheets and Threefold drawings; freeze it for the
   commander depth audit rather than retuning it per sheet.
4. Use same-cohort arms only for one diagnosed H0 factor. Native H0 round-robin
   and blind replay viewing remain the product evidence.
5. Freeze WASM hashes, map/rules/topology fingerprints, common-randomness
   profile, and the outcome-blind sample before opening aggregate outcomes.

### 11.2 Arc Relay scorecard additions

In addition to the shared outcomes, faults, damage, motion, entropy, stall,
loop, and duration metrics, report:

- scheduled/actual births, pending/rearm time, and live-Core-count histogram;
- pickups by source/team/class and contested pickup attempts;
- possession time, carrier changes, voluntary drops, death drops, steals,
  adjacent handoffs, Arc Tosses, and forced carrier displacement;
- Core tile-relocation intervals and impossible-chain verification;
- route length/efficiency, gate choice, cross-theater rotation, and unresolved
  Core age;
- deliveries, pips, Pulses, Pulse lead changes, first-Pulse conversion, and
  behind-to-ahead reversals;
- bodies and combat by theater, convoy concentration, home-camp body-ticks,
  interceptions, and escort survival;
- signature attempts, completions, counters, useful effects, and stacking by
  class/composition.

Before aggregate results, select at least twelve header-only, pair- and
assignment-balanced replays. The owner watches at normal speed and records
legibility, tension, action/counteraction, repetition, and whether each Pulse
and ending felt earned. A separate 3–5 highlight gallery demonstrates the
ceiling only.

### 11.3 Commander depth audit

With stock mind v0 held fixed, enumerate sheets across:

- diverse eight-slot compositions drawn directly from each player's unlocked
  classes, with the two-copy cap held fixed;
- opening north/centre/south/reserve allocations;
- fast versus safe return paths and handoff points;
- escort/intercept/carrier policies;
- static versus ordered-gambit variants.

Read the payoff matrix for dominance, cycles, and sensitivity. The sharpest
question remains the handover's: do gambit-bearing sheets beat static sheets?
That study cannot prove fun, but it can falsify the commander layer if sheet
choices do not move outcomes or collapse to one dominant plan.

## 12. Phase B completion audit

| Requirement | Authoritative evidence in this report | Status |
| --- | --- | --- |
| `DECISION NEEDED` first; then RESULT, EVIDENCE, NEXT | Literal report structure | Met |
| Larger authored map | Exact 31×23 rows, landmarks, spawns, tags, regions, routes, and density in Section 3 | Met as design; unimplemented |
| Well cadence | Exact 25/50/75 offsets, 75-tick per-source lists, pending/rearm rule, and alternatives in Sections 4.2 and 5.1 | Met as registered H0 |
| Pulse thresholds | Three deliveries per Pulse and three Pulses per reactor in Sections 4.5 and 5.1 | Met as registered H0 |
| Match horizon | 600 ticks with exact timeout ranking and alternatives in Sections 4.5 and 5.1 | Met as registered H0 |
| Respawn delay | 20-tick field and exact `D + 1 + 20` lifecycle consequence in Section 4.6 | Met as registered H0 |
| Statline bands | Exact hull, handling, gun, vision, projectile, class projection, and carrier rules in Section 6 | Met as registered H0 |
| Field size / composition deferred by Gate 1 | Eight slots, direct unlocked-roster selection, duplicate bound, all-live opening, and the scale-triggered stable alternative in Sections 2 and 5.2 | Met as registered H0 |
| First-pass answers to seven Gate 1 §3.9 risks | One exact row per named risk, with rule answer, metrics, and trigger in Section 9 | Met |
| Decisions per minute and legible watch drama | Sheet decisions and Canvas2D grammar in Section 8 | Met as design; fun unproven |
| Commander layer retained | Composition, paths, zones, rally lines, policies, gambits, stock mind, and depth audit in Sections 3.4, 8, and 11.3; the redundant 16-class stable filter is intentionally absent | Met with owner steering |
| Determinism / mind / 2D / frozen generations | Chronology and Phase C boundaries in Sections 4 and 10 | Met as design constraint |
| No implementation | Only this report is created; no engine, SDK, renderer, map package, ruleset, or test is changed | Met |

NEXT: none without the owner ruling. If approved, Phase C may implement H0 behind a new experimental Arc Relay identity with Canvas2D presentation in the same change. If changed, revise this brief before code. Do not run balance evidence, author stock mind v0, or begin implementation from an unratified Phase B foundation.
