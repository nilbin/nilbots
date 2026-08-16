# Arc Relay Tactical Programs and signature opportunities

Date: 2026-08-16  
Branch: `main`  
Status: implementation complete; owner UX/gameplay review pending

Public six-cell gallery:
<https://beer-researchers-newer-panel.trycloudflare.com/>

## Outcome

The sheet workshop now treats **Programs** as the normal map-first authoring
surface and **Instructions** as the advanced generated explanation. Program
order is the only player-facing priority order. For each role, materialization
filters that ordered Program list to the Programs containing the role and
flattens its participant Instruction blocks in the same order. A participant
with no executable Instruction falls through to the next Program; after the
Program band, existing unlinked Instructions remain the advanced compatibility
fallback.

Programs remain editor-only linkage over ordinary `arc-relay-orders-v2`
source. No Program id, phase, reservation, scheduler, runtime state, replay
field, or second priority model was added.

The same generic Program surface now covers the primary **Mid Overload Fork**
acceptance scenario: two drawn ambush areas, a drawn Kestrel route, 2/5/1
participant contingents, one shared response trigger, independent or combined
readiness, participant-specific commitment timeouts, and positional signature
opportunities. There is no hard-coded Mid Overload recipe.

## Conditions are derived from tactical choices

Normal Program authoring does not ask the player to wire Condition ids. It
derives ordinary, visible Conditions from the choice that needs them:

| Player choice | Materialized source |
| --- | --- |
| enemy count and watched map area | one Program-owned shared trigger Condition, applied to every configured participant |
| participant stages in an area with N bodies | one role-qualified `role-in-zone-count` readiness Condition |
| several participants must be ready together | one flat `all` expression containing the shared trigger plus every selected role's readiness Condition, written identically to every selected participant Instruction |
| signature activation area | one Program-owned `self-in-zone` Condition linked to the ordinary `use` Instruction |
| signature needs its public tell/channel to finish | one `signature-commitment-active` Condition and ordinary `move.to = current-position` continuation |
| commitment duration | the participant response Instruction's ordinary `timeout` field |
| failure, timeout, or no executable child | ordinary fallthrough to the next Program containing the role; no hidden state |

The exact generated Conditions are visible and editable under **Advanced ·
exact source derived from the tactical choices above**. Raw Condition authoring
therefore remains an escape hatch, not the expected workflow. The optional
explicit `until` Condition is also behind **Advanced explicit exit**.

The focused mixed-role test constructs a 5/5 return ambush from four roles with
requirements 1 + 1 + 1 + 2. It proves that the editor creates four visible
role Conditions plus one shared trigger and writes the same five-id `all` gate
to all four participant Instructions. Removing the readiness relationship
returns every role to its independent trigger-plus-own-readiness gate.

Natural-language outcome exits such as “four enemies destroyed” and “Core
acquired” remain the explicitly accepted follow-up. No hidden approximation
was introduced for this milestone.

## Signature opportunity architecture

- One shared authoring catalog covers all 16 launch classes and projects
  compiler-valid target, spatial, timing, and preset controls into both the
  App catalog and web editor.
- A linked signature opportunity is an ordinary `use` leaf placed above its
  ordinary continuation. An unavailable or illegal use falls through in the
  same tick; it never becomes a compound move-and-use action.
- `route-forward` provides deterministic, route-aware forward targeting for
  movement signatures. The focused scenes pin straight-span selection,
  blocked/bent/ambiguous decline reasons, minimum gain, and forward cursor
  reconciliation after relocation.
- Commitment handling observes the authoritative signature lifecycle rather
  than copying duration constants into the Program. Higher-priority ordinary
  Instructions may still pre-empt it.
- Presets for all 16 signatures are data-only suggestions through the same
  generic editor. Removing a preset does not remove generic authoring.
- Map ownership, rename propagation, detach, removal, drift checks, undo/redo,
  save/copy/reload, and Practice correlation continue to operate on ordinary
  Instructions and map-plan source.

## Production browser proof

The production Docker `runtime` image was built and served against the real
local PostgreSQL volume. The persisted acceptance sheet is:

- sheet: `Mid Overload Fork`, revision 2
- id: `f89cb9be-2380-4519-be21-4385fd096f75`
- account: `Mid Overload QA`
- persisted structure: 2-body Mason ambush, 5-body main force, immediate
  Kestrel route, shared four-enemy trigger, independent 2/2 and 5/5 readiness,
  wall and Dash opportunities

On a clean browser origin, the normal controls changed the two area
contingents into one visible **Return ambush · 7/7 ready** relationship. This
was an unsaved local refinement of the persisted source, deliberately leaving
the PostgreSQL revision untouched. The generated Advanced surface then showed
five editable Conditions: response start, both readiness Conditions, and the
two positional signature Conditions. There were no browser console warnings
or errors.

- [Desktop: map-first Program and combined readiness](assets/program-derived-readiness-desktop.png)
- [Phone 390×844: combined readiness](assets/program-derived-readiness-phone.png)
- [Advanced: generated Conditions remain editable](assets/program-generated-conditions-advanced.png)

The 390×844 view retained the sticky Scenario / Practice controls, required no
hover interaction, and exposed both selected participants plus the exact 7/7
relationship without horizontal page overflow.

## Focused evidence and validation

The implementation landed as these phase commits:

| Commit | Purpose |
| --- | --- |
| `bafeea01` | shared all-class signature authoring catalog |
| `76befe28` | linked signature opportunities |
| `5cc8564c` | route-aware signature continuation |
| `2c4c155d` | generic map-first contingents |
| `d63e8d15` | all-16 data-only presets |
| `9811a72d` | Practice evidence correlation |
| `5d41c5d4` | production App review separated from the WASM toolchain |
| `77b56c60` | map-authored signature targets |
| `e4b1426c` | generated ids removed from player copy |
| `45995c7c` | focused route and commitment scenes |
| `edbad3d0` | grammar and architecture documentation |
| `1e42b3d3` | Program/Instruction priority model |
| `fccd7fd2` | final player-facing vocabulary sweep |
| `9cde9f01` | direct trigger and combined readiness derivation |

Validation at `9cde9f01` before this report:

- focused web authoring/browser-source tests: 32/32 passed;
- full web suite: 586/586 passed;
- focused compiler/movement/signature scene tests: 81/81 passed;
- `cd web && npm run build`: passed;
- `bash scripts/generate-api-clients.sh`: zero diff;
- production Docker `runtime` target: passed and did not invoke clang, wasi,
  `build-wasm-guest.sh`, or the controlled compiler image;
- production browser smoke: persisted sheet loaded from PostgreSQL, Program
  refinement and Advanced Conditions worked at desktop and 390×844, console
  warnings/errors 0.

No first-party WASM artifact was built, no frozen artifact or playlist/rating
era was minted, and no release workflow ran. That is intentional under the
tracked fast design-review policy: hosted first-party sheets execute the
registered trusted mind in-process, while the controlled WASM path belongs to
an explicit deployment/custom-mind checkpoint.

## Six-cell owner exhibit

The delivered gallery is `lockbox-v1` versus `wavebreak-v1` on the current
`arc-relay-tapline-07` rules and `arc-relay-tapline-02` map, trusted in-process
runtime, in the registered cell order. The generated sample manifest contains
only this exact pairing. All six replays passed the tracked movement sanity
gate and felt-degeneracy eligibility, and all six ended by reactor destruction.

| Cell | Recorded result |
| --- | --- |
| `w-9001` | Wavebreak won, reactor destroyed at t236 |
| `w-9002` | Wavebreak won, reactor destroyed at t229 |
| `e-9005` | Wavebreak won, reactor destroyed at t349 |
| `e-9009` | Wavebreak won, reactor destroyed at t260 |
| `w-9012` | Wavebreak won, reactor destroyed at t241 |
| `e-9004` | Wavebreak won, reactor destroyed at t270 |

These are six behavior records, not a balance sample and not evidence that
Wavebreak is stronger in the population.

Several candidate pairings were rejected before gallery construction because
the mandatory motion gate found an existing local orbit or brief loaded-return
reversal. They were not rejected or retained based on match outcome, were not
served, and were not “fixed” by weakening or skipping the gate. This work did
not expand into unrelated pathfinding or sheet tuning. The final Lockbox /
Wavebreak pairing was the first current seven-class pairing in this review
sequence whose complete standard set cleared both mandatory gates.

Detached delivery processes at report time:

- `nilbots-programs-review-server-9380`
- `nilbots-programs-review-tunnel-9380`

The public index and `sample-01` replay payload both returned HTTP 200 after a
later process check.

## Remaining limits

- Natural-language outcome exits are deferred as owner-approved.
- Combined readiness is deliberately a flat `all` gate and respects the
  existing eight-Condition expression limit. Arbitrary nested condition logic
  was not introduced.
- Readiness links Conditions; it does not make participant actions atomic or
  reserve bodies at runtime.
- The first persisted Mid Overload practice run predates the combined-readiness
  refinement and did not produce a trigger episode. The browser and pure-source
  proofs establish authoring/materialization, not that this one sheet is
  balanced or that its tactic succeeds against every opponent.

