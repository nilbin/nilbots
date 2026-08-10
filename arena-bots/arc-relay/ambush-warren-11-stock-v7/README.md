# Frozen ambush-warren-11 stock artifact v7

This directory is the immutable first-party tactical mind for hosted entrant
playlist v13. It retains the consolidated direct-ladder DSL, semantic intent
labels, bounded movement policy, and relative-Well subjects from stock v6, and
adds a deterministic persistent course for authored route traversal.

An authored patrol now keeps its chosen course around durable terrain until its
rung, route, lap, waypoint, or reachable course genuinely changes. A transient
body or projectile remains traffic rather than silently selecting a new side of
the route. Reasons expose `route-commit`, `route-held`, `route-advance`,
`route-rejoin`, and `route-replan-impossible` so every wait or move remains
causal in traces.

The registered Wellwright-vs-Wellwright seed-9001 stock cell changed gameplay
state from v12 digest
`c763b012c0b94077d661723a5036b361e7c18b1ea386c76c70c86ad89bc526a6`
to v13 digest
`b7e974028d1235dced3f10845527eaade0b7b44018757391f9f3beb77368e702`.
Playlist v13 therefore starts a fresh rating book rather than carrying v12
ratings across changed executor behavior.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build`, through the platform-matched Docker
  backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `6dea59a7ee39d6291e6280f7f15c9dd8ed9706d377e7d3aad62b88336dd54abd`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical work pinned to v1-v6 must continue to resolve those bytes
exactly.
