DECISION NEEDED: none.

RESULT: **Arc Relay H0 Phase C is built.** The new experimental ruleset runs
the approved Threefold map with eight bodies per team, direct sheet selection
from unlocked launch classes under the two-copy cap, stable Cores, Wells,
reactors, Pulses, respawn, all sixteen signature envelopes, typed mind
observations, replay-v3 chronology validation and summary counters. Canvas2D
ships with the mechanic and resolves sixteen distinct class-default SVGs plus
the shared Arc Pulse projectile. No stock mind, native doctrine, balance
finding, or claim of fun is included.

# Phase C — Arc Relay H0 build report

## Evidence

### Immutable contract and artifact identities

The two evidence replays carry the same contract and presentation identities:

| Surface | Identity |
| --- | --- |
| Engine | `1.0.0` |
| Replay | format `3`; contract schema `2` |
| Rules / game-rules version | `arc-relay-h0-01` |
| Mode | `arc-relay` / `arc-relay-h0` |
| Rules fingerprint | `f6d3ee9b1bb17d7bd8d0981941fd00a6a96f0e7ef834497d11924c06087174eb` |
| Map | `arc-relay-threefold-01` version `1`; map format `3` |
| Map fingerprint | `f4649d6d17e80c02cd4b09fec849de5729435855cf411592a5e2c73cd889bbbe` |
| Topology fingerprint | `30ae5652c87744625b21079a2cb96e8dcb74f201243f5a62bacfa5fea120302c` |
| Match-contract fingerprint | `5ad89a9d17a2d897bacd0ec6f66ab5c8162319389f2aac91c2bcee8031fb83f0` |
| Format fingerprint | `dc81a4f285ada9baceba99751e2de2ede8247cd943ad5c2164368c2f55129463` |
| Mind profile | `generic-mind-match-1`; runtime protocol `1.0`; runtime configuration `2.0` |
| Presentation | `ember-forge`; sixteen authored form mappings; shared `arc-pulse` |

The launch sheet does **not** contain a five-class stable. It fills all eight
slots directly from the player's unlocked launch classes, with one or two
copies of a fielded class. `roster-25-stable` remains only a registered future
response beyond roughly 25 classes, or an explicit draft/tournament lever.

### Build and regression gauntlet

| Suite | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: |
| BotArena.App.Tests | 181 | 77 | 0 |
| BotArena.Cli.Tests | 110 | 0 | 0 |
| BotArena.Determinism.Tests | 17 | 0 | 0 |
| BotArena.Engine.Tests | 1,349 | 0 | 0 |
| BotArena.Guest.Tests | 36 | 0 | 0 |
| BotArena.Runtime.Wasm.Tests | 67 | 0 | 0 |
| BotArena.Sdk.Tests | 84 | 0 | 0 |
| Web Node tests | 351 | 0 | 0 |
| **Total** | **2,195** | **77** | **0** |

`dotnet build BotArena.sln --no-restore` completed with zero warnings and zero
errors. `npx tsc -b --pretty false` and `npm run build:cli` also completed.
The 77 App skips are the repository's existing external integration gates; no
executed test failed. The Engine chronology tests assert the Section 4.1 order,
handoff's same-tick relocation, Core recovery, in-flight pickup exclusion,
death drops before banking, pending-Well rearm state, and rejection of forged
Core chronology. The SDK and WASM round trips cover the new absolute
`position-target` argument. A DocDrift test binds the approved launch roster,
signature list and map identity back to the Gate 2 brief.

The frozen Duel and Frontline generations remain covered by their existing
goldens and the 17-test determinism suite. No old rules identity, replay
version or presentation package was re-minted.

### Counted smoke replay files

The engineering smoke uses the real `generic-mind-match-1` participant shape
with an in-process mechanic exerciser. It is deliberately **not** a stock mind
or Phase D doctrine. The evidence directory contains exactly two replay files:

| Seed | Core mechanics exercised | Respawns | Other signatures exercised |
| ---: | --- | ---: | --- |
| 17 | 14 births; 25 pickups; 183 carried relocations; 5 adjacent handoffs; 2 Arc Tosses; 1 steal; 11 banks; 3 Pulses | 44 | all 16 signature kinds attempted; 16 completed at least once |
| 29 | 13 births; 16 pickups; 154 carried relocations; 4 adjacent handoffs; 3 death drops; 3 steals; 10 banks; 3 Pulses | 55 | 14 signature kinds attempted |

Together they exercise Core birth, pickup, carry, adjacent handoff, Arc Toss,
death drop, steal, bank, Pulse and automatic respawn. Beyond Arc Toss, the
evidence contains Vector Dash, Prism Wall, Tractor Hook, Repair Beam, Survey
Flare, Falling Star, Trip Node, Null Field, Exchange, Rail Line, Hardlight
Block, Target Paint, Kinetic Burst, Smoke Canister and Sentinel Seed. This is
mechanic coverage only; the matches ended at the 600-tick horizon and support
no balance or fun conclusion.

| Replay | Canonical replay hash | File SHA-256 | Bytes |
| --- | --- | --- | ---: |
| `sandbox-arc-relay-smoke-evidence/seed-17/replay.json` | `cef3f59f055ed8f5a801f9e9135a96b80b08a63945463a7546bc0611e346b7ac` | `1648640ba840ec2dca0b4d3f0ef46ca9874cda977658ccd2eb147b810363ebdd` | 137,742,720 |
| `sandbox-arc-relay-smoke-evidence/seed-29/replay.json` | `9e93a1e2b0d718520e127507f0cb124e3132034feb84505ae408575e6407d64c` | `755d32b2c8a41cc72a10a683adc2d0a96e74cd1842cf00ead349578a560d4fe3` | 132,693,558 |

Both files pass `nilbots verify`: canonical replay-v3 content, embedded
contract, causal chronology and stored hash verify. They are local ignored
evidence rather than a 270 MB source commit. Reproduce and inspect them with:

```sh
dotnet src/BotArena.Cli/bin/Debug/net10.0/botarena.dll \
  experiment arc-relay-h0-smoke \
  --out sandbox-arc-relay-smoke-evidence
dotnet src/BotArena.Cli/bin/Debug/net10.0/botarena.dll \
  verify sandbox-arc-relay-smoke-evidence/seed-17/replay.json
dotnet src/BotArena.Cli/bin/Debug/net10.0/botarena.dll \
  replay sandbox-arc-relay-smoke-evidence/seed-17/replay.json --summary
```

### Canvas2D and self-contained viewer proof

`npm run build:cli` rebuilt the theme-scoped viewers, and `nilbots replay`
embedded the exact seed-17 file into
`sandbox-arc-relay-viewer/viewer.html`. Headless Chromium loaded that viewer
with one live Canvas, all sixteen fielded class names present, and no page or
console errors. The reviewed active frame is retained locally as
`sandbox-arc-relay-viewer/tick-80-final.png` (the viewer advanced to tick 86
while the proof was captured).

The Canvas grammar renders:

- three Wells with schedule/pending/rearm state;
- loose, carried and in-flight Core glyphs, carrier beams and recovery rings;
- reactor charge pips, damage and Pulse presentation;
- tells, fields, constructs, marks, suppression and signature movement;
- exact team accents, selected-mind fog and respawn state;
- genuine manifest-resolved class sprites rather than generic bodies.

The parked WebGL viewer still compiles but has no Arc Relay mechanic or GLB
extension, as required by DECISIONS #196.

### Sprite manifest

Every package contains `look.json` plus a genuine canonical East-facing SVG,
is discovered through the existing class-look manifest, uses direct semantic
team-accent surfaces, and maps to the shared `arc-pulse` projectile:

| Class | Default look package |
| --- | --- |
| Kestrel | `web/src/assets/class-looks/arc-kestrel/` |
| Palisade | `web/src/assets/class-looks/arc-palisade/` |
| Towline | `web/src/assets/class-looks/arc-towline/` |
| Patchbay | `web/src/assets/class-looks/arc-patchbay/` |
| Lantern | `web/src/assets/class-looks/arc-lantern/` |
| Mortar | `web/src/assets/class-looks/arc-mortar/` |
| Minesmith | `web/src/assets/class-looks/arc-minesmith/` |
| Hush | `web/src/assets/class-looks/arc-hush/` |
| Relay | `web/src/assets/class-looks/arc-relay/` |
| Switchback | `web/src/assets/class-looks/arc-switchback/` |
| Longshot | `web/src/assets/class-looks/arc-longshot/` |
| Mason | `web/src/assets/class-looks/arc-mason/` |
| Sunder | `web/src/assets/class-looks/arc-sunder/` |
| Repulsor | `web/src/assets/class-looks/arc-repulsor/` |
| Veil | `web/src/assets/class-looks/arc-veil/` |
| Nest | `web/src/assets/class-looks/arc-nest/` |
| Shared basic projectile | `web/src/assets/class-projectile-looks/arc-pulse/` |

`scripts/build-arc-relay-class-art.mjs` is the deterministic art source. The
nilbots visual-assets skill now records the large-roster method: one shared
brief, gameplay-scale silhouette first, canonical SVGs, restrained semantic
team inlays, a two-team contact sheet, manifest defaults, a shared projectile
when mechanics are shared, and entitlement/store packages for later alternate
skins. The 3D/provider path is explicitly dormant for this experiment.

## H0 deviations

None identified. Every numeric H0 value and typed boundary in the approved
Gate 2 brief was implemented. The later owner steer removing the launch
stable is incorporated in the Gate 2 brief, commander-mode design, campaign
handover, sheet validation and tests; it changes no combat or objective H0
number.

## Next

Phase C stops here. Phase D may separately author stock mind v0, sheet
doctrine and the depth audit, then produce galleries for the owner's felt-
experience gate. This report does not pre-approve that work and does not
convert mechanic coverage into a balance or fun claim.
