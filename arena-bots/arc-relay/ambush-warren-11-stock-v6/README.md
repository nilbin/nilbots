# Frozen ambush-warren-11 stock artifact v6

This directory is the immutable first-party tactical mind for the line that
adds team-relative Well subjects to the sheet grammar. It keeps everything v5
froze — consolidated Warren rules, map, sheets, semantic intent labels, and the
deterministic executor movement policy — and adds two Well subjects the frozen
v5 build predates.

`own` is the Well nearest your own reactor and `enemy` the furthest; `centre`
keeps its name. The mind derives both from reactor distance and aliases them
into every Well-keyed fact, so `well-has-outstanding`, `own-socket-filled`,
`enemy-socket-filled` and `well-ticks-until-birth` all accept them. The compass
names are unchanged and remain correct on maps whose flank Wells are neutral
ground. Nothing about movement, arbitration, or the ladder changed.

The reason the subjects exist is the Crucible arena, where each side owns its
near Well: there, a compass name means opposite things to the two corners, and
one team-relative sheet could not say which Well it wanted.

The historical v11 artifact remains byte-exact in `ambush-warren-11-stock-v5`,
and v10 in `ambush-warren-11-stock-v4`.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build`, through the platform-matched Docker
  backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `5e2995feb56dc480be28fdb2bde66879dc3f97d0a29ea338b11aa8a30eabaad7`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical work pinned to these bytes must continue to resolve them
exactly.
