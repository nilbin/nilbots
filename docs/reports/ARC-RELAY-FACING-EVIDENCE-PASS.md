# Arc Relay destination-facing and observed-evidence pass

Date: 2026-08-15  
Branch: `main`  
Starting checkpoint: `abd8ba9963184f39128c115f571e377ed6127286`

## Outcome

The last two items in the Tapline depth brief are now ordinary product
features of `arc-relay-orders-v2`. No candidate profile, alternate ruleset,
compatibility wrapper, playlist, or experiment-only sheet was added.

- A `move.to` aimed at an authored zone or anchor may also name one authored
  `face` anchor. Travel remains unchanged. After arrival, the body spends an
  ordinary rotation action only when the anchor has left its legal forward
  quadrant, then holds its position and facing.
- Four finite historical conditions expose only evidence the team actually
  observed: enemy entries into a zone, distinct observed enemy lives in a
  zone, confirmed destructions in a zone, and uninterrupted vision over every
  traversable tile of a zone.
- Every evidence query uses exactly one bounded interval: a rolling window,
  the match so far, or the active order episode. It ends before the current
  tick's actions, survives individual body deaths, and resets between matches.
  `since: node-entry` is legal only from that node's `until`.
- Canonical debug reasons name the exact evidence interval and result. The
  compact spectator broadcast deliberately publishes only the resulting
  authored order/action plus independent travel and watched-anchor targets; it
  does not leak the private evidence ledger.
- The selected-body path lens keeps travel cyan and draws the watched anchor
  separately in amber in both Canvas2D and WebGL.

The compiler, direct executor, editor, map preview, hosted broadcast projector,
gallery projector, replay ingress, both renderers, product docs, and tests all
use the same words and constraints.

## Product-sheet adaptations

### Demolition March

`demolition-march-v1` now demonstrates authored facing without changing its
central thesis. Its overwatch and reserve bodies occupy a plotted centre post
and watch the enemy centre approach. Only two demolition-line bodies may claim
the defended enemy Well at once; the remaining line holds centre overwatch
instead of orbiting one destination.

### Lockbox

`lockbox-v1` is now a genuine anti-column response rather than a renamed
general-purpose sheet:

- roster: Mason, Palisade, Mortar, two Repulsors, Hush, Patchbay, and Longshot;
- trigger: at least three observed entries or four distinct observed enemy
  lives in the centre pressure gate during the last 75 ticks;
- response: up to six available empty-handed bodies interrupt optional loose-
  Core collection, enter a spacious centre killbox, and face the threat axis;
- kit: Prism Wall, Falling Star, twin Kinetic Bursts, Null Field, Repair Beam,
  and Rail Line all remain role-owned signature orders;
- release: three confirmed enemy destructions during that response, or a
  120-tick timeout, followed by a 12-tick cooldown and the role's ordinary
  scoring/deployment ladder;
- safety: one loaded body gets the reactor lane while other loaded bodies wait
  in a plotted approach zone, and the Mason's deployment attempt is a bounded
  16-tick episode with a 12-tick retry cooldown.

This is deliberately authored in the sheet. The executor supplies observation
truth, deterministic history, facing, and normal movement micro; it does not
recognize “Demolition” or invent the counter.

## Centre-pressure read

The accepted standard six-cell exhibit is:

| cell | west | east | result |
| --- | --- | --- | --- |
| w-9001 | Lockbox | Demolition | Lockbox, tick 310 |
| w-9002 | Lockbox | Demolition | Demolition, tick 203 |
| e-9005 | Demolition | Lockbox | Lockbox, tick 350 |
| e-9009 | Demolition | Lockbox | Demolition, tick 190 |
| w-9012 | Lockbox | Demolition | Demolition, tick 308 |
| e-9004 | Demolition | Lockbox | Demolition, tick 119 |

All six cells fired the historical response, used the anti-column signature
kit, emitted facing holds, completed by reactor destruction, passed the tracked
felt-degeneracy bars, and passed movement sanity with zero closed cycles,
recovery no-progress episodes, false movement traces, or local movement
orbits. Compressed broadcasts are 58,961–143,303 bytes each and 620,173 bytes
in total, below the 300 KiB per-match and 8 MiB gallery budgets.

This is an exhibit, not a balance sample. It establishes that a sheet can
observe the centre column, mobilize a coordinated response, release it, return
to its baseline plan, and win a game from either map side. It does **not** show
that the matchup is even: Demolition won four of these six. Direct centre
pressure therefore remains a credible meta risk. The next honest question is a
separately owner-called population read with multiple centre, split-lane, deep-
raid, and anti-column authors—not another rule reaction based on six games.

Public phone gallery:
`https://database-asset-dsc-ports.trycloudflare.com`

## Determinism and non-effects

- Repeating the final e-9005 cell produced canonical replay hash
  `a2ef0371154bbf3314b91f52259ddc4723908fd784d47dd4a5d8939d2c4656a0`
  and gameplay-state digest
  `7ffb6136dfe8dfef07a7461d6917b1e43ce50c88a9d18631f1889298a4cb0ce0`
  byte-identically both times.
- A source-at-starting-checkpoint versus current-source comparison of the
  untouched `tempered-standard-v1` mirror at Tapline-4 seed 9001 produced the
  same tick-267 result and gameplay-state digest
  `768847e85657d3bbf8b6ff62311812f51d500e9e882ef04f221c87bfc8b8d3b6`.
  Replay hashes differ because the participant artifact/provenance hash is
  intentionally part of the canonical document; gameplay does not.
- No game rules, map, contract-owned fingerprint, replay schema, frozen
  artifact, playlist, rating book, or release version changed. Compact format-2
  broadcasts gained additive presentation columns for order, travel target,
  and facing target.
- Per the current fast-loop ruling, this design merge uses trusted in-process
  execution. It does not rebuild the first-party WASM mirror or mint deployment
  identities; that remains an explicit deployment-checkpoint responsibility.

## Verification

- `dotnet build BotArena.sln --no-restore` — pass, zero warnings/errors.
- compiler suite focused on `ArcRelayTacticalPlaybookCompilerTests` — 33/33.
- `ArcRelayTacticalPlaybookMind.Tests` — 24/24.
- `ArcRelayBroadcastIntentTests` — 3/3.
- `ArcRelayPlaylistDefinitionTests` — 7/7.
- `cd web && npm test` — 555/555.
- `cd web && npm run build` — pass.
- Chrome-controlled production-like site-review smoke — pass at desktop and
  390×844: plotted zones expose facing anchors, historical conditions expose
  all three interval choices, and the phone editor remains touch-accessible.
- `git diff --check` — pass.

An unfiltered CLI-suite attempt also exposed two pre-existing review-stage
fixture failures unrelated to this pass:
`ArcRelaySignatureStageCommandTests.FocusedStageWritesShortCompleteCompactReplay`
still fields only six distinct classes under Tapline-4's seven-class minimum,
and `ArcRelayCoreStageCommandTests.FocusedStageWritesEightBodyCustodyChoreography`
constructs an invalid `minimumDistinctClasses` value. Neither command, fixture,
nor test was changed here; the product compiler tests are green.
