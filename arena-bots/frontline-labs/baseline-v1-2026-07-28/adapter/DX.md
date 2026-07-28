# Adapter authoring DX

## Timing and scope

- Scaffold creation: 2026-07-28 10:22:28 +0200.
- First valid controlled WASM artifact: 2026-07-28 10:29:54 +0200.
- Time from generated project to first valid build: about 7 minutes 26
  seconds, including reading the generated project, consulting the public SDK,
  and writing the one gameplay pass.
- The controlled build itself completed in 20.1 seconds on a cold cache.
- Authoring passes: 1.
- Mechanical repairs: 0.
- Gameplay source was frozen before the first build. No match, replay,
  tournament output, opponent source, built-in policy, engine code, App code,
  runtime test, or balance evidence was used.

## Documentation and terminology

The standalone Labs-v1 rule card was sufficient to understand capture,
lifecycle, form, projectile, and transition behavior. Its repeated warning
that the delivered contract is authoritative was useful, especially beside
the current concrete map and timing values. The author packet also made the
fresh-runtime boundaries for Fabricate and Split unambiguous.

The Labs authoring commands are repeated in both `EXPERIMENTAL-FRONTLINE.md`
and `WASM-DEVELOPMENT.md`. They agree, but the generic-runtime section contains
substantial wire/runtime detail after the player-facing commands. A shorter
linked “generic bot authoring” section would make the intended reading boundary
easier to scan.

“Team score,” “territorial progress,” “capture progress,” and “active
position” are distinct concepts. The rule card explains them, but a compact
SDK example comparing the timeout-ranking channel across teams would reduce
the chance that an author treats capture progress as the scoreboard.

## API and scaffold friction

The generated action helper correctly resolves the numeric code from current
legality and the starter objective navigator is a useful minimal example. The
starter does not demonstrate extracting unit-target, form-target, or
shot-program constraints, so those require reading the SDK records.

The public API is expressive but its nested closed-union names are long. Code
such as
`GenericActorActionLegality.ArgumentConstraint.FormTargetConstraint` is clear
once found, but repetitive. Small public helpers such as `AllowedDirections`,
`AllowedUnitTargets`, `AllowedFormTargets`, and `TryCreateDecision` would make
constraint-safe authorship more direct.

Other missing conveniences that would have helped:

- contract lookups for form, attack profile, region, and timeout score channel;
- a typed score-ranking direction or a public comparison helper (the direction
  is currently a policy-ID string);
- an objective-region resolver joining Frontline mode state to the map binding;
- a pathfinding helper that accepts the current movement constraint and dynamic
  occupied tiles;
- a shot-ray helper for exact eight-way alignment, range, walls, and strict
  diagonal corners.

The starter BFS is intentionally small, but it does not feed the move
direction constraint into path selection and only treats actors as dynamic
obstacles. Extending it to account for visible projectiles was straightforward
after reading their public fields.

## Diagnostics

The successful build output clearly identified the entry type, WASM runtime,
selected actor protocol, SDK and compiler versions, cache result, artifact
hash, and artifact path. That is enough to confirm the intended generic actor
surface without running a match.

The first build was valid and emitted no compiler diagnostics. The produced
artifact hash was
`d2549f94b31068730d889148f35400bf017a9b71a37013aedc9583e135945b42`.

## Hardcoding temptations

The concrete rule card makes it tempting to copy the two-team arrangement,
three unit slots, five objective indices, 23-by-15 map, spawn coordinates,
unlock ticks, capture threshold, action codes, and named form progression.
Adapter instead reads topology, region ordering, geometry, score ranking,
timing, form weights/profiles, transition catalogs, and typed legal values.

Stable action IDs are used only to recognize optional semantic capabilities.
Target unit IDs and target form IDs come from the current legality constraints;
numeric action codes always come from the same legality entry. The `#` wall
symbol follows the generated scaffold and the public map row encoding.

## Repair ledger

No mechanical repair was needed. The first controlled build after source
freeze succeeded, so the manifest remains at one authoring pass and zero
mechanical repairs.
