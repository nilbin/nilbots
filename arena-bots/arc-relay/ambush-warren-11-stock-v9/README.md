# Frozen ambush-warren-11 stock artifact v9

This directory is the immutable first-party tactical mind for hosted entrant
playlist v15. It retains carrier clearance and persistent authored-route
courses from stock v8, then gives an occupied held course a bounded escape:

- after the authored stuck window, transient occupancy may trigger a local
  detour within the route's corridor budget;
- a detour rejoins a later tile of the exact held terrain course without
  reselecting direction, waypoint, or lap;
- an occupied waypoint at the end of that course may advance only to a later
  waypoint in the same authored direction; and
- a failed bounded attempt keeps waiting and says so in its causal reason.

The authoritative generic actor movement gate is also wall-corner strict: a
diagonal requires both orthogonal companion tiles to be open terrain. Body
occupancy is deliberately not part of that companion check. The map and rules
fingerprints remain the registered ambush-warren-11 values; the shared geometry
audit separately records that this historical map contains corner-touching
floor pairs and is not retroactively re-authored here.

The registered Wellwright-vs-Wellwright seed-9001 stock cell changed gameplay
state from v14 digest
`4afb253b831b8d50a2cd04591eedd8b56393961c4d13ba6e72d0656fa5150c5f`
to v15 digest
`c356c5882fcd86c3646365b15147f18d8c8b494b33bc7e5b3a505f6ab91068a6`.
Playlist v15 therefore starts a fresh rating book rather than carrying v14
ratings. In-process and sandboxed-WASM execution produce that same gameplay
digest.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build --no-cache`, through the platform-matched
  Docker backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `4443b18bf718114db764a440604c7987751a7ad1c28fbe092862b4d4a7fa8af5`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical work pinned to v1-v8 must continue to resolve those bytes
exactly.
