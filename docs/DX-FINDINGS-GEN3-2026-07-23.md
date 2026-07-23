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

## Fixed in the pre-gen-4 batch
4. **[med] Fuel faults are undiagnosable.** Accidental mutual recursion passed
   in-process and died in WASM as `Fuel limit exceeded` ×3 → DQ with no depth/
   fuel info. Fixed (lite): single-seed `play` in WASM mode now prints a
   `Fuel: peak <bot> <N>M per tick (limit 200M)` line, so a bot can see how
   close it runs before it dies. (Enforcement itself judged fair.)
6. **[low] `/build-status` returns an array** while the docs read like an
   object — docs now say array-newest-first, poll `[0].status`.
7. **[low] No way to pin default rules per-project** (`--rules` silently
   dropped = practicing the wrong game) — botarena.json now takes a `rules`
   field used by play/set when no `--rules` flag is given (announced when it
   engages; explicit flag always wins).

## Open
5. **[med] `OnCooldown` is overloaded** for energy-blocked shots — a bot
   cannot distinguish "reloading" from "dry" about ITSELF. If energy ever
   ships, add a distinct `OutOfEnergy` result (additive enum value; old
   compiled bots simply won't match it).
8. **[doc] Energy semantics were reverse-engineered** (regen at end of ticks
   where tick % 3 == 2, globally; legality checked pre-regen; dry Shoot
   neither starts cooldown nor spends) — recorded in the agent-arena skill's
   experiment brief; must become player docs if energy ever ships.

Balance observations went to docs/GAME-DESIGN.md (energy verdict), not here.
