# Arc Relay movement-rate design

**Status:** Tapline-11 promoted mechanical baseline; combat mobility remains a
reviewed follow-up.

## Verdict

Tapline-11 should make `swift`, `standard`, and `deliberate` honest movement
rates instead of three unrelated handling exceptions:

| chassis | public move opportunities | rate | facing while moving |
|---|---:|---:|---|
| swift | 1 of every 1 tick | 1.00 | Combat Strafe |
| standard | 4 of every 5 ticks | 0.80 | Combat Strafe |
| deliberate | 2 of every 3 ticks | 0.67 | Combat Strafe |

Every accepted move is still one adjacent tile. Closed opportunities do not
bank, so waiting cannot create a burst. Stable unit ID staggers closed ticks;
an equal-rate squad does not visibly stop in lockstep. The rate is part of the
canonical rules contract, SDK, replay wire, manifest, CLI output, and hosted
playlist—not an unexplained executor heuristic.

Combat Strafe is one control model for every chassis: forward and lateral
travel can preserve aim, while rearward travel turns the body. Tapline-10 and
older profiles retain their exact historic facing models and universal 1/1
travel.

The renderer still receives ordinary one-tile relocations. A missed movement
opportunity is a stationary tick; it does not stretch one tile across several
ticks, so per-tile animation remains brisk.

## Measured travel impact

These are conservative terrain-only ticks for an arbitrary cadence phase.
They exclude traffic, combat, signatures, and detours. `Carried` includes the
existing Core-owned two-tick relocation gate in addition to chassis cadence.

| route steps | swift empty / carried | standard empty / carried | deliberate empty / carried |
|---:|---:|---:|---:|
| 12 | 12 / 24 | 15 / 30 | 18 / 36 |
| 19 | 19 / 38 | 24 / 48 | 29 / 57 |
| 22 | 22 / 44 | 28 / 55 | 33 / 66 |
| 29 | 29 / 58 | 37 / 73 | 44 / 87 |
| 31 | 31 / 62 | 39 / 78 | 47 / 93 |
| 41 | 41 / 82 | 52 / 103 | 62 / 123 |
| 58 | 58 / 116 | 73 / 145 | 87 / 174 |

The carrier gates compound rather than replace one another. This is simple and
truthful, but it materially rewards swift logistics. It should be watched in
playtests: Relay and other swift carriers may become the default for high-value
returns, while deliberate carriers need escorts and shorter routes. Do not add
a hidden carrier speed exception until that tradeoff has real match evidence.

### 600-tick reachability

Tapline's enemy Well is 31 steps from the home reactor by the direct route and
41 through Centre. Using the latest opening tick (56), the 46-tick source-local
recharge, an empty outbound trip between banks, and three enemy-Well Cores (each
worth 4 charge), conservative no-combat destruction times are:

| repeated enemy-Well route | swift | standard | deliberate |
|---|---:|---:|---:|
| direct, 31 steps | 334 | 382 | 429 |
| via Centre, 41 steps | 394 | 469 | 549 |
| deliberate outer route, 58 steps | 520 | 637 | 752 |

So every chassis can still end regulation inside 600 on both practical routes.
The 58-step experiment is a viable three-bank plan only for swift travel; it is
not a standard or deliberate solo win plan. Parallel Cores, Arc Toss, Dash,
combat, denial, and traffic can change real outcomes. This is a reachability
proof, not a balance verdict.

## Speed-aware combat movement

Do **not** make fast bots circle automatically. Rear hits deal 2x damage, while
swift classes use the short gun's two-tick firing interval. A baseline rule of
“shoot whenever ready, move only during reload” usually grants only one step
before the next shot; arbitrary shot delay, on the other hand, risks making 2x
rear damage the dominant autopilot. Both behaviors would hide a strategically
important decision.

A later explicit phase posture named **Mobile flank** is mechanically coherent:

- the player opts a Group into it for one Tactic phase;
- the phase's zone, route corridor, or posts are the hard movement leash;
- choose a stable left/right flank side when contact begins and keep it for the
  episode;
- allow at most two displacement steps from the episode anchor;
- every step must strictly improve a reachable rear-angle firing tile or return
  progress—never orbit, reverse, kite, or backtrack;
- authored posts/routes, Core custody, recovery, and higher-priority
  Instructions always win;
- stop repositioning when the gun is ready, the target is lost, the bot gains a
  Core, the phase changes, or the leash would be crossed;
- surface `repositioning`, `shot ready`, `no improving tile`, and `returning` in
  Practice diagnostics.

An advanced preset could later permit sacrificing exactly one ready short-gun
shot to seek the second flank step, but it must be explicit and visually
disclosed. Slower bodies may eventually get a separate **Screen advance**
preset that advances behind cover or holds between a protected ally and a
threat; it should not be bundled into Mobile flank.

The current executor can support this without a second scheduler: the Tactics
compiler can emit ordinary bounded Fight/Move Instructions under the existing
top-to-bottom priority and phase lifecycle. What is missing is a reviewed
player-facing preset plus a small stable-side/episode fact in the existing
fight continuation. That should be implemented only after the simple chassis
rates have been played, because it is a tactical product choice rather than a
movement legality requirement.

## Acceptance boundary

- Implement now: canonical public cadence, unified Combat Strafe, immutable
  Tapline-11 and hosted playlist v28, historical compatibility, focused route
  and legality proofs.
- Defer: Mobile flank, Screen advance, class-specific combat AI, or any hidden
  kiting/circling policy.
- Balance follow-up: compare carrier class share, successful 4-charge returns,
  travel deaths, and decisive-tick distributions before tuning these rates.
