# Aegis Tortoise authored form fidelity brief

Status: implementation authority for the deterministic Shell and whole-Turret
rebuild. The checked-in SVGs remain the canonical 2D/site/Canvas identities.

## Shared family contract

- Canonical 2D assets:
  - `web/src/assets/class-looks/aegis-tortoise-shell/sprite.svg`
  - `web/src/assets/class-looks/aegis-tortoise-turret/sprite.svg`
- Visual/material references: the owner-approved inferred-3D sheets and the
  audited raw Meshy tasks `019fb22d-c313-76f8-8eca-91ccbb46afd0` (Shell) and
  `019fb232-6e09-787f-85cc-dbaea29fb9b6` (whole Turret). Their topology is
  expressly rejected as an authoring source.
- Family read: low, heavy, planted hard-surface machinery with layered
  graphite/navy armor, weathered warm bronze, dark mechanical understructure,
  restrained scratches, panel breaks, and one cool fixed class-energy core.
- Coordinate contract: `+X` is East/facing, `+Y` is up, the floor is `Y=0`,
  and the maximum authored planform span is one gameplay tile.
- Team identity is a separate neutral material carrying
  `extras.nilbotsRole = "team-accent"`. Armor and the central reactor never
  inherit team tint.
- Broad silhouette and real armor thickness do the work at gameplay scale.
  Texture and fastener detail may enrich a close review, but cannot repair a
  failed top planform.
- Forbidden shared cues: flight, hover, wheels, long tank cannon, force-field
  dome, text, logo, floor, trail, cast-shadow geometry, baked team color, or
  any mechanism that promises rules the forms do not have.

## Directional Shell

Gameplay read: a stationary stance that physically protects only the
East-facing `-45°…+45°` quadrant. Its hard radial edges are counter-play and
must stay obvious from the gameplay camera.

Required independent anchors:

1. Exact East-facing SVG outer planform under rigid same-scale overlay.
2. One physical bronze/navy guard occupying exactly the East quadrant, with
   hard radial ends and no muzzle.
3. Open north, west, and south quadrants with exposed, named planted support
   hardware; the missing armor is intentional negative space.
4. One concentric octagonal hub/reactor that remains fixed class energy.
5. Exactly three semantic team surfaces: the two narrow guard-edge bars and
   one west-centre support panel.
6. Genuine side depth near `0.31` of the one-tile planform span: layered top
   armor, a recessed underbody, short locks/feet, guard thickness, and visible
   support pivots.

Named hierarchy:

- `root`
- `hub`
- `guard`
- `support_north`
- `support_west`
- `support_south`

The `AegisShell_Deploy` clip may raise the physical guard and plant the three
supports; `AegisShell_Idle` may settle the hub by only a few millimetres. Both
are presentation-only, in-place, and must end in the exact authored footprint.
No walk, recoil, turning, or locomotion clip belongs to Shell.

## Whole omnidirectional Turret

Gameplay read: one stationary, non-facing radial emplacement. It sees and
fires omnidirectionally; no arm may read as a privileged nose or rear.

Required independent anchors:

1. Exact SVG star/cross outer planform under rigid same-scale overlay.
2. One singular concentric octagonal hub/reactor.
3. Exactly four equal cardinal arm pods and four equal diagonal shoulder webs.
4. Exact `90°` repetition from one canonical arm/support construction.
5. Four compact planted supports inside the canonical footprint.
6. Exactly four equal semantic team bars, one on each arm top. No team-colored
   under-arm or support surface is allowed.
7. Genuine side depth near `0.26` of the one-tile planform span, with readable
   armor stacks, under-arm mechanics, ground locks, and a raised central hub.

Named hierarchy:

- `root`
- `hub`
- `arm_east`, `arm_north`, `arm_west`, `arm_south`
- `support_east`, `support_north`, `support_west`, `support_south`
- `emitter_east`, `emitter_north`, `emitter_west`, `emitter_south`

`AegisTurret_Deploy` may unfold the four arm pivots from one folded heading
into exact quarters and plant their supports. `AegisTurret_Idle` may settle
only the central hub. Direction-specific fire response must remain available
through the four named emitter pivots; a clip that recoils all four arms at
once would lie about the recorded shot heading and is forbidden.

The runtime must mount this complete object once. The existing renderer path
that clones one emplaced model four times is not a legal installation route.

## Acceptance evidence

- Strict top, side, front, rear, three-quarter, and gameplay-oblique renders.
- Same-scale exact-SVG top overlays with no reflection, non-uniform scale, or
  contour warp.
- Turret `90°` rotational silhouette and authored-instance symmetry checks.
- Blue-team and red-team gameplay renders proving that only semantic inlays
  change.
- Deploy-frame boards proving pivots remain inside the authoritative
  footprint and finish on the planted rest pose.
- Structural GLB validation for axes, floor, bounds, materials, hierarchy,
  clips, cameras/lights, triangle count, texture dimensions, and deterministic
  rebuild hashes.
