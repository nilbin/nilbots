# Frontline classes experiment (local-only)

Status: pre-registered candidate arms (DECISIONS #153). Nothing here is
hosted, ranked, or balanced; the values below are hypotheses for the
class-pair factorial.

Each team plays one **class**: a chassis that changes stats and available
verbs but never movement speed, projectile speed, or damage. Both teams keep
the same map, objective rules, scoring, and three-slot topology as the base
Labs contract. Your bot reads everything it needs from the resolved contract:
form stats, allowed actions, action legality masks, unlock ticks, and both
teams' form IDs are all visible at match start — a well-written bot can
recognize the opposing class from its forms and adapt.

## The slate

| | striker | bulwark | fabricator |
| --- | --- | --- | --- |
| Prime / child health | 3 / 3 | 5 / 4 | 2 / 3 |
| Mobile vision | facing quadrant, range 6 | omnidirectional, range 4 | facing quadrant, range 6 |
| Fire cooldown | 2 | 3 | 2 |
| Projectile range | 8 | 6 | 7 |
| Shot language | straight or one private bend (`shoot`) | straight only (`shoot-straight`) | straight only (`shoot-straight`) |
| Turret health | 5 | 7 | 5 |
| Turret may Mobilize back | no | **yes** | no |
| Child unlock ticks | 120 / 260 | 120 / 260 | **60 / 180** |
| Child rebuild delay | 30 | 30 | **15** |

Shared by every class: one tile of movement per tick, projectile speed two
with damage one, Anchor (`transform`) from child to turret gaining +2 health
capped at the turret maximum, Prime-only Split into two replicas, explicit
fabrication from the protected home pad, and Prime respawn after 18 ticks.

## Reading the class from the contract

- Your own forms carry your class prefix (`striker-prime`, `bulwark-turret`,
  …); the enemy's visible `FormId`s carry theirs.
- If your forms allow `shoot`, you have the one-bend program envelope; if
  they allow `shoot-straight`, the action takes no payload and always fires
  straight along your facing.
- Only a `bulwark-turret` ever has `mobilize` in its allowed actions.
- Unlock ticks and rebuild delays come from your slots' lifecycle
  assignments — do not hard-code 120/260.

## Running matches

```bash
nilbots experiment frontline-labs \
  --bot <generic-spec> --opponent <generic-spec> \
  --classes bulwark-vs-striker [--duel-map thin-fronts] \
  --seed 42 --runtime wasm --out /tmp/classes
```

Pairs are canonical in alphabetical order (`bulwark-vs-fabricator`,
`bulwark-vs-striker`, `fabricator-vs-striker`, and the three mirrors). Team 0
always plays the first class; use `--swap` to mirror bot assignments.
`--print-candidate-contract` emits the exact resolved identity for a spec.
