# Veterancy Crowned Mantle — Meshy 7 handoff plan

Status: **approved direction, model-production plan only**. This document does
not approve runtime integration.

Owner direction:

- a veteran bot should read as physically more powerful, not as a bot carrying
  rank metadata;
- no numbers, stars, chevrons, medals, or tall rank stack;
- preserve the current sixteen matte Arc Relay chassis;
- build a small modular attachment kit that adapts to each chassis;
- rank one is the visual starting point, and ranks two through five reveal a
  progressively stronger silhouette;
- the maximum-rank read should approach the full-roster concept board without
  requiring sixteen replacement bots.

The numeric rank plate currently visible on `main` is a rejected prototype. It
is not a modeling reference and the new pieces must not be designed around it.
Removing or replacing that prototype belongs to the later renderer pass.

## Authoritative references

Read these before producing inputs or spending provider credits:

1. [`veterancy-crowned-mantle-full-roster.png`](./veterancy-crowned-mantle-full-roster.png)
   — approved overall silhouette and material direction.
2. [`veterancy-power-directions.png`](./veterancy-power-directions.png) — rank
   progression and alternative-language evidence.
3. [`README.md`](./README.md) — concept rationale and generation record.
4. [`FULL-ROSTER-PROMPT.md`](./FULL-ROSTER-PROMPT.md) — preserved conceptual
   prompt, useful as art language but not as a Meshy prompt.
5. [`../arc-relay-meshy7-fleet-retexture-v3/README.md`](../arc-relay-meshy7-fleet-retexture-v3/README.md)
   — approved matte Arc fleet material family.
6. [`../../../docs/ARENA-VISUALS.md`](../../../docs/ARENA-VISUALS.md) — current
   arena model, scale, material, and promotion contracts.

The local source files are authoritative. Concept images communicate shape and
feel; they are not exact orthographic model inputs.

## Exact model count

Produce **five new standalone Meshy 7 model families**:

1. `veterancy-crown-spine`
2. `veterancy-swept-blade`
3. `veterancy-square-mantle`
4. `veterancy-radial-petal`
5. `veterancy-curved-rail`

Do not produce sixteen replacement bot models, per-rank bot variants, or
preassembled class kits. The renderer will mirror, rotate, scale, and position
these five pieces using sixteen class-specific fit profiles. Do not add a sixth
piece without showing which chassis cannot be solved with this kit and getting
owner approval.

At maximum rank, the default composition is five visible elements:

- one centered crown spine;
- one mirrored inner pair;
- one mirrored outer pair.

Repeated elements reuse the same GLB. The five model files are therefore enough
to produce a broad set of silhouettes without multiplying transfer cost.

## Common modeling contract

Every model is an attachment, never a whole bot.

- One isolated object per model file. No bot body, base, floor, camera, light,
  particles, orbit, trail, or presentation geometry.
- `+X` is bot forward, `+Y` is up, and positive local `Z` points outward on the
  canonical side piece. Side pieces must mirror cleanly across `Z = 0`.
- Place the attachment root at the intended inner mounting edge. Keep the
  lowest modeled point at `Y = 0`; the renderer supplies the chassis-relative
  elevation.
- Do not bake a world position, a chassis-specific tilt, or a left/right body
  offset into the mesh.
- Keep the root zone visually plausible when floated slightly above a chassis.
  The pieces are renderer-owned hover-mounted armor, not welded replacement
  bodywork.
- Favor thick, readable planes and chamfered ends. Avoid needle tips and thin
  filigree that alias at gameplay scale.
- The full composition may enlarge a bot's top-down silhouette by roughly
  15–30%, but must not visually claim a neighboring tile.
- No rig, bones, animation clips, morphs, weapon barrels, turrets, thrusters,
  wings, drones, antennas, letters, numerals, stars, or chevrons.
- Do not modify, retexture, re-topologize, or republish any existing fleet GLB.

The following dimensions are starting envelopes, in arena tile units. They are
not permission to distort a good silhouette merely to hit a number. Record the
actual normalized bounds for every result.

| Model | Canonical use | Starting envelope | Shape brief |
| --- | --- | --- | --- |
| `veterancy-crown-spine` | centered dorsal anchor | 0.22 X × 0.16 Y × 0.12 Z | A low angular command crest with one strong forward/backward rhythm. It must remain recognizable when scaled down and must not become a central opaque blob. |
| `veterancy-swept-blade` | mirrored long-hull mantle | 0.36 X × 0.12 Y × 0.11 Z | A broad rear-swept armor blade with a clipped end and recessed inner seam. More armored fin than wing or sword. |
| `veterancy-square-mantle` | mirrored heavy-hull framing | 0.26 X × 0.15 Y × 0.16 Z | A compact squared shoulder plate, stepped and slightly canted, with enough negative space at its root to avoid reading as a solid box. |
| `veterancy-radial-petal` | round/radial chassis | 0.24 X × 0.10 Y × 0.14 Z | A low armored wedge/petal designed to rotate around `Y`. It should reinforce a circular chassis without becoming a complete orbit ring. |
| `veterancy-curved-rail` | crescent/open chassis | 0.34 X × 0.12 Y × 0.12 Z | A broken curved mantle segment that frames negative space. Never close it into a halo, selection ring, or force-field hoop. |

The crown spine is centered on `Z = 0`. The other four files are canonical
single-side pieces. Mirroring and repeated placement are renderer work.

## Material and surface direction

Match the current Arc fleet rather than inventing another faction:

- rough, aged brass on the veteran hardware;
- restrained graphite or gunmetal recesses;
- worn edges and shallow scuffs, not dirty noise over the whole surface;
- controlled warm amber inlay channels;
- no glossy plastic, chrome, wet highlights, saturated team paint, or baked
  bloom.

The final reviewed derivative should expose two visually separable regions:

1. matte hardware;
2. recessed energy inlay.

The energy region may use a neutral review emission, but the runtime renderer
will own its actual amber intensity, rank response, and bloom. Do not bake team
color or a bright aura into textures. Do not force a production texture atlas
before silhouette approval; preserve enough material separation for the later
optimization pass.

## Meshy input requirements

The full-roster board is not a valid multiview upload. Before each paid model
call, create a dedicated input set for exactly one module:

- strict top view;
- strict side view;
- front three-quarter view;
- rear three-quarter view.

All four images must show the same isolated object, proportions, cut lines,
surface language, and lighting. Submit them as four separate multiview inputs,
not as a contact sheet. Exclude bots, hands, stands, floors, labels, arrows,
dimensions, shadows that resemble geometry, and duplicated pieces.

Store and owner-review all five input sets before the first provider call. The
3D agent must preserve the exact prompt and input hashes for each task.

Use the repository's established current Meshy 7 provider workflow. Recheck the
live provider parameters, pricing, and account balance immediately before the
run; record numeric balances only. Credentials remain in the macOS Login
Keychain entry `nilbots.meshy.api` and must never be printed, logged, copied
into a command transcript, or committed.

## Class composition map

This is the first fit hypothesis for the all-roster sheet. It does not require
additional model files.

| Class | Inner pair | Outer pair | Fit intent |
| --- | --- | --- | --- |
| Kestrel | swept blade | swept blade | Keep both pairs low and rear-swept so the fast nose stays dominant. |
| Palisade | square mantle | square mantle | Frame the broad defensive mass without resembling its wall signature. |
| Towline | swept blade | square mantle | Preserve the hook-facing front and add authority mainly across the rear shoulders. |
| Patchbay | curved rail | square mantle | Keep the repair silhouette open; do not obscure its beam origin. |
| Lantern | radial petal | curved rail | Reinforce the round survey body while keeping the center readable. |
| Mortar | square mantle | swept blade | Avoid blocking the launcher and retain its fore/aft firing read. |
| Minesmith | swept blade | square mantle | Keep deployer hardware and mine cues unobstructed. |
| Hush | curved rail | radial petal | Use the lowest crown-spine scale in the roster and preserve its quiet, flat profile. |
| Relay | radial petal | curved rail | Frame the core-handling/open regions; do not create a fake carried-core ring. |
| Switchback | swept blade | square mantle | Maintain the directional exchange silhouette and clear front/back read. |
| Longshot | swept blade | swept blade | Long, low framing only; never raise pieces into the rail-line firing path. |
| Mason | square mantle | square mantle | Use architectural steps, but do not make the bot look like a spawned wall block. |
| Sunder | swept blade | radial petal | Keep target-paint emitters and the forward aim line clear. |
| Repulsor | radial petal | radial petal | Strengthen radial mass while leaving visible gaps between every petal. |
| Veil | curved rail | curved rail | Preserve its crescent/negative-space identity; no complete smoke-like sphere or halo. |
| Nest | curved rail | square mantle | Keep the nose, seed deployment path, and sentry language unobstructed. |

Every class also receives the centered crown spine. Inner and outer pairs may
use different transforms and scales of the same module. The model agent should
flag failed fits in the full-roster sheet rather than silently producing a new
class-specific mesh.

## Rank progression contract

The model files contain the maximum-detail pieces. Rank behavior is a renderer
responsibility, but the geometry must support this sequence:

| Veterancy | Persistent visual read |
| --- | --- |
| 1 | Crown spine only, low profile and restrained energy. This is the visual starting point. |
| 2 | Crown spine plus the mirrored inner pair. The first level-up is therefore immediately visible. |
| 3 | The same three elements spread/cant outward and energize; no new GLB is needed. |
| 4 | Add the mirrored outer pair for the full five-element silhouette. |
| 5 | Spread all five elements to their strongest pose and raise only the controlled inlay energy. |

The progression must remain monotonic when viewed at the real arena camera:
each level should look at least as powerful as the preceding level. No piece may
pop through the chassis as it spreads. The model agent only needs to provide
clean roots and geometry suitable for those transforms; do not author clips.

## Production stages and approval gates

### Gate A — five input sheets

Deliver all five four-view input sets and one combined sheet. Confirm:

- one consistent object across each set;
- correct axes and root intent;
- coherent shape family across all five modules;
- matte brass/graphite/amber direction;
- no forbidden rank symbols or chassis geometry.

Do not spend provider credits until the owner approves these sheets.

### Gate B — one pilot model

Generate `veterancy-swept-blade` first. It is the most useful test of whether
Meshy can preserve a small, thin-but-readable standalone attachment.

For the pilot, immediately download and preserve the raw result, then produce:

- a normalized GLB;
- six deterministic review views;
- one mirrored-pair render;
- one Kestrel and one Longshot static fit mockup;
- bounds, triangle, vertex, material, texture, and byte reports.

If the object fuses to an invented base, loses its root, becomes a fantasy wing,
or aliases at gameplay scale, stop and revise the input. Do not spend four more
calls to discover the same failure.

### Gate C — remaining four models

After the pilot is approved, generate the crown spine, square mantle, radial
petal, and curved rail. Stop after the first material/silhouette failure rather
than blind-rerolling. Any corrective retexture or extra generation needs a
separately recorded cap and owner authorization.

### Gate D — normalized model review

Deliver the five normalized candidates together, using identical lighting,
camera, and scale. Include:

- isolated six-view renders for each model;
- a fixed-orientation five-model sheet;
- mirrored-pair tests for the four side pieces;
- a maximum-rank five-element composition test;
- an all-sixteen static fit sheet using the approved current fleet meshes;
- rank 1/2/3/4/5 strips for at least Kestrel, Palisade, Lantern, Hush, Longshot,
  Repulsor, Veil, and Nest.

The all-roster sheet is a static DCC/review render. Do not change viewer code and
do not run the replay/capture harness. Runtime replays are reserved for the
later integration pass and only when the owner explicitly asks for them.

### Gate E — lean derivative readiness

Only after silhouette approval, prepare lean runtime candidates. Suggested
batch targets:

- no more than 8,000 triangles per unique module;
- no more than 40,000 triangles across all five unique modules;
- no more than roughly 300,000 additional visible attachment triangles in a
  worst-case sixteen-bot, rank-five scene;
- no more than 1.5 MB first-load transfer for the complete five-model package;
- 256px maps per module or one shared 512px atlas preferred after visual proof;
- shared geometry/materials must remain instancing-friendly.

These are optimization targets, not permission to quietly damage an approved
silhouette. Report rich and lean metrics side by side and seek a decision if a
target cannot be met.

## Required evidence and file layout

Create a new provider-run group rather than mixing these files into the fleet
retexture run:

```text
art/class-models/provider-runs/meshy/arc-veterancy-modules-v1/
  PLAN.json
  inputs/
    veterancy-crown-spine/
    veterancy-swept-blade/
    veterancy-square-mantle/
    veterancy-radial-petal/
    veterancy-curved-rail/
  <module-id>/
    <task-id>/
      request-record.json
      task.sanitized.json
      artifacts.json
      timing.json
      RUN.md
      raw/
```

Use the existing provider-run scripts and conventions. Preserve, at minimum:

- exact model/provider version and sanitized settings;
- input paths, dimensions, and SHA-256 hashes;
- exact prompt text;
- task IDs and timestamps;
- numeric credit balance before and after each call;
- raw downloads and their hashes;
- normalization and optimization commands;
- rich/lean geometry and transfer metrics;
- any provider warnings, failed calls, or reroll rationale.

Place the owner-review evidence under a new directory:

```text
art/reviews/arc-relay-veterancy-modules-meshy7-v1/
```

Raw and review models remain art-side. Do not copy any GLB into the application
runtime tree before Gate D owner approval.

## Model acceptance checklist

A module batch is ready for owner review only when all of the following are
true:

- exactly five reusable modules exist and no bot body is fused into them;
- GLB headers, buffers, images, bounds, and transforms validate;
- axes, roots, and lowest-point normalization match this plan;
- side pieces mirror without inverted-looking detail or broken normals;
- matte hardware and energy inlay remain visually separable;
- the pieces read at the current Tapline gameplay camera and scale;
- the five-element composition looks elite when the sheet is viewed small or
  slightly blurred;
- all sixteen chassis remain recognizable and their important signature/weapon
  origins stay clear;
- open chassis preserve negative space;
- no attachment intersects the floor, health bib, veteran UI, or adjacent tile
  in the static fit sheet;
- no silhouette resembles an active field, selection ring, projectile, wall,
  turret, mine, carried core, or signature telegraph;
- all evidence, raw outputs, settings, balances, and hashes are preserved;
- the working tree is clean and the model batch is committed on `main`.

## Explicit non-goals for the 3D agent

Do not:

- alter game mechanics, replay data, or simulation timing;
- edit viewer/renderer code;
- remove the numeric rank plate in code;
- implement rank reveal, glow, animation, or level-up transitions;
- change health bibs, cooldown bars, selection rings, or UI layering;
- create projectiles or signature-effect models;
- replace, animate, or edit the sixteen existing bot chassis;
- run gameplay capture/replay harnesses;
- promote unapproved output to runtime assets.

## Renderer follow-on after model approval

This is included only to make the model boundary clear. A later renderer task
will:

1. introduce a separate lazy `veterancy` presentation asset tier;
2. load the five approved modules without changing CLI/replay output;
3. define sixteen declarative class fit profiles;
4. mirror and instance shared geometry rather than cloning unique assets;
5. drive the rank 1–5 visibility, spread, and controlled amber inlay;
6. replace the rejected numeric plate;
7. validate health-bib clearance, headings, all roster classes, and the real
   Tapline camera;
8. request runtime gallery/replay evidence only when the owner asks for it.

The model task ends with approved art-side modules and static all-roster fit
evidence. Runtime promotion is a separate approval.
