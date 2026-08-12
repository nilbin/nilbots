# Follow and heal-service review

Date: 2026-08-12  
Branch: `codex/order-urgency`  
Status: fast in-process design review; not a hosted promotion or balance read

## Owner catches

In the earlier Tapline G3 (`e-9005`), Hunter unit 0 returned a Core and then
appeared stuck beside its reactor for roughly 40 ticks. Its medic follower was
the cause: `follow` resolved the leader's exact occupied tile. The follower's
normal movement urgency outranked the Hunter's routine shadow patrol, so allied
traffic repeatedly made the Hunter yield its own position. The oscillation
governor then correctly refused the resulting back-and-forth circuit, turning
the bad target into a long visible hold.

Later in the same replay, two injured allies sought one heal tile. Healing
requires three consecutive successful `Wait` actions. Each incoming seeker
asked the current channeler to make way, movement reset the channel, and the two
bodies exchanged the tile without either completing the service.

## Correction

`follow` now targets the deterministic legal adjacent ring around its resolved
leader. The existing posture filters that ring, so the follower still tracks
the leader without asking the leader to leave its own tile.

A hurt body already waiting on its selected heal tile owns that one-body service
point against another recovery seeker for the current tick. Later seekers
target its legal adjacent waiting ring and resume when the tile is free. This is
deliberately narrower than rooting the healer: unrelated traffic retains the
ordinary urgency and safety rules, and ownership disappears immediately when
the body leaves, changes order, or reaches full health.

No `activate` action was added. `Wait` is already the engine's physical healing
channel. Adding a sheet verb would expose executor micro without resolving the
actual traffic-arbitration defect. Reasons now make the micro visible as
`channel-heal:traffic-owner` and `service-queue:uN`.

## Decisive G3 comparison

The exact old and replacement `e-9005` replays report:

| Scene measure | Before | Candidate |
| --- | ---: | ---: |
| Hunter u0 evictions requested by follower u1, t80-125 | 39 | 0 |
| recovery seeker evictions requested by another recovery seeker | 18 | 0 |
| protected heal-channel waits | 0 | 93 |
| explicit service-queue waits | 0 | 11 |

The real-scene regression also requires at least two distinct Hunter bodies to
complete a heal pulse, so eliminating the visible swap cannot pass merely by
making every seeker wait forever.

## Exhibit

Public phone gallery:
[Follower standoff and heal-pad queueing](https://performing-holdem-inns-florida.trycloudflare.com)

G3 is sample 3 and carries the precise scene cue. The six disclosed results are
Hunter wins at ticks 470, 458, 427, 387, 381, and 433. They are observations
from an exhibit, not a balance claim.

A 390x844 Chromium smoke loaded G3, its WebGL canvas and ready play control,
then advanced playback with no page or console errors.

## Validation and promotion boundary

- focused movement-governor and real-scene tests: 39/39 passed;
- `git diff --check`: passed;
- gallery and replay generation: current source, in-process.

This fast correction did not build WASM, alter a frozen artifact, mint a
playlist, move a rating book, or run a release suite. Those remain a separate
owner-authorized promotion gate.
