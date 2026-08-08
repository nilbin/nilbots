# arc-light

A Frontline Labs **striker** whose doctrine is built around the class-skill kit
rather than retrofitted onto it. Wave-5 revision of the wave-4 entrant of the
same name.

Qualified **T4** on `frontline-qualification-5`
(`frontline-duel-depth-union-t4-v1`), `balanceEvidenceEligible: true`.

## How it plays

- **Aiming is interception, not line of sight.** Movement resolves before
  combat, so every candidate arc is scored against where the target can *be* when
  the bolt arrives. Under a facing-locked coupling a body may only step where it
  faces, which turns its reachable set into a ray — arc-light shoots the ray.
- **The launch offsets are read, and therefore earned.** The gun's declared
  initial-aim envelope is the one place a doctrine can learn whether a diagonal
  launch exists. When it does, a facing owns three headings instead of one: a
  step keeps the body armed 45 degrees either side of the line it walks, which is
  what makes repositioning under a facing lock an armed retreat instead of a
  disarmed one.
- **A curved arc is a commitment.** The whole declared bend envelope is
  enumerated and previewed through the SDK's own bend rule, but a bend is only
  spent on a hard interception: the tile the target stands on, or the one tile it
  can reach exactly as the bolt lands.
- **The volley is priced in bodies, by arithmetic.** A stance costs its entry
  windup plus the cast plus the return's windup; the ordinary gun would fire one
  aimed bolt per cadence in that window. So the fan must connect with
  `ceil(cycle / cadence)` *distinct* bodies — two, on the measured arm — or the
  cast is a tempo loss however good it looks. That is why arc-light casts into a
  swarm and almost never into a duel.
- **A loaded gun is not a bolt.** A windup permits nothing but waiting, so a
  commitment is refused when an enemy gun merely *bears* on the tile, not only
  when a bolt is already in flight. The surplus over break-even is the budget for
  bearings: none at break-even, one more per extra body the fan reaches.
- **Where a transition may rise comes from the route, not the map.** A map can
  publish a transition-forbidden tag over ground that no current route forbids;
  the route's own `placement` is the authority, so on an open-ground arm the fan
  rises on the objective it is denying.
- **The supply line outranks the swarm.** The fabrication and replication
  catalogs name which forms can create another body. That body is the only one
  whose death stops the rebuilds, so it is the priority target — regardless of
  what its class is called.
- **Presence is arithmetic too.** Where surplus objective weight scales capture
  pressure, every tick spent outnumbered on the point is a full point of progress
  against you, and one more of your bodies cancels exactly one of them. Being
  outnumbered is a reason to stand on the point, not to leave it.
- **The hold is read, never inferred.** Inside an enemy territory hold a completed
  capture is spent, so the claim is banked short of the threshold and completed on
  the tick the hold lifts. Only an enemy standing alone erodes a claim, so leaving
  an empty objective is free and leaving a contested one is not.
- **Every projectile it does not own is hostile** — including a bolt of its own
  that an aegis shell returned. Enemy guards are counted from published deflection
  events accumulated across the life, so a shield one contact from breaking is fed
  rather than avoided.
- **Bodies envelop.** Independent lives with empty private memory take a rank
  from the shared observation and spread across different bearings, because
  clumping into one lane is what feeds a three-bolt fan.

Nothing above is conditioned on an arm name. The same artifact plays the kit-off
cells (where no stance route exists and the stance code simply declines), both
bend envelopes, both ground arms, and the classless duel-depth qualification
profile — where it finds an anchor route instead, fabricates from the pad, and
never touches a volley. Handed a chassis it was not written for it reads that
chassis's routes instead: on a bulwark it anchors behind relief and raises a
deflecting shield against a bearing, without a line of code that names a class.

## Building

```bash
nilbots build <this directory> --no-cache
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4
```
