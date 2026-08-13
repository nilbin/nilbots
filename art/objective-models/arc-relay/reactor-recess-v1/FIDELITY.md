# Team Reactor recess fidelity brief

## Canonical identity

- Canonical 2D sheet: `reactor-recess-six-view.png`
- Intended gameplay read: a broad, very flat Reactor aperture installed in the
  arena floor, never a raised machine or bot-sized prop.
- Required top silhouette: one octagonal pale rim around one dominant dark
  central aperture.
- Required negative space: the central aperture remains open, broad, and
  visibly deeper than the rim from the 52-degree gameplay camera.
- Required hardware: exactly three large shallow rectangular state bays around
  the rim and exactly three muted oxblood clamps between them.

## Physical and renderer ownership

- Physical model owns only the neutral rigid socket, recessed bowl, bays, and
  clamps.
- Renderer owns team teal/orange, crest, exact charge and integrity state,
  ownership ring, banking pulse, damage, destruction, and any emission.
- The model is deliberately normalized to about `1.5` tiles and recessed into
  the arena floor at runtime. It changes no collision or game rule.

## Material regions

- Dominant: medium-light aged ivory/warm-gray matte ceramic rim.
- Secondary: deep graphite aperture and below-floor bowl.
- Hardware: dull nickel edge trim and restrained muted oxblood clamps.
- Finish: high-roughness environment material, brighter and coarser than the
  arena floor but less shiny and less saturated than a bot.

## Forbidden cues

- No tower, station, gate, turret, weapon, bot, legs, pillar, orb, Core, or
  raised central machine.
- No baked teal, cyan, orange, team crest, light, glow, pips, text, numbers,
  logo, ownership ring, damage, smoke, or fire.
- No extra ports, bays, lamps, clamps, disconnected parts, or scenery.

Any provider output that closes the central aperture, invents a tall housing,
or loses the three-bay/three-clamp rhythm fails the geometry gate regardless
of texture quality.
