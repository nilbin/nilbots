# DX findings — agent-arena gen-3 (energy experiment), 2026-07-23

Single-challenger run (Metronome, energy-tempo duelist) under
`BOTARENA_RULES=energy` — the first rules-experiment tournament. The new
tooling carried it: the agent iterated in-process with `--seeds` batches,
did all loss forensics through `replay --summary`, and reached
Built-with-identical-parity on both submissions. Findings:

## Fixed same-day
1. **[high] `replay --summary`/`verify` crashed on every drawn match** —
   `MatchResultInfo.WinnerSlot` was `required` but canonical JSON omits null;
   draws are exactly the games needing forensics. Fixed (78e79cb) + round-trip
   regression test.
2. **[med] Summary hid quiet ticks** (movement-only sequences, e.g. the three
   decisive turns before a death) — `--summary --full` added (78e79cb).
3. **[low] `set` default seeds were unreproducible** — a `Repro:` line now
   echoes the exact `--maps/--seeds/--rules` flags.

## Open
4. **[med] Fuel faults are undiagnosable.** Accidental mutual recursion passed
   in-process and died in WASM as `Fuel limit exceeded` ×3 → DQ with no depth/
   fuel info. Wanted: per-tick fuel report in in-process mode or fault
   diagnostics. (Enforcement itself judged fair — bounded BFS fits easily.)
5. **[med] `OnCooldown` is overloaded** for energy-blocked shots — a bot
   cannot distinguish "reloading" from "dry" about ITSELF. If energy ever
   ships, add a distinct `OutOfEnergy` result (additive enum value; old
   compiled bots simply won't match it).
6. **[low] `/build-status` returns an array** while the docs read like an
   object — one sentence in the docs would prevent the first-parse failure.
7. **[low] No way to pin default rules per-project** (`--rules` silently
   dropped = practicing the wrong game) — consider a `rules` field in
   botarena.json.
8. **[doc] Energy semantics were reverse-engineered** (regen at end of ticks
   where tick % 3 == 2, globally; legality checked pre-regen; dry Shoot
   neither starts cooldown nor spends) — recorded in the agent-arena skill's
   experiment brief; must become player docs if energy ever ships.

Balance observations went to docs/GAME-DESIGN.md (energy verdict), not here.
