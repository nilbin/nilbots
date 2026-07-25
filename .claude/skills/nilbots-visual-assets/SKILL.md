---
name: nilbots-visual-assets
description: Create, revise, bake, and validate nilbots arena themes, bot looks, and projectile looks. Use when work involves generated material sources, floor or wall textures, topology atlases, theme manifests, map presentation families, bot chassis or projectile PNG/SVG assets, visual bundle size, or gameplay-scale art review.
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

1. Start with genuine SVG. Treat PNG as an exception that requires
   gameplay-scale evidence that intentional painterly, organic, or
   texture-heavy art is materially worse in vector. Never auto-trace a raster
   merely to claim vector.
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

## Projectile-look workflow

1. Start with genuine SVG on a transparent `viewBox="0 0 256 256"`, authored
   facing East. Current projectile sprites are white alpha masks: opacity may
   shape internal energy layers, but fixed colors, embedded rasters, floors,
   shadows, trails, and glows do not belong in the sprite.
2. Keep one compact projectile head centered with generous transparent
   padding. It must remain distinct around 16–28 gameplay pixels without
   appearing wider than its authoritative tile or implying a different
   hitbox, path, speed, range, or damage.
3. Add `web/src/assets/projectile-looks/<id>/look.json` plus `sprite.svg`.
   Discovery is manifest-driven; do not add a TypeScript registry. Keep scale
   within the tested 0.3–0.7 gameplay-tile range.
4. The renderer tints the mask with the bot accent and owns the common trail,
   glow, local contrast adaptation, fog truthfulness, and interpolation across
   authoritative replay traversals. Do not bake those effects into one look.
5. Test all cardinal and diagonal headings, launch, speed-one and speed-two
   traversal, bends, impact, light and dark themes, fog, DPR 1/2, and the
   appearance-editor preview.

Projectile appearance belongs to the bot beside its chassis and accent. The
bot record snapshots it into each match and replay. Legacy or missing IDs use
the default Pulse Bolt. Entitlements authorize equipping a look; they never
change replay rendering or gameplay.

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
