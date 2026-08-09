# Frozen ambush-warren-11 stock artifact v3

This directory is the immutable first-party direct-ladder mind pinned by
hosted Arc Relay entrant playlist v9. Playlist v9 moves the current stock
entrant and its migrated `wellwright-v1` sheet onto the consolidated DSL while
leaving playlist v8 and `ambush-warren-11-stock-v2` byte-exact.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build`, which invokes
  `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `48d778f42e104e30611216c9e72620981e9994857a0052188ba08a866064de3f`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`
- linked-data format: `ATL1` direct ladder

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and playlist
version. Historical playlist v9 work must continue to resolve these exact
bytes.
