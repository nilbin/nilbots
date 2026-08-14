# Tapline 4 stock v1

This directory freezes the Orders v2 tactical mind used by hosted Arc Relay
entrant playlist v22. It runs `tempered-standard-v1` on Tapline-4: the promoted
product line combining team-relative Core value, veteran command, public
respawn waves, seven-class roster variety, durable deployments, and the
combat-magnitude scale with Tapline-3's coordinated movement.

## Frozen identity

- Artifact SHA-256:
  `4f2fbab6bce9565d936a22dd0d80a1533fda0d438835e0a5030496cf2445992d`
- Build-input SHA-256:
  `2b1f32c1dc1459923507d38ffe3993802c1c1dfe7c4e44c2bbf945254966e5e4`
- Artifact bytes: `5602870`
- Toolchain SDK / guest adapter: `0.10.13`
- Build pipeline: `5`
- Controlled build: `nilbots build --no-cache`, through the platform-matched
  Docker backend selected by `scripts/run-wasm-publish.sh` on macOS

On Tapline-4 seed `9001`, Tempered Standard versus itself ends by reactor
destruction at tick 333 in both trusted in-process and WASM runtimes. Their
gameplay-state digest is identical:
`57ef92f429616d2ac6e5dcc9b1c321636eedf7056080b459c4c03aae714afbd7`.

Playlist v22 starts a fresh rating book because the promoted rules, stock
sheet, artifact, and stock gameplay differ from v21. Do not rebuild or replace
`bot.wasm`. A later behavior change must mint a new stock directory and
playlist version.
