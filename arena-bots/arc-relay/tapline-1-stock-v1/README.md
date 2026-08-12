# Tapline 1 stock v1

This directory freezes the first Orders v2 tactical mind used by hosted Arc
Relay entrant playlist v20. It runs `wellwright-tapline-v2` directly from its
ordered role trees; no ATL1 doctrine/custody IR is generated or interpreted.
Playlist v19 and its ATL1 artifact remain immutable in
`ambush-warren-11-stock-v12`.

## Frozen identity

- Artifact SHA-256:
  `d9281f6abee1f5f5e3eeebbdc911dac091138199dea91feb7a8a0812d1549b88`
- Build-input SHA-256:
  `76917d1516cce340d90cb29433b0163a5028b6f53d00e36c35feb9fceb856d81`
- Artifact bytes: `5006467`
- Toolchain SDK / guest adapter: `0.10.13`
- Build pipeline: `5`
- Controlled build: `nilbots build --no-cache`, through the platform-matched
  Docker backend selected by `scripts/run-wasm-publish.sh` on macOS

On Tapline seed `9001`, Wellwright Orders v2 on the west versus Hunter Orders
v2 on the east has the same gameplay-state digest in the in-process and WASM
runtimes:
`0aaccd6ed28a00ecfbc6d401915b0834b99c5ea9bb0116d7cedaf689f9cdc947`.
The match ends by reactor destruction at tick 418 in both runtimes.

Playlist v20 starts a fresh rating book because the executable DSL, stock
sheet and stock gameplay all changed. Do not rebuild or replace `bot.wasm`.
A later behavior change must mint a new stock directory and playlist version.
