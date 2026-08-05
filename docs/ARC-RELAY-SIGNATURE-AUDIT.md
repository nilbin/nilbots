# Arc Relay signature grammar audit (2026-08-05)

Owner-requested check of all sixteen signatures before the sentinel
rules change, on one question: **does every enemy-affecting effect give
its victim an out** — a travel time, a tell, or a positional warning?
Numbers are the `arc-relay-forward-combat-01` contract values.

## The grammar, signature by signature

| Signature (class) | Effect | Victim's out | Verdict |
|---|---|---|---|
| rail-line (longshot) | 2 dmg line, range 12 | **2-tick tell** before the line fires | clean |
| falling-star (mortar) | 1 dmg at tile, range 8 | **2-tick tell**, marked tile — step off | clean |
| kinetic-burst (repulsor) | push 1 + dmg, adjacent | **tell** and melee range is its own warning | clean |
| trip-node (minesmith) | 2 dmg on trigger | positional — revealed within 2, don't step on it | clean |
| basic guns (all) | 1 dmg | projectile travels 2 tiles/tick — move off the path | clean (the baseline) |
| arc-toss (relay) | core displacement | 1-tick tell + visible flight | clean |
| exchange (switchback) | ally swap | 1-tick tell; allies only | clean |
| vector-dash (kestrel) | self movement | tell; affects nobody | clean |
| prism-wall / hardlight-block | terrain | walls are walls | clean |
| survey-flare / smoke-canister | vision | information, not harm | clean |
| repair-beam (patchbay) | ally heal | n/a | clean |
| **sentinel-seed (nest)** | **1 dmg, range 4, every 3 ticks** | **none — instant hitscan, no tell, no travel** | **outlier — change greenlit** |
| **tractor-hook (towline)** | **forced pull 3, range 6** | **none — instant at range, no tell** | **outlier** |
| **null-field (hush)** | **suppression, radius 3, 5 ticks** | **none — instant AoE, no tell** | **borderline** |
| **target-paint (sunder)** | +1 dmg on next 3 hits, range 7, 8 ticks | instant mark, no tell — but no direct harm; counterplay is retreating while painted | acceptable |

## Findings

The owner's instinct was right and generalizes: the grammar is
overwhelmingly consistent — damage travels or telegraphs — and exactly
two signatures break it outright, with one borderline:

1. **sentinel-seed** — the only silent instant repeating ranged damage.
   Ruled: becomes a real projectile, and fires faster
   (`fireCooldownTicks` 3 → 2) to compensate for dodgeability.
2. **tractor-hook** — the only silent instant forced movement. A hook
   that can be seen coming (short tell, or a hook projectile that
   travels and that a prism-wall can eat) turns "you were repositioned"
   into "you were caught". Recommend: projectile hook, blockable by
   walls — it uses grammar the game already has and gives Palisade a
   beautiful counter-role.
3. **null-field** — instant self-centered suppression. Small radius
   partially excuses it (proximity to a Hush is a warning), but a
   1-tick charge tell would put it fully inside the grammar. Recommend:
   tell 1.
4. **target-paint** — instant, but harmless by itself and counterable
   during its whole duration. Recommend: leave unchanged.

## Proposed rules version (one fingerprint roll carries all of it)

- sentinel-seed: fire becomes a projectile (2 tiles/tick, same range 4,
  damage 1), `fireCooldownTicks` 3 → 2 (owner compensation ruling).
- tractor-hook: hook becomes a projectile (2 tiles/tick, range 6),
  blocked by walls and bodies; pull applies on contact.
- null-field: gains `tellTicks` 1.
- signature metadata fields (`category`, `argumentKind`,
  `engagementRange`) ride the same version per
  `DESIGN-ARC-RELAY-SIGNATURE-METADATA.md`.

Everything else stays. Champions were frozen under `-01`; the new
version starts a fresh evaluation context and every reigning strategy
gets re-validated against the new physics before any Stage 4 goal.
