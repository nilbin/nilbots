# Frozen ambush-warren-11 stock artifact v4

This directory is the immutable first-party tactical mind pinned by hosted
Arc Relay entrant playlist v10. Playlist v10 gives direct-ladder intents
semantic spectator names while preserving the v9 gameplay-state behavior and
leaving `ambush-warren-11-stock-v3` byte-exact.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build`, through the platform-matched Docker
  backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `c2b2f0a92f34d2d006bd4e7ecf8d68d0750df89300db7e23f33ef111d793a9a0`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical playlist v10 work must continue to resolve these exact
bytes.
