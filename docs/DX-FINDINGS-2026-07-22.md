# DX findings — agent-arena tournament, 2026-07-22

Reported independently by 3 agents building bots from player docs alone.

## Fixed during the run
1. **[critical] Isolated builds crashed on every cold compile** —
   `DirectoryNotFoundException: .../cache/<key>/build.log` (BotBuilder.cs:119):
   cacheDir was never created when the workspace lives under /var/lib/botbuild.
   All 3 agents hit it; it also *masked real compiler errors*. Fixed in 4477292.

## Open, by severity
2. **[high] Compiler errors are buried** — real C# errors (e.g. CS0136) surface
   wrapped in an unhandled-exception stack trace from the CLI instead of a clean
   error listing. Fix: BotBuildException already carries the log; print the
   compiler diagnostics section, not the stack.
3. **[high] Shared `out/` clobbering** — `botarena play` writes `out/` under
   cwd; concurrent users (or one user, two bots) overwrite each other's
   replay.json. Fix: default out dir per bot (`<botdir>/out`) or unique names.
4. **[med] Docs omit load-bearing rules** — unlimited shot range, mutual
   elimination = draw, MaxTicks → higher health wins, and the corner-strict
   LOS rule (diagonal tiles behind corners invisible; broke an agent's escape
   logic). Add to DocsPage "Rules".
5. **[med] `botarena build --help`** parses `--help` as a project path →
   "not a bot project". Add help flags to arg parsing.
6. **[low] Replay self-inspection** — agents wanted their own per-tick
   position/debug more discoverable in replay JSON (it exists in
   ticks[].bots[]; docs never say so). Document the replay schema.
7. **[low] `BOTARENA_BUILD_ISOLATION=off` is undocumented** (was needed as a
   workaround for #1; still worth documenting for local debugging).
8. **[low] Seed-derivation docs** — behavior of match seeds vs. resubmitted
   sources isn't documented (results change after any source edit).
