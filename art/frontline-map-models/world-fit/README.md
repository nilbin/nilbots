# Frontline and class-model world fit

The environment and class-model tracks share one manufacturing language:
graphite, aged bronze, amber machinery, layered armor, recessed vents, and
swept/chamfered forms.

They deliberately do not share the same visual priority:

- Frontline architecture is rougher, matter, coarser, lower-contrast, and
  lower-saturation. Broad panel fields and dusty heat wear survive gameplay
  distance without competing with units. Amber is dim and recessed.
- Bots remain the hero assets: cleaner surfaces, finer panels, sharper layered
  edges, stronger controlled emission, and the only team cyan/red accent.
- Environment materials never contain team cyan or team red. Objective and
  team presentation remain renderer-owned overlays.

`review/world-fit/frontline-striker-world-fit-v1.png` is the approval board.
It pairs the V4 matte environment concept with review-sized copies of the
approved Striker oblique target and multiview candidate. The class-model source
files remain owned by the class-model branch; the copies here are only stable
review evidence.

Rebuild the board with:

```sh
node scripts/render-frontline-world-fit-board.mjs
```
