# DX findings — agent-arena gen-2 tournament, 2026-07-22

Reported by 3 agents (Rampart, Oracle, Switchblade) across build + 2 ranked
rounds + 1 improvement iteration. All gen-1 findings stayed fixed; every agent
reached Built-with-identical-parity on the first submission attempt and called
the submission flow "frictionless". These are the NEW findings, consolidated
and severity-ranked.

**Status: all 11 fixed** in the follow-up pass (DECISIONS #46). Per item:
1 → REPLAY-FORMAT.md "How to read a replay" + `replay --summary`; 2 →
`BotContext.Slot` (SDK 0.2.0, no wire change); 3 → SDK XML docs + site docs
event-semantics bullet; 4 → `.wasm` labels from the parent directory; 5 →
`--swap`, `--seeds`, and the `botarena set` command; 6 → rules bullets on the
site + template README; 7 → ranked pool named in docs; 8 →
`GET /api/bots/{id}/build-status`; 9 → "Scripting the API" docs card; 10 →
`build` copies the artifact to `<project>/out/bot.wasm`; 11 → determinism
card explains seed-invariance (spawn variation itself stays a GAME-DESIGN
backlog item). Original findings below for the record.

1. **[high] Replay semantics are undocumented where it matters.**
   `docs/REPLAY-FORMAT.md` documents the shapes but not the load-bearing
   conventions, and nothing on the site links it. All three agents
   reverse-engineered, and initially mis-read: (a) `ticks[i].state` is
   POST-tick while `ticks[i].bots[]` decisions were taken from the PRE-tick
   state (= `ticks[i-1].state`); (b) `Damage.slot` is the DEALER, `targetSlot`
   the victim; (c) `Shot.toX/toY` is where the ray stopped; (d) `debug` is
   absent (not empty) on silent ticks; (e) three position encodings coexist
   (`[x,y]` pairs, `{x,y}` fields, `fromX/fromY`). Fix: a "reading a replay"
   section in REPLAY-FORMAT.md + link from DocsPage; consider unifying
   encodings at the next replay-version bump.
2. **[high] A bot cannot learn its own slot** — `BotContext` has no `Slot`,
   but `VisibleEvent.Slot`/`VisibleEnemy.Slot` exist, so attributing a Shot
   event to self-vs-enemy before first sighting needs heuristics. All three
   agents built cooldown ledgers; all three hit this. Fix: add
   `BotContext.Slot` (SDK version bump; trivial to thread through).
3. **[med] `VisibleEvents` semantics unspecified**: which tick events
   describe, whether events beyond vision range are delivered (muzzle-flash
   tracking depends on it — empirically yes at ~7 tiles), which position
   gates visibility, and per-kind meaning of `Position`. Fix: XML-doc the SDK
   types + a docs bullet defining event visibility (both endpoints of the
   event are checked against your vision).
4. **[med] `.wasm` opponents are all named "bot"** — labels come from the
   file stem, so `champions/*/bot.wasm` opponents print `bot (slot 1) wins`
   and share the `-vs-bot-` output directory, colliding across different
   champions. Fix: when the stem is `bot`, label from the parent directory
   (`warden-gen1`).
5. **[med] No `--slot`/`--swap` on `play`, no multi-seed batch mode.** Ranked
   sets play both positions; locally you must invert `--bot`/`--opponent`
   and read inverted result labels. 15-combo test matrices = 15 CLI
   invocations with full startup each. Fix: `--swap`, `--seeds a,b,c`, and a
   `botarena set` command that runs the 6-game mirrored format locally —
   this is also exactly what the GAME-DESIGN balance harness wants.
6. **[med] Rules-doc gaps found by play** (docs said nothing wrong, but not
   enough): vision is omnidirectional (facing-independent) and Chebyshev;
   corner-strict is not formally defined (supercover: any wall the sight
   segment touches blocks, corners included); adjacent diagonal walls can be
   invisible (`IsWall` returns false for unseen tiles — warn next to the
   helper); `Cooldown` counts 2→1→0 after a shot; a shooter's ray originates
   from its pre-move position/facing, and a perpendicular same-tick move
   dodges it (the two duel-deciding corollaries of the resolution order).
7. **[low] Ranked map pool undocumented** (it is basic-01 + arena-01 today);
   `botarena maps` doesn't say which are ranked.
8. **[low] Build-status polling returns full sources** — `GET /api/bots/{id}`
   ships every version's SourcesJson on every poll. Add a slim status view.
9. **[low] Headless auth path undocumented** — `botarena login` is
   browser-only; the agents used register + cookie jar against the REST API,
   which works but is described nowhere player-facing.
10. **[low] `build .` leaves no artifact in the project dir** (cache only),
    so you cannot point `--opponent` at a freshly built *file*; passing the
    project directory works. Either emit `out/bot.wasm` or document the
    asymmetry.
11. **[low] Seeds rarely vary outcomes** between disciplined deterministic
    bots (only `context.Random` consumes the seed; identical strategies ⇒
    identical games across seeds). The docs' "test several seeds" advice is
    misleading at high skill; map/slot variation is what matters. Real fix is
    game design (seed-varied spawns — GAME-DESIGN backlog).

Positives, verbatim themes from all three: determinism made loss forensics
trivial; `Debug.Write` in replays was "invaluable"; the build cache and
per-matchup output dirs (gen-1 fixes) "worked flawlessly"; artifact parity
IDENTICAL on every submission; the champion bar "is the right difficulty".
