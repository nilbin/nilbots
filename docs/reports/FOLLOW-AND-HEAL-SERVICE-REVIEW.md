# Coordinated-default and follower-course correction

Date: 2026-08-12
Branch: `codex/order-urgency`
Status: fast in-process design review; not a hosted promotion or balance read

## Audit correction

The first version of this report was wrong about its runtime. The exhibit and
real-scene fixture passed untagged ATL2 data, whose historical default was the
greedy stepper. They therefore did not establish behavior on coordinated
planning.

The product default is now coordinated in all four relevant layers:

- untagged mind data selects `CoordinatedArenaStepper`;
- the local Arc Relay command defaults to `coordinated`;
- the exhibit harness documents greedy as an explicit diagnostic control; and
- every real-scene regression appends an explicit coordinated option trailer.

On Tapline seed 9005, unflagged and explicit-coordinated runs both end at tick
505 with the identical replay hash
`ede228a0e97fa224286e00955fa9eadf72f8c0f01230745786b0b7a7f9ffada7`.

## What survived the coordinated audit

The follower defect is real on coordinated planning. Before the correction,
Hunter follower u1 repeatedly selected the next tile on leader u0's committed
route and requested that the leader make way nine times during ticks 0-45.

`follow` already uses the deterministic legal adjacent ring around its resolved
leader. Once both plans are available, the follower now also excludes the
leader's committed next route tile. The coordinated seed-9005 regression sees
zero follower-requested leader evictions while the leader advances under its
authored `shadow-hunt` route.

## What was removed

The proposed heal-pad ownership/queue policy was supported only by the greedy
replay. In the original coordinated seed-9005 replay, recovery seekers caused
zero recovery-vs-recovery make-way events. The policy was therefore removed
rather than promoted without product-path evidence.

No `activate` action was added. `Wait` remains the engine's physical healing
channel, while any real coordinated contention defect remains a separate scene
to reproduce before changing arbitration.

## Exhibit

Public phone gallery:
[Coordinated movement — product default](https://performing-holdem-inns-florida.trycloudflare.com)

The six matches use no `--mind-stepper` override. They are observations from an
exhibit, not a balance claim.

## Validation and promotion boundary

- four explicitly coordinated real-scene tests plus the product-default test:
  5/5 passed;
- unflagged/explicit-coordinated replay identity: passed;
- `git diff --check`: passed;
- gallery and replay generation: current source, in-process.

This fast correction did not build WASM, alter a frozen artifact, mint a
playlist, move a rating book, or run a release suite. Those remain a separate
owner-authorized promotion gate.
