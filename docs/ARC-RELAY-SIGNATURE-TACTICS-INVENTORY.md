# Arc Relay signature skills in map-first Tactics

**Date:** 2026-08-21  
**Status:** design inventory plus implemented Prism Screen outcome
**Rules baseline:** promoted `tapline-12` (`vision 5`, short/medium/long guns `3/4/6`, swift/standard/deliberate movement `1/1`, `4/5`, `2/3`)

## Verdict

The sixteen signatures are a good source of tactical depth, but the current
Tactics surface does not yet let a player use that depth reliably. The main
problem is not a shortage of skills. It is that automatic uses are broad,
authored uses are too weakly constrained, and ordinary Fight leaves are
materialized before signature leaves. A skill can therefore be invisible in
the editor, fire for the wrong reason, or be starved by the combat behavior it
was meant to shape.

Promote one small, consistent player model:

- every bot has one phase-local skill policy: **Smart**, **Prefer marks**,
  **Only at marks**, or **Off**;
- marks are drawn on the same map as posts and routes and are stored by **bot
  slot**, not by class;
- the authoring catalog declares each skill's mark shapes, target choices,
  safe smart predicate, commitment behavior, and place in the ordinary
  Instruction list;
- the Tactic compiler still emits only ordinary, top-to-bottom Instructions;
  there is no skill scheduler, reservation system, or second priority model;
- an illegal or unavailable use falls through in the same tick;
- active tells/channels that must be preserved get one ordinary
  `signature-commitment-active -> current-position` continuation above normal
  actions.

The moving frontal shield is a strong fit for **Palisade** and is now promoted
as Prism Screen in Tapline-12. It does not replace **Mason**'s Hardlight Block:
keeping a mobile screen and a static route-bending block creates two distinct
strategic jobs. The first tuning point is `30` shield hull, `6` active ticks
and `18` cooldown ticks. These values are acceptance-proven mechanics, not a
claim that population balance is already settled.

## What is authoritative today

The current product line is `ArcRelayLoopProfile.Tapline12` and is selected by
`ArcRelayLoopProfile.Current` in
`src/BotArena.Engine/ArcRelayLoopProfile.cs`. It keeps Tapline-10's map,
economy, grammar-3 signatures, perception and guns while making chassis rates
explicit under one movement control model:

| Shared rule | Current value |
| --- | ---: |
| Facing-quadrant body vision | 5 |
| Omnidirectional proximity vision | 0 |
| Swift / standard / deliberate movement | 1/1 · 4/5 · 2/3 |
| Facing coupling | Combat Strafe for every chassis |
| Short / medium / long basic gun range | 3 / 4 / 6 |
| Basic hit damage | 10 |
| Short / medium / long fire interval | 2 / 3 / 5 ticks |
| Basic gun windup | 1 tick |
| Rear-arc multiplier | 2x, before veteran bonus |
| Durable deployment mode | on |

All bodies still receive one decision opportunity per tick, but accepted
one-tile movement is cadence-gated: swift moves every tick, standard moves on
four of every five ticks, and deliberate moves on two of every three. Closed
movement opportunities do not bank. Every chassis now uses Combat Strafe, so
signature Smart behavior may rely on the public cadence while still treating
each accepted move as exactly one adjacent tile.

The current launch hulls are `30`, `40`, or `50`; repair, damage and deployment
hull values are in the same 10x magnitude. Signature numbers come from
`ArcRelayH0Definition.cs:964-1207` and `:1210-1275`; lifecycle mechanics come
from `ArcRelaySignatureBehaviors.cs` and `ArcRelaySignatureRuntime.cs`.

## Complete class and signature inventory

“Current Tactics” refers to `ArcRelayTacticsDsl` and
`ArcRelayTacticsMaterializer`, not to the older Program editor reports.

| Class · hull · handling · gun | Authoritative signature now | Current Tactics behavior and problem | Recommended Smart default and map controls | Balance/usability verdict |
| --- | --- | --- | --- | --- |
| **Kestrel** · 30 · swift · short 3 | **Vector Dash:** 1-tick tell, up to 4 tiles along an eight-way ray, cooldown 12 at completion. It takes the furthest legal tile and drops a carried Core on departure. | A drawn line compiles to route-forward Dash with minimum gain 2. Without a line, combat posture may call it at the nearest enemy. The marked leaf does not require empty hands, so a `keep` custody phase can accidentally drop a Core. | Smart: empty-handed, following the phase's actual route, and at least 2 safe forward tiles exist. Do not Dash automatically from a fixed post. **Prefer/Only** lets the player paint route segments; show start/end and landing preview. An advanced `Mobile flank` fight preset may Dash only inside the active fight geometry and must return to its route/post. | Mechanic is good. Add the empty-handed guard, route-bound smart predicate, and commitment continuation. Never expose unrestricted “dash toward enemy” as the default. |
| **Palisade** · 50 · deliberate · short 3 | **Prism Screen:** immediate three-segment, 30-hull screen one tile ahead, duration 6, cooldown 18 from start. It follows Palisade's position/facing, passes allied fire, blocks hostile projectiles, does not block bodies, and ends on owner death. A body-occupied segment is inactive for collision that tick. | The existing broad nearest-enemy authoring can cast it, but the intended route/post facing controls remain future editor work. Exact authored facing is mechanically respected because use is limited to current facing. | Cast only if it interrupts a hostile firing lane to Palisade, a carrier, or at least one ally; do not replace a still-useful screen. Add route-segment marks with “face route forward” or explicit facing. | Promoted in Tapline-12. Rail deals 20 and stops, basic hits deal 10, Falling Star overlaps without losing its area, Null Field suppresses, flanks bypass. Keep the 30/6/18 values provisional until population play. |
| **Towline** · 40 · standard · medium 4 | **Tractor Hook 2:** range 6, 1-tick rooted lock, bolt travels 2 tiles/tick, pulls the first hit up to 3 tiles toward the frozen origin, cooldown 12 from start. The lock needs team sight and a frozen facing wedge; movement or lock loss cancels. Walls, constructs and bodies can intercept the bolt. | No map mark. The posture bundle targets the enemy carrier, but normal Fight is higher priority and can starve the Hook. Current UI does not explain the lock, pull destination, or interceptors. | Smart: enemy carrier first, otherwise a high-value exposed veteran/Longshot, only when the predicted pull is tactically beneficial and the ray is clear. Before committing, draw the source-to-target ray and ghost pull destination. **Prefer/Only** supports an activation zone, watched lane and target priority. | Strong control tool and good counterplay. Keep numbers initially under 3/4/6 guns; range 6 now equals the longest gun and exceeds Towline's own gun by 2, so team spotting matters. |
| **Patchbay** · 40 · standard · short 3 | **Repair Beam:** range 4; channels 10 hull every 2 ticks, maximum 20 per activation; cooldown 10 begins when the channel ends. It ends on movement, damage, death, full health, range/line loss, smoke, or hostile Null Field. | No map mark. `signature-ready` alone can select it, but Fight and recovery movement are above it. Catalog says preserve commitment; the Tactics materializer does not emit the continuation. | Smart: heal a missing-10+ ally when a two-tick safe channel is plausible; prioritize valuable Core carrier, protected veteran, then lowest health. Do not start while Patchbay or target is under immediate fire unless the selected posture is `Fight on`. **Prefer/Only** selects a protected bot/group and optional heal zone. Hold automatically while the authoritative channel is active. | Good sustain signature whose fun depends on visible target priority and reliable channel commitment. Current broad auto-use is not sufficient. |
| **Lantern** · 30 · swift · short 3 | **Survey Flare:** cast range 8, flight 2 tiles/tick, radius 4, active 8, cooldown 16 from start. Despite its name, it only removes smoke occlusion for its own team inside the footprint. It does not create vision, see through walls, or reveal enemies beyond an allied sensor's normal range/facing. | Exact point marks exist and the preset says “Reveal an exact approach.” That copy overpromises the real mechanic. With no smoke, a Flare can have zero gameplay effect. | Until semantics change, Smart should cast only when hostile smoke occludes a relevant active route, post sightline or watched zone. **Prefer/Only** points identify anti-smoke coverage, with the footprint and actual allied sight rays shown. | **Design decision required.** Either rename/copy it as anti-smoke illumination, or make it a real temporary sensor. Do not ship the current “reveal approach” promise. A true sensor would need a fresh rules profile and balance proof under base vision 5. |
| **Mortar** · 30 · deliberate · medium 4 | **Falling Star:** visible target within range 8, 2-tick tell, cross/plus footprint of five tiles, 10 damage, cooldown 12 from start in runtime. It damages hostile bodies and hostile deployments in the footprint; allied bodies are immune. | Exact point marks compile on `signature-ready` alone. They can fire at an empty authored point merely because a default target exists elsewhere, and Fight is higher priority. | Smart: cast on enemy value in the projected cross; target must be occupied/recently confirmed and inside active Tactic geometry. **Prefer/Only** uses points or small target zones and always renders the cross plus tell time. | Good area-denial skill. `TimingFor(FallingStar)` reports completion while runtime starts cooldown at activation—reconcile before relying on catalog timing. |
| **Minesmith** · 40 · standard · short 3 | **Trip Node:** adjacent empty legal tile, 10 hull, 20 trigger damage, revealed to an enemy body within 2, cooldown 12 from start. It persists until triggered, destroyed or replaced and does not physically block bodies. Recast replaces the owner's existing node. | Exact points work opportunistically when the bot comes adjacent, which is useful on a patrol. There is no “keep existing mine” condition, so returning to a mark after cooldown may replace a useful node elsewhere. | Smart: choose a choke/route tile just ahead of a held post or patrol, avoiding allied traffic; never replace a healthy useful node unless the policy says relocate. **Prefer/Only** points may live on a patrol or approach route; show detection radius 2 and “one active per bot.” | Strong map-first fit. Keep current numbers for the first pass. The UI should call it a damaging mine, not a blocker. |
| **Hush** · 40 · standard · medium 4 | **Null Field 3:** self-centered frozen radius 3, 1-tick public tell, then 5 active ticks, cooldown 18 from start. It suppresses hostile constructs/channels and prevents hostile signature starts in the field. Enemies can step out during the tell. It does not stop guns or movement. | No spatial mark. The posture bundle sees “enemy nearby,” not “hostile signature value nearby,” and Fight is higher. Commitment metadata is not materialized. | Smart: use when it will suppress a hostile active/telling signature or at least two signature-capable enemies are committed inside the frozen radius. **Prefer/Only** draws activation zones; the actual frozen radius is centered on Hush at use time. Render which hostile operations would be suppressed. | Excellent anti-combo tool. Current generic enemy-nearby trigger wastes it and obscures why it fired. Keep numbers initially. |
| **Relay** · 40 · swift · short 3 | **Arc Toss:** while carrying, straight/cardinal/diagonal target up to 5, no tell, starts after combat, Core travels 3 tiles/tick, cooldown 12 at launch. Walls clip the landing. The carrier must survive the committed combat tick. | Custody compilation already supports a preferred receiver or nearest ally toward the reactor; ordinary marked skill geometry is not exposed. A failed/illegal Toss falls through to banking. | Smart only inside active Core custody: toss to a legal empty-handed ally if it creates at least 2 walking tiles of forward progress along the authored return route and a useful catch, otherwise keep moving/banking that tick. **Prefer/Only** chooses receiver bot/group and eligible route segment. Do not present the two-Relay race as a starter. | Good advanced logistics. Preserve the current no-special-race architecture and same-tick fallback. Avoid generic combat-phase Toss automation. |
| **Switchback** · 30 · standard · medium 4 | **Exchange:** allied target within 6 and vision, 1-tick tell, cooldown 16 at completion. Both bodies must remain on their exact start tiles and destinations must remain legal. The targeted ally drops a carried Core; an owning Switchback that carries a Core keeps it through the swap. | No map mark. The default targets the healthiest ally but has no definition of “rescue,” safety improvement or desired destination. Fight is higher and commitment is not preserved. | Smart only for a clearly endangered carrier/veteran/low-health ally when the swap improves that ally's safety and the Switchback destination is acceptable. **Prefer/Only** selects a protected bot/group and an allowed source/destination zone pair. Preview both destinations and Core consequence. | Mechanic is potentially brilliant but current default is unsafe. The asymmetric Core behavior must be deliberately accepted, explained and tested before Smart is enabled. |
| **Longshot** · 30 · deliberate · long 6 | **Rail Line:** 1-tick rooted target lock, range 12, 20 damage, pierces all enemies on the line, allies are transparent, stops at and damages the first hostile deployment; cancel cooldown 8, success cooldown 18 at completion. It needs team sight and a clear frozen facing wedge and loses the lock on movement/damage/sight failure. | A two-point line is supported, but the `use` leaf is emitted **before** its move-to-post leaf and has no on-station condition. Longshot can therefore fire away from the drawn post. Fight is higher priority too. | Smart only while on its selected firing post/area, with team contact and a clear line; prioritize Core carrier, veteran, then a line with multiple enemies. **Prefer/Only** uses a firing post plus draggable aim handle or watched lane. Hold throughout the authoritative tell. | Signature-superrange is now 12 versus body vision 5 and long gun 6. That is healthy spotter synergy if the UI explains team-shared contact. Fix the station gate before player testing. |
| **Mason** · 50 · deliberate · short 3 | **Hardlight Block:** adjacent empty legal tile, 30 hull, duration 30, cooldown 14 from start. It blocks all bodies and hostile projectiles while active/not suppressed; three 10-damage basic hits destroy it. Recast replaces the owner's prior block. | Exact point marks exist and fit map-first play. There is no route-safety analysis or guard against replacing a useful block. | Smart should be conservative: protect a reactor/carrier route or close an obvious hostile choke only if the tile does not sever the authored friendly route. **Prefer/Only** exact points should show both teams' path detours and current block health/lifetime. Default `Only at marks` is defensible for alpha because a wrong body-block is costly. | Keep this static skill. It gives the map a player-authored, killable terrain layer and remains distinct if Palisade becomes mobile cover. Current 30 hull / 30 duration is a credible first test point. |
| **Sunder** · 40 · standard · medium 4 | **Target Paint 2:** range 7, 1-tick rooted acquisition, then up to 8 active ticks or 3 allied hits; each hit gains 10 damage. Cancel cooldown 8, full cooldown 16 from start. Acquisition needs target alive, in range and in team vision. | No map mark. It defaults to enemy carrier, but ordinary Fight is above it and there is no test that enough allies can capitalize. Commitment metadata is not emitted. | Smart: carrier/veteran/focus target only when at least two allies are plausibly able to connect during the window. **Prefer/Only** selects target priority and activation fight zones. The UI should show “next 3 team hits +10,” not merely a mark icon. | Powerful focus-fire enabler, especially with veterancy. Keep numbers until coordinated proof; prevent isolated Sunder from wasting it. |
| **Repulsor** · 50 · standard · short 3 | **Kinetic Burst 2:** frozen self origin, 1-tick rooted tell, pushes every adjacent body—ally or enemy—1 tile after response movement if the destination is legal; no damage; cancel cooldown 6, full cooldown 12 after maturity. | No map mark. “Enemy nearby” is too broad for an adjacency skill and does not account for allies or push destinations. Fight is higher. | Smart: at least two adjacent enemies, an adjacent enemy carrier, or a genuinely surrounded body, and the resulting pushes are net beneficial. Avoid pushing an enemy carrier toward the friendly reactor or ejecting allies from required posts. **Prefer/Only** activation zones plus optional protected zone/edge. Render ghost destinations during authoring/Practice. | Good anti-ball and anti-corridor answer. Older audit copy that calls it damaging is stale; current effect is displacement only. |
| **Veil** · 30 · swift · short 3 | **Smoke Canister 2:** range 6, public flight at 3 tiles/tick, radius 2, duration 10 after landing, cooldown 18 from start. Smoke blocks sight rays for both teams; friendly Survey Flare cancels its occlusion within the overlap. | Exact points exist but use only `signature-ready`; no check that the smoke protects something or harms friendly Longshot/Hook/Repair sightlines. Fight is higher. | Smart: cover a carrier crossing or break a hostile long-range lane inside active route/fight geometry, while avoiding critical friendly sightlines. **Prefer/Only** points/zones show the radius, flight delay and friendly lines that will be cut. | Strong corridor utility under shorter vision/ranges. Keep numbers initially, but require visible collateral preview. |
| **Nest** · 40 · deliberate · medium 4 | **Sentinel Seed 2:** 1-tick rooted placement on an adjacent legal tile, 20 hull, range 4, 10-damage bolt every 2 ticks, bolt speed 2, duration 30, cooldown 18 from start. One active seed per bot. It selects ray-aligned enemies using unobstructed wall/smoke line, but does not require team-visible contact. | Exact points work, but there is no coverage/ray preview or replace guard. Catalog says preserve the placement tell; Tactics does not. Current automatic targeting can shoot at a body the owning team does not otherwise see. | Smart: deploy at an authored/obvious guard post with useful ray coverage, without replacing a healthy useful seed. **Prefer/Only** points show the eight firing rays and range 4. Decide whether the seed requires team contact or owns an explicit sensor before calling this Smart complete. | Great hold/patrol skill. Under vision 5 and medium gun 4, its range is reasonable; its hidden-information semantics are not. Resolve them explicitly. |

## Why the current authoring is not yet reliable

### 1. Fight currently wins before the skill can shape the fight

`ArcRelayTacticsMaterializer.cs:780-818` emits custody, recovery,
destination Fight and travel Fight before signatures. Most unmarked signatures
are gated on `enemy-nearby`, the same situation that makes Fight eligible. The
earlier Fight leaf wins. Palisade, Towline, Hush, Sunder, Repulsor and other
combat signatures can therefore be silently starved.

This is not solved by putting every skill above every action with a broad
condition. Smart predicates must first become conservative enough that a
higher skill leaf means “this is a useful opportunity,” not merely “cooldown
is ready.”

### 2. Commitment is described but not compiled

The authoring catalog marks Kestrel, Towline, Patchbay, Mortar, Hush,
Switchback, Longshot, Sunder, Repulsor and Nest as
`PreserveByDefault`. The Tactics materializer never consumes that metadata.
The lower Orders vocabulary already supports
`signature-commitment-active`, and focused Orders scenes prove an intentional
hold for Repair and Rail, but Tactics does not generate it.

Result: a new phase action or floor move can cancel a rooted tell/channel. Add
one body-local continuation derived from the authoritative operation state;
do not copy tell/duration numbers into the Tactic.

### 3. Marks are keyed by class, not by bot

`ArcRelayTacticsDsl.cs:955-1026` validates one mark set per class, and the web
stores `job.sigs[bot.class]` (`MapFirstChrome.tsx:448-514`). The roster rules
permit one duplicate wildcard. Two Masons, Relays, Palisades, or any duplicated
class cannot receive independent skill policies or geometry.

The source must be keyed by stable squad slot. A deterministic migration can
copy a legacy class mark to every matching slot in that group and mark the
result for review; new documents must never collapse it back by class.

### 4. A spatial mark is currently only a target, not an opportunity

Exact points compile with `signature-ready` and a default named target. They do
not express occupancy, friendly-fire safety, meaningful effect, current
deployment state, or whether the caster is at the intended firing post.
Longshot is the clearest correctness bug: its Rail use precedes and is not
conditioned on its drawn stand position (`ArcRelayTacticsMaterializer.cs:
1405-1435`).

### 5. Several public descriptions do not match runtime truth

- Survey Flare is anti-smoke, not independent reveal.
- Kinetic Burst currently pushes and does no damage.
- Sentinel can select enemies without team-visible contact.
- Falling Star ignores allied bodies, matching the rest of the signature band.
- Exchange drops the targeted ally's Core but lets the casting Switchback
  transport its own Core through the swap.
- Falling Star's catalog timing says completion-anchored cooldown while its
  behavior sets cooldown at activation.

These may be valid rules, but they must be deliberate, tested and explained
before the editor automates them.

## One map-first skill authoring model

### Player surface

Selecting one bot in one phase should open its existing bot drawer and show:

1. signature name and a one-sentence rule summary;
2. one segmented policy control:
   - **Smart** — use the catalog's conservative default inside this phase;
   - **Prefer marks** — try authored opportunities first, then Smart;
   - **Only at marks** — never use outside authored opportunities;
   - **Off** — do not author a use leaf in this phase;
3. the relevant draw tool: point, zone, route segment, firing post + aim,
   facing handle, or protected/target bot;
4. a plain-language preview such as “Wall while crossing this segment, facing
   forward” or “Mortar this area only when no ally is in the cross”;
5. an optional advanced toggle: **Must happen before this phase can finish**.

`Must happen` is not a reservation or atomic cast. It adds phase-local evidence
from an accepted/completed signature operation to the existing phase exit
condition. The UI must require a maximum phase duration or warn about a
possible deadlock. Normal “Only at marks” merely constrains opportunities and
does not hold the phase open.

### Suggested source shape

The exact names can follow repository conventions, but the semantic ownership
should be explicit and slot-local:

```json
{
  "skillPlans": {
    "2": {
      "mode": "prefer",
      "requiredBeforeExit": false,
      "opportunities": [
        {
          "kind": "route-segment",
          "line": [[8, 14], [18, 14]],
          "facing": "route-forward"
        }
      ]
    }
  }
}
```

The catalog—not UI switches—declares which opportunity kinds and target
options are legal for each signature. Extend the existing
`ArcRelaySignatureAuthoringDescriptor` with data such as:

- `OpportunityKinds`: point, zone, route segment, post-and-aim, friendly target;
- `DefaultPolicy` and safe smart preset;
- `InstructionBand`: custody, commitment, opportunity, or transit;
- `PreserveCommitment`;
- deployment ownership/replacement warning;
- preview footprint/range semantics from the resolved Engine definition.

Numeric timing, range, damage and duration continue to come from the resolved
Engine rules projected by the App. Do not duplicate balance constants in web
code or TacticalPlaybooks.

### Ordinary Instruction order

Keep the Tactic phase coordinator as the only state machine. For each body in
the selected phase, materialize a visible ordinary order band:

1. active Tactic-wide abort/phase decision (existing coordinator);
2. preserve an already-active signature commitment where the descriptor says
   it must be preserved;
3. Core custody branch, including Relay Toss where applicable;
4. urgent recovery;
5. useful marked/Smart signature opportunity;
6. ordinary Fight;
7. scan;
8. authored route/post/patrol movement and fallback.

Kestrel's route Dash lives in the transit portion of step 5 and remains
empty-handed. Relay Toss lives inside custody, not as a parallel generic skill
system. All other signatures use the same opportunity slot. If the selected
use cannot be admitted, it falls through to Fight or movement that tick.

This ordering is deterministic and inspectable. The generated explanation can
say exactly which higher leaf won. There is no cooldown scheduler, no “skill
AI” ticking beside Instructions, and no hidden priority between a Tactic and a
skill.

## Palisade moving frontal shield implementation

### Clarify the class ownership

Palisade historically owned **Prism Wall**. Mason owns **Hardlight Block**. The
promoted moving shield is a new Prism Wall envelope for Palisade—display name
**Prism Screen**—not a Mason rewrite.

That leaves a useful class pair:

- **Palisade:** mobile, directional projectile protection for a convoy or
  corridor advance;
- **Mason:** static, killable terrain that redirects bodies and shots.

Giving Mason the moving screen or deleting Hardlight would collapse two
different map decisions into one.

### Promoted Tapline-12 contract

| Property | Prototype value / rule | Why |
| --- | --- | --- |
| Shield hull | **30** | Three basic 10-damage hits; a 20-damage Rail is stopped but leaves it one hit from breaking. |
| Active duration | **6 ticks** | Enough for a corridor push without covering most of an 18-tick cooldown. |
| Cooldown | **18 ticks from activation** | Twelve-tick uncovered window after natural expiry; stronger mobility pays more than current Wall's 8/16. |
| Facing | Current Palisade facing; follows later rotations | Makes facing/post/route authorship meaningful. Rotating spends the ordinary action opportunity and exposes flanks. |
| Movement | Follows owner after every accepted movement; no additional shield penalty beyond the deliberate chassis's 2/3 cadence | Tests the fun without stacking a second speed tax. The shared Combat Strafe model and authored facing support a stable corridor heading. |
| Body collision | None | It is a projectile screen, not a second Hardlight Block. |
| Projectile collision | Hostile projectiles crossing the front screen hit it first; allied fire passes from behind | Preserves current team-relative Prism behavior and convoy fantasy. |
| Rail | Stops at the first screen segment and deals 20 to shield hull | Gives a clear counter without nullifying Longshot forever. |
| Area damage | Falling Star damages overlapping screen hull but still resolves its area | Mortar remains an answer to a packed convoy. |
| Suppression | Hostile Null Field suppresses the screen while Palisade/screen footprint is affected | Preserves Hush counterplay. |
| Owner death | Screen ends immediately | An attached defense should not become a free static deployment after death. |
| Recast | Restarts/replaces that bot's screen | Preserves one-owned-operation semantics. |

Use a three-segment frontal footprint only if collision remains legible in
one-tile corridors. The cleanest first implementation can reuse the current
three Wall tiles but recompute them from the owner's current position and
facing after movement. They must **not** be returned by `BlocksBody`.

An edge-based shield would be geometrically purer, but it introduces a new
projectile-edge collision model. Prefer derived moving tiles for the first
prototype. Define occupied-front-tile behavior explicitly: a segment
overlapping a body is inactive for that tick, rather than displacing or
sharing a construct with the body.

### Smart and authored behavior

Smart use is conservative:

- Palisade is moving/holding inside the current Tactic's geometry;
- a hostile projectile lane, pending Rail line, or visible ranged threat is in
  the front half-plane;
- the screen protects Palisade, a carrier, or another ally behind it;
- no healthy active screen already covers that lane.

Player controls:

- on a post: face a map anchor and screen when the lane is threatened;
- on a route/patrol: mark eligible segments and select `face route forward` or
  a fixed facing;
- `Prefer marks` allows reactive Smart use elsewhere in the phase;
- `Only at marks` supports a deliberately timed corridor crossing.

Authored facing always wins. Smart must not rotate every tick toward the
nearest enemy; that would create opaque shield dancing and erase flank
counterplay.

### Counters and fun case

The shield creates readable answers rather than a universal damage reduction:

- flank or rear fire bypasses it;
- focus fire breaks 30 hull;
- Rail consumes most of it but is stopped once;
- Mortar attacks the packed formation/footprint;
- Hush suppresses it;
- Towline or Repulsor can disturb the screen formation;
- Mason can alter the route in front of it;
- smoke can cut the convoy's own target knowledge.

That interaction web is why the change is more promising than simply adding
hull to Palisade.

### Engine and presentation implications

Implement it as a new concrete signature definition variant under a new loop
profile/rules fingerprint. Historical Prism Wall definitions and replay bytes
must remain resolvable. Keeping stable action/signature id `prism-wall` is
reasonable if the new definition is versioned like `SentinelSeed2`; use the
display name `Prism Screen` in the new catalog.

Required runtime work:

- derive operation positions from live owner position/facing after movement;
- remove it on owner death and suppress it under hostile Null Field;
- consume real damage rather than contact count in the new durable envelope;
- make projectile and Rail collision use the derived positions;
- keep it out of body collision and static pathfinding;
- project movement/facing changes into every replay snapshot.

The current 2D/3D Prism presentation assumes static operation positions.
Either parent the visual to the Palisade transform or update projected
positions every frame/tick. Test turns, route corners, damage state, expiry,
suppression, death and replay seek; avoid a one-tick visual lag. This is a
presentation-mechanics change and should version the signature presentation
package if the replay contract requires it.

## Combat behavior under Tapline-11 movement rates

Tapline-11 makes swift/standard/deliberate real 1/1, 4/5, and 2/3 movement
cadences under one Combat Strafe control model. That difference can create
useful combat identity, but it must be a visible posture preset rather than
hidden class AI.

Suggested fight presets:

- **Hold line:** defend the authored post/zone; slower/deliberate bodies screen
  lanes and do not auto-strafe.
- **Pressure:** current bounded approach/chase behavior.
- **Mobile flank:** during the existing gun reload window, a quick body may
  take a bounded side/rear angle inside the active Tactic geometry, then fire
  or return. This is recommended for Kestrel, Lantern, Relay and Veil, not
  silently forced on them.
- **Screen carrier:** Palisade/Mason/Repulsor occupy the authored lane between a
  carrier and known fire, without inventing a separate escort scheduler.

Guardrails for Mobile flank:

- authored posts, facing and routes always win;
- maximum 2 displacement steps from the episode anchor before return;
- every step must improve a stable flank goal or return progress;
- target loss, phase change, Core pickup or route departure ends the episode;
- do not choose a new “best” side every tick;
- no back-and-forth kiting against a fixed target and no local orbit;
- log the ordinary Fight/move reason in Practice.

The rear 2x multiplier makes a bounded reload-window flank potentially very
strong. Swift classes also use the two-tick short-gun interval, normally
leaving only one reposition tick before the next shot. Start with an explicit
opt-in that stops when the shot is ready; any preset that deliberately gives up
one ready shot for a second flank step must say so. Run orbit/reversal gates
before considering it a default for swift bodies.

Signature interactions with those roles are natural: Kestrel Dashes along the
bounded flank route, Veil Smokes a crossing, Lantern counters smoke, Relay
uses mobility for custody rather than free combat kiting, Palisade screens the
lane, Longshot/Mortar/Nest hold authored ground, and Towline/Sunder exploit the
contacts the flank produces.

## Ranked implementation plan

### P0 — make existing signatures honest and executable

1. Change Tactics signature ownership from class-keyed to slot-keyed; migrate
   legacy marks deterministically and add duplicate-class tests.
2. Add the four policy values and catalog-declared opportunity kinds. Keep
   Smart as the no-configuration default, but make risky terrain skills such
   as Mason eligible for `Only at marks` as their catalog default.
3. Materialize catalog commitment continuation and useful signature
   opportunities before ordinary Fight; keep Relay inside custody and Kestrel
   empty-handed/route-bound.
4. Fix Longshot's on-station gate and show firing post/aim/range/team-vision
   requirements.
5. Reconcile Falling Star cooldown metadata/runtime, preserve its documented
   allied immunity, decide/document Exchange Core asymmetry, and decide Sentinel
   vision ownership.
6. Make Survey Flare copy and Smart predicate match anti-smoke reality, or
   version it into a real sensor before advertising reveal.

### P1 — finish the map-first controls and defaults

7. Implement the row-by-row conservative predicates and previews above from
   catalog data plus resolved rules; no skill-id switches in React.
8. Support activation zones, points, route segments, post-and-aim/facing and
   friendly target selection in the bot drawer. Show current attack range,
   vision 5, signature footprint and timing together.
9. Add deployment replace warnings and “existing deployment still useful”
   guards for Prism/Trip/Hardlight/Sentinel.
10. Make generated priority/rejection reasons visible in Practice: “Fight was
    used because Hook had no clear beneficial pull,” for example.

### P2 — Palisade screen promoted in Tapline-12

11. **Done:** versioned moving Prism definition and v4 presentation using the
    30-hull / 6-duration / 18-cooldown candidate.
12. **Mechanics proof done:** front/side/allied fire, Rail, body-overlap,
    movement, turning, suppression, death, expiry, cooldown, canonical
    projection, and historical Tapline-11 compatibility. Keep population
    tuning and authored route-segment proof as follow-up. Mason is unchanged.

### P3 — explicit combat mobility after the skill pass is stable

13. On the promoted movement rates, add an explicit bounded Mobile flank
    posture and prove no orbit/kiting regression. Do not make class speed a
    hidden second behavior system.

## Required tests and proof scenes

### Static/compiler acceptance

- all 16 catalog entries round-trip resolved numeric mechanics and lifecycle;
- descriptor cooldown anchor agrees with runtime behavior for every concrete
  current signature kind;
- two copies of the same class in one group retain independent policy, marks,
  target and migration state;
- Smart / Prefer / Only / Off generate the documented ordinary leaf order;
- every `PreserveByDefault` signature generates and exits an authoritative
  commitment continuation;
- unavailable/illegal use falls through to Fight/movement in the same tick;
- Kestrel never Dashes while carrying under its generic route policy;
- Longshot cannot Rail until on its authored station;
- risky point skills enforce occupancy/safety/replacement predicates.

### Deterministic per-signature micro scenes

Build one small fixed scene per signature against deterministic actors. Prove
accepted use, decline reason, commitment, completion/cancellation, cooldown,
targeting, suppression, deployment replacement and fallback. Existing engine
grammar tests are valuable, but `ArcRelayTacticalPlaybookCompilerTests` mostly
prove valid syntax and `OrdersSignatureOpportunitySceneTests` cover only
Kestrel, Repair and Rail. They do not prove all-16 Tactics usability.

### Palisade corridor-counter scene

On the promoted Tapline map:

1. Palisade and a Core carrier take the long corridor toward their reactor.
2. A deterministic Longshot fires from the corridor axis; a short/medium bot
   supplies additional front fire.
3. The authored route segment activates the screen facing forward.
4. Assert the first Rail stops at the shield, applies 20 to its 30 hull, and
   does not hit bodies behind it.
5. Assert one basic 10-damage hit then destroys it.
6. In a paired run, a side attacker bypasses the shield.
7. Assert route movement, turning, suppression, death and expiry update the
   projected screen without body collision or replay lag.
8. Compare with the same convoy and seed without the screen. This is a
   mechanics/fun proof, not a population balance claim.

### Interaction scenes

- Smoke blocks a Longshot/Repair line; friendly Flare restores only the normal
  sight through the overlap and does not reveal beyond vision 5.
- Hush suppresses an active screen/deployment/channel and prevents a start in
  its radius after the response-movement tell.
- Mason block visibly changes both teams' pathing and survives exactly three
  basic hits.
- Sentinel's chosen visibility rule is proved with an enemy outside all body
  facing cones but inside its range 4.
- Falling Star's chosen friendly-fire rule and cross preview agree.
- Exchange's chosen carrier rule agrees for both owner and target.
- Sunder plus two allies consumes three enhanced hits without creating an
  independent focus-fire scheduler.

### Movement safety scenes, if speeds/postures change

- swift flank gains a rear angle during reload and returns to its authored
  geometry;
- hold/post bodies never leave merely because a flank is possible;
- no 2-4 tile local orbit, repeated reversal, wall pinning, or target-loss
  chase across the phase boundary;
- an incidental Core pickup immediately hands control back to the same
  Tactic's custody behavior.

## Existing evidence and its limits

Relevant current tests include:

- Engine grammar/lifecycle: `tests/BotArena.Engine.Tests/ArcRelayGrammar2Tests.cs`,
  `ArcRelayGrammar3Tests.cs`, `ArcRelaySignatureLockTests.cs`,
  `ArcRelaySignatureWindupTests.cs`,
  `ArcRelaySignatureActivationTimingTests.cs` and
  `ArcRelayH0DefinitionTests.cs`;
- Tactics/Orders compiler: `tests/BotArena.Cli.Tests/ArcRelayTacticalPlaybookCompilerTests.cs`
  and `ArcRelayTacticsMaterializerTests.cs`;
- focused runtime scenes:
  `tests/BotArena.Cli.Tests/OrdersSignatureOpportunitySceneTests.cs`;
- synthetic presentation coverage:
  `tests/BotArena.Cli.Tests/ArcRelaySignatureStageCommandTests.cs`.
- promoted Prism Screen mechanics:
  `tests/BotArena.Engine.Tests/ArcRelayPrismScreenTests.cs`.

The older `docs/ARC-RELAY-SIGNATURE-AUDIT.md` was a counterplay grammar audit,
not a current balance or Tactics usability pass. The older Program opportunity
reports document lower-level capabilities, but the current map-first Tactics
materializer does not inherit every promise from those reports. A synthetic
all-signature Stage replay proves rendering, not that a player can author
sound use.

## Unresolved owner decisions

These should be answered before calling Smart skills player-ready:

1. **Survey Flare:** explicitly anti-smoke, or a real temporary sensor? The
   recommendation is to make its UI honest immediately, then test a sensor
   variant separately.
2. **Sentinel perception:** require existing team contact, or give the seed an
   explicit projected sensor? The recommendation for alpha is team contact;
   an autonomous sensor is a larger vision/balance feature.
3. **Falling Star friendly fire:** resolved by owner decision; allied bodies
   are immune, and the editor still previews the hostile cross and tell.
4. **Exchange and Cores:** retain “caster transports, target drops,” or make
   both sides drop? The current asymmetry is strategically interesting but
   surprising.
5. **Palisade tuning:** Tapline-12 resolves the footprint as three derived
   front tiles. Population play still needs to validate the initial 30/6/18
   tuning and whether body-overlap deactivation is sufficiently legible.

## Final recommendation

Do not add more signature mechanics yet. Make these sixteen understandable,
slot-specific and dependable through one map-first policy model, then validate
the newly promoted Palisade screen in ordinary authored play. That pass should
produce substantially more playable depth than another class would: players
will be able to draw a
route, post, lane or target relationship and trust the bot to use its unique
tool for a visible reason, while the underlying game remains one ordered
Instruction executor.
