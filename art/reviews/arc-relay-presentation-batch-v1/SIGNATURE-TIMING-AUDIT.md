# Tapline signature timing and mechanics audit

This audit reads the current `tapline-1` rules and runtime chronology on
2026-08-12. It is the mechanics gate that precedes final VFX and SFX work.

At normal viewer speed, Nilbots presents 2.5 ticks per second, so one authored
tick occupies roughly 400 ms and two ticks roughly 800 ms.

## The important distinction

A published `tell` always gives the renderer time. It does **not** always give
the opposing mind a decision.

Signatures created during tick `T` become visible in that tick's resolved
state. Most timed utility signatures currently mature in the tick-start phase
of `T+1` or later, before the next movement and action resolution. A one-tick
utility tell can therefore be a 400 ms spectator telegraph with no reaction
window. The locked line attacks are deliberately different: Tractor Hook and
Rail Line mature after `T+1` movement, so their one-tick tells really can be
answered by moving, breaking the lock, interposing, or abandoning the cast.

A two-tick tell that matures at tick start also supplies one full intervening
decision tick. That is why Falling Star's current two-tick mark is actionable
while the current one-tick Null Field and Kinetic Burst tells are not.

## Current Tapline matrix

Tapline uses signature grammar 2 and a one-tick strike wind-up. Nine signatures
publish an explicit `tell`; Repair Beam and Survey Flare provide useful
channel/flight time without one; Arc Toss adds flight after its tell; five
signatures become active immediately.

| Class / signature | Current pre-effect time at 1× | Is it an actionable window? | Recommendation |
| --- | --- | --- | --- |
| Kestrel — Vector Dash | `tell` 1 tick / 0.4 s | No opposing response; it resolves at tick start. That is acceptable for self-movement. | Keep. Use the tell as anticipation and animate the real authored relocation afterward. |
| Palisade — Prism Wall | None; active on the cast tick | No. It can intercept later projectile delivery in the same tick. | Keep its reactive identity. Draw the complete blocking outline immediately, then settle matte panes inside it. |
| Towline — Tractor Hook | `tell` 1 tick / 0.4 s, then a real grapple bolt | Yes. The locked line matures after response movement; the bolt then travels and can be blocked. | Keep. This is the model for an honest offensive wind-up. |
| Patchbay — Repair Beam | Channel begins immediately; first repair after 2 ticks / 0.8 s | Yes, as interruption rather than evasion: movement, damage, sight loss, or a full target ends the channel. | Keep. Animate link formation over the first channel interval. |
| Lantern — Survey Flare | In flight for 1–4 ticks / 0.4–1.6 s by distance | The field is informational; the travel is a real presentation and planning window. | Keep. Land the reveal ring only on the authored `landed` transition. |
| Mortar — Falling Star | `tell` 2 ticks / 0.8 s | Yes. One complete action tick exists between declare and tick-start impact. | Keep. The current timing supports the storyboard. |
| Minesmith — Trip Node | None; active on the cast tick | It is a hidden trap placed after movement, so an enemy cannot step on a newly placed node until a later movement phase. | Keep. Prop/model work remains with the other 3D agent. |
| Hush — Null Field | `tell` 1 tick / 0.4 s | **No.** It activates at the next tick start before response movement. | Rework in Tapline vNext: mature after the response-movement phase while preserving the frozen cast footprint. |
| Relay — Arc Toss | `tell` 1 tick plus 1–3 flight ticks / total 0.8–1.6 s | The tell itself is not an action window, but the real Core flight is visible; longer throws allow positioning before landing. | Keep. Animate the one authoritative Core, never a duplicate. |
| Switchback — Exchange | `tell` 1 tick / 0.4 s | No; the ally commitment was made on the cast tick and swap resolves at tick start. No victim needs counterplay. | Keep. Treat the tell as a readable paired commitment, not an evasion promise. |
| Longshot — Rail Line | `tell` 1 tick / 0.4 s | Yes. The locked piercing line matures after response movement and can cancel under the declared-strike rules. | Keep. The current timing is short but honest. |
| Mason — Hardlight Block | None; active on the cast tick | Body movement has already resolved, so it blocks bodies from the next movement phase; it can intercept later same-tick projectiles. | Keep its reactive builder role. Show the full collision silhouette immediately while slabs settle. |
| Sunder — Target Paint | None; active on cast and can enhance later same-tick attacks | **No.** The first bonus can land before the target ever sees acquisition. | Rework in Tapline vNext: one-tick tracked acquisition, resolving after response movement; loss of range/sight cancels it. |
| Repulsor — Kinetic Burst | `tell` 1 tick / 0.4 s | **No.** It currently resolves at tick start before adjacent bodies can use the tell. | Rework in Tapline vNext: mature after response movement; hold the caster at the frozen origin during charge. |
| Veil — Smoke Canister | None; the entire remote field is active on cast | No. Its name and proposed visual imply a thrown object that the rules do not contain. | Rework in Tapline vNext: a public ballistic `in-flight` phase at 3 tiles/tick, then start the full field duration on landing. |
| Nest — Sentinel Seed | `tell` 1 tick / 0.4 s before deployment | Not before placement, but the sentry cannot fire on its deployment transition and its later bolts are real and dodgeable. | Keep. Prop/model work remains with the other 3D agent. |

## Recommended Tapline vNext mechanics package

The presentation goal should not mutate historical `tapline-1` behavior under
the same contract fingerprint. Approval authorizes a new signature grammar and
a new Tapline rules profile, preserving all frozen profiles and hosted playlist
references.

### 1. Null Field — make the existing tell actionable

- Keep radius 3, active duration 5, cooldown 18, and a one-tick tell.
- Freeze the field footprint at declaration exactly as today.
- Mature after the following movement phase instead of at tick start.
- Revalidate signature starts after activation: a body that remained inside
  and chose a signature has that resolution blocked, while a body that spent
  the tick moving out is safe. Existing maintained signatures inside end and
  constructs become suppressed on that same activation boundary.
- This gives nearby bodies one honest escape move without turning the field
  into an 800 ms charge.

### 2. Target Paint — acquire before the bonus exists

- Add a one-tick tracked acquisition.
- Resolve after response movement, before attack delivery.
- Keep target identity, range 7, duration 8, three enhanced hits, +1 bonus, and
  cooldown 16.
- Cancel if the target dies, leaves range, or breaks team visibility before
  acquisition completes.
- Commanding Sunder to move abandons acquisition and pays an 8-tick half
  cooldown. A cancelled acquisition has no lock-confirm SFX.
- No allied attack receives the bonus on the original cast tick.

### 3. Kinetic Burst — let adjacency be answered

- Keep a one-tick tell, radius 1, push 1, and cooldown 12.
- Freeze the origin and mature after response movement.
- Hold Repulsor at that origin during the charge. A commanded move abandons the
  burst and pays a 6-tick half cooldown.
- Bodies that step out are not pushed; bodies that step in are. The existing
  legal-destination and carried-Core rules remain unchanged.

### 4. Smoke Canister — make the canister real

- Add a public ballistic `in-flight` phase at 3 tiles per tick: 1–2 ticks over
  its current range 6.
- Preserve the selected landing tile. The arc passes over walls; it is not a
  damage projectile and does not create a new interception rule.
- Start the full radius-2, 10-tick smoke duration on landing rather than launch.
- Keep cooldown 18 beginning at launch, so the change does not also increase
  cast frequency.
- If Veil dies after launch, the already-thrown canister still lands, matching
  Survey Flare and Arc Toss's authored in-flight persistence.

## Why the other instant signatures stay instant

Prism Wall and Hardlight Block are reactive construction tools. Adding a full
rules tick would remove their ability to answer a shot declared in the same
turn. Their renderer contract instead shows the complete blocking silhouette
on the first active frame, so the mechanical claim is never hidden behind a
half-built visual.

Trip Node is a trap, and telegraphing its placement globally would contradict
that role. Movement already occurs before placement, preventing a newly placed
node from surprising a body that moved during the same tick.

Repair, Survey, Arc Toss, Falling Star, Hook, and Rail already have useful
channel, flight, or counterplay time. Vector Dash and Exchange need anticipation
for readability, not a new opponent response rule.

## Compatibility and proof plan

1. Introduce presence-driven grammar-3 signature definitions for only the four
   changed envelopes. Never reinterpret grammar-2 bytes.
2. Mint a new `tapline-2` profile and rules fingerprint; keep `tapline-1`, its
   stock artifacts, ratings, and hosted playlists immutable.
3. Pin event chronology for every changed signature: declared state, response
   movement, maturity, cancellation, cooldown, active duration, seeking, and
   replay round-trip.
4. Run paired deterministic Tapline exhibits to measure use rate, resolve rate,
   cancellation, displacement, enhanced hits, smoke uptime, signature denial,
   score, and match duration. Report the balance movement; do not disguise it
   as visual polish.
5. Point the concept VFX and SFX at the grammar-3 phases only after chronology
   is stable.
6. Advance the local `Current` profile to `tapline-2` as part of this approved
   batch. Deployment and mutation of any frozen hosted playlist remain outside
   the goal.
