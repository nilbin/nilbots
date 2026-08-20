# Tapline 11 stock v1

This append-only directory freezes the Orders v2 tactical mind used by hosted
Arc Relay entrant playlist v28. It runs the existing Tempered Standard stock
sheet on Tapline-11, whose player-visible changes are compact 3/4/6 gun ranges,
five-tile vision, one shared Combat Strafe movement grammar, and explicit
swift/standard/deliberate movement rates.

The tactical mind also closes inside a windup-safe firing margin before a Hold
defender shoots a fleeing Core carrier. That behavior belongs to v28's artifact
identity; historical Tapline-4 through Tapline-10 ladders keep using the frozen
`tapline-4-stock-v1` bytes.

## Frozen identity

- Artifact SHA-256:
  `48154fbab28f48d58435f9251efe16ed992f1bcf60564603ffd6555fde665a2b`
- Build-input SHA-256:
  `b7b1d783612973f51f93b7c8b43e3ae8152eb70f0bc6caf4c2b42d024d2a66e2`
- Artifact bytes: `6298290`
- Toolchain SDK / guest adapter: `0.10.15` / `0.10.13`
- Build pipeline: `5`
- Controlled build: `nilbots build --no-cache`, through the platform-matched
  Docker backend selected by `scripts/run-wasm-publish.sh` on macOS

Do not rebuild or replace `bot.wasm`. A later behavior or contract change must
mint a new stock directory and playlist version.
