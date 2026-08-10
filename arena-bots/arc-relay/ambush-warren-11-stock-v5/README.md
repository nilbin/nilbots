# Frozen ambush-warren-11 stock artifact v5

This directory is the immutable first-party tactical mind pinned by hosted
Arc Relay entrant playlist v11. Playlist v11 keeps the consolidated Warren
rules, map, sheets, and semantic intent labels, and adds the deterministic
executor movement policy: bounded recovery episodes, allied-traffic tickets,
and a short oscillation circuit breaker. The historical v10 artifact remains
byte-exact in `ambush-warren-11-stock-v4`.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build`, through the platform-matched Docker
  backend selected by `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `50e932fe5a7e79791bfdc9959c868cb58f06925a73c170d4ce105c636dd0a060`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical playlist v11 work must continue to resolve these exact
bytes.
