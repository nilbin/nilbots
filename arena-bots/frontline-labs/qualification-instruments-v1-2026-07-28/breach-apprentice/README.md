# BreachApprentice

Status: cumulative T4 qualified under
`frontline-duel-depth-union-t4-v1`; entrant-level balance-evidence eligible,
but not independently sufficient for a balance verdict.

BreachApprentice derives from the retained T3 ArcApprentice and adds one
positional rule. When it is outside the active objective and a visible
projectile will threaten its current tile within two advances, it may advance
toward the objective before the generic dodge if that move remains safe
through the next projectile advance.

That narrow initiative rule crosses the current-map suppression choke from
both assignments without weakening the cumulative T2/T3 capabilities. The
suite also verifies straight suppression while holding useful ground,
objective-preserving threat response, rotation after a captured front, and
the same pressure-entry behavior on the thin-fronts holdout map.

It still has no opponent model, curve mixture, forced-shot construction,
transform doctrine, body roles, shared-information tactics, or multi-front
planning. Because it is an adjacent revision of the House/Arc lineage, it is
a boundary instrument and potential launch opponent—not an independent
doctrine for the four-doctrine pilot population.

Re-run the frozen qualification:

```bash
scripts/botarena experiment frontline-labs qualify \
  --bot arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/breach-apprentice/bot.wasm \
  --runtime wasm \
  --suite frontline-qualification-5 \
  --out /tmp/breach-apprentice-t4
```

The tracked report is `qualification-frontline-5.json`. Its replay paths
resolve against the cohort's ignored `evidence/breach-apprentice-t4-v1/`
directory; `../breach-evidence-manifest.json` records every retained replay
byte hash.
