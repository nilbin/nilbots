# HouseApprentice

Status: T2-qualified under `frontline-duel-depth-union-t2-v1`; T3 boundary
pending; not balance-verdict eligible.

HouseApprentice is the contract-driven generic-actor starter retained as a
canonical low-tier instrument and potential friendly launch opponent. It:

- handles non-default participant identities and every declared team slot;
- activates automatic children and explicitly fabricates ready children;
- routes to and holds the active Frontline objective;
- fires direct legal shots;
- sidesteps straight projectile trajectories and avoids immediately returning
  to its vacated hazard tile.

It deliberately lacks curve planning, transformations, body roles, focus fire,
shared-information tactics, opponent modelling, and deeper planning. Passing
T2 means those basic verbs work; it does not prove that the policy is an
exact-boundary T2 instrument until it also cleanly fails the future cumulative
T3 profile.

Re-run the frozen qualification:

```bash
scripts/botarena experiment frontline-labs qualify \
  --bot arena-bots/frontline-labs/qualification-instruments-v1-2026-07-28/house-apprentice/bot.wasm \
  --runtime wasm \
  --suite frontline-qualification-3 \
  --out /tmp/house-apprentice-t2
```

The tracked qualification report is `qualification-frontline-3.json`. Its
replay paths resolve against the cohort's ignored
`evidence/house-apprentice-t2-v1/` directory; `../evidence-manifest.json`
records the retained byte hashes.

