---
name: nilbots-visual-assets
description: Create, revise, bake, and validate nilbots arena themes and bot looks. Use when work involves generated material sources, floor or wall textures, topology atlases, theme manifests, map presentation families, bot chassis PNG/SVG assets, visual bundle size, or gameplay-scale art review.
---

# Nilbots Visual Assets

Preserve ASCII gameplay semantics while improving presentation. Read
`docs/ARENA-VISUALS.md` completely before changing assets, then inspect the
owning map JSON, theme manifest, or bot-look manifest.

## Arena theme workflow

1. Generate distinct opaque material fields, never a whole map, isolated wall,
   or atlas. Keep the exact accepted prompt in `art/themes/SOURCE-PROMPTS.md`.
2. Put accepted wall sources under
   `art/themes/<theme>/walls/<family>/source.png`. Keep perimeter and interior
   cover as separate semantic families.
3. Update `art/themes/<theme>/art.json` and the runtime
   `web/src/assets/themes/<theme>/theme.json`. Keep their atlas dimensions in
   agreement. Do not raise `assetBudgetBytes` just to make a build pass.
4. Rebuild in the disposable environment:

   ```sh
   python3 -m venv sandbox/theme-art-venv
   sandbox/theme-art-venv/bin/pip install -r scripts/requirements-theme-art.txt
   sandbox/theme-art-venv/bin/python scripts/build-theme-art.py art/themes/<theme>/art.json
   ```

5. Assign the theme and wall families in map presentation data. Never infer a
   theme from map ID or add a viewer skin switch. Keep map packages within the
   engine's 32×32 envelope.
6. Review small and large maps at device pixel ratios 1 and 2. Inspect outer
   perimeter, isolated cover, corners, junctions, zone contrast, bots, health,
   projectiles, and fog.

The build owns seamless normalization, PBR helpers, 256 topology variants,
encoding, and the theme-wide runtime size check. Do not hand-edit its outputs.

## Bot-look workflow

1. Choose SVG for clean mechanical shapes; choose PNG for painterly, organic,
   or texture-heavy art. Never auto-trace a raster merely to claim vector.
2. Use the canonical transparent 512×512 canvas or SVG viewBox, facing East.
   A genuine SVG must not contain `<image>` or `data:image` raster payloads.
3. Keep high-resolution raster masters and exact generation prompts outside
   the runtime package. Remove backgrounds before deriving the 512×512 PNG.
   When replacing a shipped raster with SVG, retain the old image under
   `art/bot-looks/<id>/raster-reference.png`, not beside runtime assets.
4. Add `web/src/assets/bot-looks/<id>/look.json` plus `sprite.png` or
   `sprite.svg`. Discovery is manifest-driven; do not add a TypeScript registry.
5. Inspect the look at gameplay size facing all four directions and during
   movement, recoil, damage, destruction, fogging, and panel rendering.

Bot appearance belongs to the bot and is snapshotted into matches and replays.
Do not introduce slot-owned selection except the existing legacy fallback.

## Validation

Run:

```sh
(cd web && npm test && npm run build)
sandbox/theme-art-venv/bin/python -m py_compile scripts/build-theme-art.py
sandbox/theme-art-venv/bin/python scripts/build-theme-art.py \
  art/themes/<theme>/art.json --check
git diff --check
```

Compare the final `web/dist/index.html` size with the pre-change baseline.
When engine, replay, map, or submission contracts changed, also run
`bash scripts/test.sh`. Record new durable presentation choices in
`docs/DECISIONS.md`.
