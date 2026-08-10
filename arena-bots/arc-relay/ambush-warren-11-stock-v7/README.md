# Frozen ambush-warren-11 stock artifact v7

This directory is the immutable first-party tactical mind for hosted entrant
playlist v13. It retains the consolidated direct-ladder DSL, semantic intent
labels, bounded movement policy, and relative-Well subjects from stock v6, and
adds deterministic, progress-aware authored route traversal.

An authored patrol now keeps its chosen course around durable terrain until its
rung, route, lap, waypoint, or reachable course genuinely changes. A transient
body or projectile remains traffic rather than silently selecting a new side of
the route. A waypoint is a directional hint rather than a compulsory post:
arrival requires measured approach, the arrival ring cannot swallow its next
leg, and an unreachable or persistently stuck point yields to a later reachable
point. Reasons expose `route-commit`, `route-held`, `route-advance`,
`route-rejoin`, `route-replan-impossible`, and
`route-bypass-(unreachable|stuck)` so every wait or move remains causal in
traces.

The registered Wellwright-vs-Wellwright seed-9001 stock cell changed gameplay
state from v12 digest
`c763b012c0b94077d661723a5036b361e7c18b1ea386c76c70c86ad89bc526a6`
to v13 digest
`3cd9cfdb58e758eb5aa2437e64c7f718e8ddb9a84502cbb5a73852cdcfeb0496`.
Playlist v13 therefore starts a fresh rating book rather than carrying v12
ratings across changed executor behavior.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build`, through the platform-matched Docker
  backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `0db80102a2dbc902d2bc91dae434c729a39aa0788f8b691bc0ef3d11f5507e85`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical work pinned to v1-v6 must continue to resolve those bytes
exactly.
