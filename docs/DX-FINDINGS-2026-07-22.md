# DX findings — agent-arena tournament, 2026-07-22

Reported independently by 3 agents building bots from player docs alone.
**Status: all 8 findings fixed** (1 during the run, 7 in the follow-up pass;
verification for each fix noted inline).

## Fixed during the run
1. **[critical] Isolated builds crashed on every cold compile** —
   `DirectoryNotFoundException: .../cache/<key>/build.log` (BotBuilder.cs:119):
   cacheDir was never created when the workspace lives under /var/lib/botbuild.
   All 3 agents hit it; it also *masked real compiler errors*. Fixed in 4477292.

## Fixed in the follow-up pass
2. **[high] Compiler errors are buried** — real C# errors surfaced wrapped in an
   unhandled-exception stack trace. Fixed: `BotBuilder` extracts the compiler
   diagnostic lines (player-relative paths, warnings filtered, log path
   appended) and the CLI catches `BotBuildException`. Verified: a syntax-error
   bot now prints `error: Build failed:\n  BrokenBot.cs(31,37): error CS1002: ;
   expected` and the build.log path — nothing else.
3. **[high] Shared `out/` clobbering** — `botarena play` wrote `out/` under
   cwd; concurrent runs overwrote each other. Fixed: default is now
   `out/<bot>-vs-<opponent>-<map>-s<seed>/` (identical reruns of a
   deterministic match still overwrite in place, by design — DECISIONS #40);
   `--out` pins an exact dir. Verified with 3 matchups from one cwd → 3 dirs.
4. **[med] Docs omit load-bearing rules** — unlimited shot range, mutual
   elimination = draw, MaxTicks → higher health wins, corner-strict LOS.
   Fixed: all four added to the site DocsPage "Rules" card and the project
   template README (plus: shots outrange vision, an implication two agents
   missed).
5. **[med] `botarena build --help`** parsed `--help` as a project path.
   Fixed: `--help`/`-h`/`help` anywhere prints usage and exits 0.
6. **[low] Replay self-inspection** — the replay schema was undocumented.
   Fixed: `docs/REPLAY-FORMAT.md` documents the full document shape, the
   canonical-JSON hashing rule, and where per-tick debug/visibility lives.
   (Also corrected a false docs promise found during this pass: debug lines
   are public in revealed replays, not owner-private — DECISIONS #39.)
7. **[low] `BOTARENA_BUILD_ISOLATION=off` undocumented** — fixed:
   `botarena doctor` now reports the active isolation mode and the override,
   and CLAUDE.md documents the knob.
8. **[low] Seed-derivation docs** — fixed: the template README and the new
   DocsPage "Determinism" card explain that `context.Random` derives from
   match seed + slot, why replays are exact, and why one seed is not a
   benchmark.
