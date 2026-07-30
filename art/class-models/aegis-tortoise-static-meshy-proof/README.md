# Aegis Tortoise static Meshy-form proof

Status: non-shipping review proof on Fable tip
`5cda000dda941783b0133ae4cb45266f69e6ddbb`. Nothing in this directory is
wired into a runtime manifest.

## Verdict

The original Meshy Shell and Turret masters are viable as static whole forms.
The prior blocker was articulation: each provider result is one fused,
watertight mesh without a rig or semantic moving-part hierarchy. This proof
does not infer seams or rebuild the visible surface. It applies only
deterministic source orientation, one-tile normalization, measured
vertical-only side-profile correction, direct decimation, PBR grading and a
semantic material split for team accents.

Prefer the **lean** tier for lazy runtime delivery:

| Form | Rich | Lean | Saving | Native 192 px rich/lean result |
| --- | ---: | ---: | ---: | --- |
| Shell | 8,799,168 B; 150,000 tris; 2K PBR | 3,816,428 B; 75,000 tris; 1K PBR | 4,982,740 B (56.63%) | 37.64–38.48 dB PSNR; 3.66–4.00% changed pixels |
| Turret | 9,120,244 B; 149,999 tris; 2K PBR | 3,795,480 B; 75,000 tris; 1K PBR | 5,324,764 B (58.38%) | 38.47–38.66 dB PSNR; 3.65–3.80% changed pixels |

Together, lean saves 10,307,504 bytes (57.52%). The native gameplay crops
show no material readability benefit from rich. Retain rich as the
archival/on-demand review candidate.

## Review evidence

- [Exact fixed 58-degree board](review/fixed-58deg-rich-vs-lean-board.png)
- [Native 192 px board](review/fixed-58deg-rich-vs-lean-native-192.png)
- [Rendered team-difference masks](review/team-diff-mask-board.png)
- [Top and approved-side silhouette overlays](review/top-side-silhouette-overlays.png)
- [Exact image metrics and hashes](reports/review-metrics.json)
- [Khronos and structural validation](reports/khronos-validation.json)
- [Build and provenance ledger](reports/build-report.json)

Every gameplay cell uses the same one-tile planform, 58-degree camera, floor,
lights and exposure. There is no candidate-specific framing.

Native 192 px rich-versus-lean measurements use an 8/255 per-channel changed
pixel threshold:

| Form/team | MAE | RMSE | PSNR | Changed pixels |
| --- | ---: | ---: | ---: | ---: |
| Shell blue | 0.003651 | 0.011912 | 38.4804 dB | 1,350 (3.6621%) |
| Shell red | 0.003782 | 0.013122 | 37.6401 dB | 1,474 (3.9985%) |
| Turret blue | 0.003432 | 0.011674 | 38.6559 dB | 1,347 (3.6540%) |
| Turret red | 0.003464 | 0.011926 | 38.4698 dB | 1,399 (3.7950%) |

Direct-source silhouette IoU:

| Form/tier | Top authority | Approved side |
| --- | ---: | ---: |
| Shell rich | 84.8938% | 83.4331% |
| Shell lean | 84.9174% | 83.4400% |
| Turret rich | 84.1188% | 83.6666% |
| Turret lean | 84.1037% | 83.7074% |

Blue-to-red rendering changes only the neutral `team-accent` material. At
native 192 px, that affects 625/670 pixels for rich/lean Shell and 390/382
pixels for rich/lean Turret. The authored cyan core, hull textures and fixed
emission remain unchanged. Both the material and its primitive carry
`extras.nilbotsRole = "team-accent"`.

## Wall-clearance constraint

The Ember Forge V4 Frontline wall contract has a final open-edge inset of
`0.14462` tile. Its final wall surface is therefore `0.64462` tile from an
adjacent open-tile centre. The renderer contract reserves `0.02` tile of
static safety and measures the maximum transformed XZ vertex radius, not half
the width.

| Form/tier | Unit-model max XZ radius | Authored look scale | Radius at raw look scale | Margin to wall |
| --- | ---: | ---: | ---: | ---: |
| Shell rich | 0.5528291803 | 1.20 | 0.6633950163 | **-0.0187750163** |
| Shell lean | 0.5527171825 | 1.20 | 0.6632606189 | **-0.0186406189** |
| Turret rich | 0.5043719882 | 1.08 | 0.5447217472 | +0.0998982528 |
| Turret lean | 0.5043560247 | 1.08 | 0.5447045067 | +0.0999154933 |

Consequences:

- Do not apply the raw `1.20` Shell scale to this normalized GLB. It clips at
  arbitrary yaw. Preserving the current 0.02 safety would require a `0.183395`
  wall inset, 0.038775 more than Ember V4.
- Keep the primary WebGL convention
  `max(0.82, form look.scale * 0.9)`. It gives Shell an effective scale of
  `1.08`, radius `0.5970555147`, and wall margin `0.0475644853`; Turret becomes
  `0.972`, radius `0.4902495725`, and margin `0.1543704275`.
- If that convention changes, cap Shell's effective model scale at
  `1.1298608` to retain the existing 0.02 safety contract
  (`1.1660383` merely avoids static overlap without the safety margin).

Authority:
`web/src/render3d/arenaScene.ts`,
`web/src/render3d/wallSolids.ts`, and
`art/frontline-map-models/CLEARANCE-AUDIT.md`.

## Validation and provenance

Khronos glTF Validator `2.0.0-dev.3.10` reports zero errors, warnings, infos
and hints on all four GLBs. Structural checks also prove:

- one static mesh with one hull and one team-accent primitive;
- portable tangents and UVs on both primitives;
- embedded base colour, normal, metallic/roughness and emission maps;
- no rig, skin, animation, camera, light or external texture dependency;
- +Y up, floor at zero and a one-tile source planform.

Final validated model hashes:

| Form/tier | SHA-256 |
| --- | --- |
| Shell rich | `93c3bd19d58e679f7793d7d9eee15f22e49a77692acd688e61f369c6d594d1d2` |
| Shell lean | `25c86e581a8f569cbd730c7209478cb1b88a4a1fc3cc0778a023164a52e912a9` |
| Turret rich | `77ba482d194a66e3e1e9640b826171b93cddbc42a9453963c4231ceaffbc2592` |
| Turret lean | `addcc7b5fc684253a20ebb72fb91f1c78cf69af44c593d9c24e13bf150b90327` |

The build ledger and the independent Khronos pass agree on each final hash,
byte count, triangle count and vertex count.

Provider provenance:

- Shell task `019fb22d-c313-76f8-8eca-91ccbb46afd0`, raw SHA-256
  `8a0a0129cc57afdf12a0c0e70159f2acd547b426501941330e4712d06b78c5ec`.
- Turret task `019fb232-6e09-787f-85cc-dbaea29fb9b6`, raw SHA-256
  `a845f48ac9f0861c8ff2860de9c0dc79b0afa728794663f9d8ff70f7187f9cac`.

No provider call was made. Raw masters were read from ignored provenance
storage and were not modified.
