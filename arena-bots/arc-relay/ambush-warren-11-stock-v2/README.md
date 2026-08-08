# Frozen ambush-warren-11 stock artifact v2

This directory is the immutable first-party tactical-playbook mind pinned by
hosted Arc Relay entrant playlist v8. It repairs v7's stale artifact without
rewriting playlist v7 or `ambush-warren-11-stock-v1`.

- source project: `../tactical-playbook-mind-v1`
- controlled build: `nilbots build ... --no-cache`, which invokes
  `scripts/run-wasm-publish.sh` on macOS
- build input: see `bot.build-receipt.json`
- artifact SHA-256:
  `072e9799933dad3c6ffc40a425c44fe0dbf7a173851761b597506b1d5136b4aa`
- stock sheet: `wellwright-v1`
- loop profile: `ambush-warren-11`

Do not rebuild or replace `bot.wasm`. Any later source, SDK, guest, compiler,
or controlled-build change must mint a new artifact directory and a new
playlist version. Historical playlist v8 work must continue to resolve these
exact bytes.
