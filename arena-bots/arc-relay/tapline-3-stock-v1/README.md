# Tapline 3 stock v1

This directory freezes the Orders v2 tactical mind used by hosted Arc Relay
entrant playlist v21. It runs `wellwright-tapline-v2` on Tapline-3 with authored
movement urgency, body-local combat contact, and atomic EPIBT squad movement.
Playlist v20 and its Tapline-1 artifact remain immutable in
`tapline-1-stock-v1`.

## Frozen identity

- Artifact SHA-256:
  `8feed1cd9a43e9a6ad8266d8ef2e6760c39386a080a974684d634082bb5cf969`
- Build-input SHA-256:
  `cd4615c6712c87d9830b459bafc1835c9b9004563501969df308c056b66dc8eb`
- Artifact bytes: `5304224`
- Toolchain SDK / guest adapter: `0.10.13`
- Build pipeline: `5`
- Controlled build: `nilbots build --no-cache`, through the platform-matched
  Docker backend selected by `scripts/run-wasm-publish.sh` on macOS

On Tapline-3 seed `9001`, Wellwright Orders v2 on the west versus Hunter Orders
v2 on the east has the same gameplay-state digest in the in-process and WASM
runtimes:
`9e8455726089e56081ccfe892735c03fc414ece6aad7c0b551cc50b44103af05`.
The match ends by reactor destruction at tick 466 in both runtimes.

Playlist v21 starts a fresh rating book because the promoted map line, planner,
stock sheet, and stock gameplay differ from v20. Do not rebuild or replace
`bot.wasm`. A later behavior change must mint a new stock directory and playlist
version.
