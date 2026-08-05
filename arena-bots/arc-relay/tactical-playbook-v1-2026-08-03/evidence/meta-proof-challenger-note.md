# Meta Proof — sealed challenger design note

Written 2026-08-05, BEFORE any Meta Proof pool tuning began. Per the
goal's blind element, the unseen challenger will be authored from this
note only, after the pool freezes, and played only at finals. It must
not sweep any pool member.

## Design: `veil-caravan` — concealed logistics

A strategy mechanically distinct from every planned pool member: it
neither zones (sentinels), drags (hooks), races (raw tempo), sieges,
denies area (mines), nor adapts defensively (recognition). It hides.

- Composition: veil ×2, patchbay ×2, kestrel ×2, relay, palisade.
- Plan: both veils escort the courier lanes and keep rolling
  smoke-canister cover on the pickup and return corridors; couriers
  (kestrels + relay) run inside the smoke, where enemy focus fire and
  hitscan-era instincts cannot find them; patchbays heal the caravan in
  transit; the single palisade walls the home choke.
- Engagements: run-fight control-first; no fortify recognition at all —
  the answer to pressure is concealment and movement, not a
  phase change. Detection machinery stays in the sheet unused (detect
  mass 8, the maximum, so it effectively never latches).
- Tasks: farm-north and farm-south with veils added to both escort
  assignments; no hunt task.
- Custody: farm custodies with authorized carriers = runners; incidental
  deliver.

Authoring constraints fixed now: dm 8 / ds 4, rm 2 / rs 12, linger 3,
no forwardPass, standard library, wildcard bindings both sides,
composition and roles exactly as above (veil in eyes + runner
candidates, second patchbay in runner candidates). Whatever these
numbers turn out to be worth, they are what the challenger plays with —
no tuning after this note.
