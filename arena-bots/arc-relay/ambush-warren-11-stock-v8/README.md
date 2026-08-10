# Frozen ambush-warren-11 stock artifact v8

This directory is the immutable first-party tactical mind for hosted entrant
playlist v14. It retains the consolidated direct-ladder DSL and the bounded,
progress-aware movement policy from stock v7, then fixes allied traffic around
a loaded carrier:

- transient body occupancy can no longer turn delivery stagnation into proof
  that the reactor is unreachable;
- an authored unreachable fallback is a bounded episode and the objective is
  re-tested;
- the carrier's existing right-of-way request propagates through a packed
  allied chain to the nearest deterministic vacancy; and
- a reactor tile is reserved only when it is the carrier's actual next step,
  rather than from anywhere on the map.

The movement plane remains the sole displacement authority. Cascading
clearance is deterministic (shortest chain, unit-id tie-break), never assumes a
simultaneous swap, and emits `make-way:chain-<depth>:u<carrier>` reasons. A
closed occupied component with no legal vacancy is left untouched.

The registered Wellwright-vs-Wellwright seed-9001 stock cell changed gameplay
state from v13 digest
`3cd9cfdb58e758eb5aa2437e64c7f718e8ddb9a84502cbb5a73852cdcfeb0496`
to v14 digest
`4afb253b831b8d50a2cd04591eedd8b56393961c4d13ba6e72d0656fa5150c5f`.
Playlist v14 therefore starts a fresh rating book rather than carrying v13
ratings across the carrier right-of-way change.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build --no-cache`, through the platform-matched
  Docker backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `08205cfdb2da34cbd894a51be89c4b638e6cc40ca16020741602d4739fb2344a`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical work pinned to v1-v7 must continue to resolve those bytes
exactly.
