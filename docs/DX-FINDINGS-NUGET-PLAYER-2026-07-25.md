# DX findings — the NuGet-only player test, 2026-07-25

The first evaluation run against the **published product** rather than the repo.
An agent was given a clean directory, no access to this source tree, and exactly
what a stranger gets: `dotnet tool install --global Nilbots` (0.4.0 from
nuget.org), https://nilbots.com/docs, `--help`, and whatever the tool scaffolds.

**Verdict: yes.** It built a genuinely strong bot — **97W–6L–17D (80.8%)** vs
built-in `hunter` across 120 canonical-WASM games (12 seeds × both spawn slots ×
the 5 ranked-pool maps), 9/9 ranked `set` runs won, 6–0 over `wander`, `coward`
and `idle`. In-process and WASM tallies agreed exactly on a 12-seed sweep.

**No rules drift.** The CLI plays rules 0.5, and nilbots.com/docs plus the
scaffolded README both document 0.5. This is the failure that sank gen-6
(DECISIONS #57) and it is fixed: the README written by `nilbots new` carries a
complete, accurate v0.5 spec (control pressure, overtime, projectile speed,
cone vision, sound redaction, resolution order, tiebreaks), and the agent
designed its whole strategy from it.

**Kept, loudly:** `replay --summary` was called "the best debugging tool I have
seen in a game like this" and found all three of the bot's real bugs. `doctor`
is the other star — it is where the version triple and the toolchain state live.

## Fixed in this batch

1. **Unhandled crashes leaked CI build paths.** `whoami` aborted with a raw
   `HttpRequestException` + SIGABRT showing
   `/home/runner/work/nilbots/nilbots/src/...`; `--rules 9.9` did the same with
   an otherwise-good message. Root cause was narrower and worse than reported:
   `CreateClient()` refreshes an expired token *before* any command's own error
   handling, so **every** authenticated command (`whoami`, `submit`) aborted
   when its server was unreachable. Now: the refresh handles network failure
   with an actionable message, the top-level handler covers `ArgumentException`
   / `HttpRequestException` / IO faults with one clean line, and a genuine
   unexpected fault prints a readable line plus `NILBOTS_DEBUG=1` for the trace.
   `NotSignedIn` no longer contradicts a real session ("cannot reach X" was
   followed by "not signed in").
2. **`--version` didn't report a version** (printed help, exit 1). Now prints
   the CLI/SDK/rules/protocol quadruple — a bug report needs it.
3. **No XML docs shipped**, so every member of the one assembly that *is* the
   player API was blank in IntelliSense; the agent resorted to reflecting over
   the DLL. `GenerateDocumentationFile` is on, and `nilbots new` copies
   `BotArena.Sdk.xml` next to the dll it already copies. (Enabling it
   immediately caught a partially-documented `ShotPaths.Preview`, now written.)
4. **Strafe and Energy read as playable API but are inert.** See below.
5. **Template csproj referenced `botarena build`** — a command that no longer
   exists, in the first file a player opens.

## Strafe / Energy: hidden, not deleted

`Actions.StrafeLeft/StrafeRight` and `BotContext.Energy` are live in the API,
absent from every public doc, and inert under all shipped rules — strafe
degrades to `Wait` reporting `ActionResult.Blocked`, which the docs teach you to
read as *"a move was blocked"*, so players debug the wrong thing. Two
independent runs found this: gen-7's tournament bots and now an outside player.

They are now `[Obsolete]` + `[EditorBrowsable(Never)]`, with doc comments that
state plainly what happens. Deliberately **not** deleted:

- **Strafe is a live design lever, not dead weight.** The gen-7 verdict
  (DECISIONS #61) names it explicitly as one of the few candidate answers to the
  2×2 diagonal-mirror camper — "any mechanic that lets me relocate without
  spending a turn". Deleting it would throw away an option we identified days ago.
- The enum values are **wire values** carried in historical replays and existing
  champion artifacts; removing them is a compat break for zero player benefit.
- The `strafe` and `energy` research arms stay runnable for the balance harness.

The harm was never that the mechanics exist — it was that they *looked shipped*.
Hiding them fixes exactly that. Engine support is untouched; the two adapters
that must still carry them suppress the warning locally, which is the correct
place for framework plumbing to do so.

## Open findings (not fixed here)

- **[med] No headless auth path in the CLI.** `nilbots register` degrades well
  — with no browser it prints the full auth URL to paste elsewhere — but there
  is no `--no-browser`/device-code flag, and the headless route (cookie auth via
  `POST /api/accounts/register`, then poll `/build-status`, which returns an
  **array**, newest-first) is documented **only on the website**. A CI or
  VPS player reading `--help` concludes they are stuck.
- **[med] `--rules` help lists 24 unexplained ruleset names**, mixing shipped
  versions with internal experiment arms, with nothing marking which is which.
  A newcomer sees 24 equally-valid games.
- **[med] Enemy cooldown is not observable, and the workaround is undocumented.**
  `VisibleEnemy` exposes slot/position/facing/health. Whether the enemy can fire
  *this tick* is the game's most important tactical fact; it is reconstructible
  from `VisibleEvents`/`HeardSounds` of kind `Shot` plus the documented 2-tick
  cooldown, but that synthesis appears nowhere. The winning bot's core loop
  depends on it. Either document the reconstruction or expose the field.
- **[low] `nilbots bots` lists names with no descriptions.** Whether `hunter`
  dodges had to be reverse-engineered from replays (it does not — that is the
  exploit the winning bot is built on). One line each would orient newcomers.
- **[low] `--swap` flips the scoreboard's point of view**; the parenthetical
  `W = hunter wins` is the only clue, and the agent nearly recorded a strong
  result as a bad one.
- **[low] NuGet README understates WASM prerequisites** — implies x64 Linux
  needs nothing extra, when it needs a wasi-sdk *or* Docker. `doctor`'s failure
  message is excellent; the README should match it.
- **[info] The site renders only via JavaScript**, so scripted/no-JS access gets
  the single word "nilbots". Irrelevant to humans with browsers.

## MEASURED: local↔server artifact parity FAILS, and here is why

Originally unverifiable (registration needed a browser). Once the headless auth
path below existed, a real submission was made and the answer is unambiguous:

```
Local artifact:   6fb40191276b2f435404452cd688da76f0201e54d9052e8dbe6ca3c474076371 (compiled)
Server artifact:  0178dcf82549043b0bbc7257622dda15083d62d8692b6b2e06658734adaff7b7
Parity:           DIFFERENT
```

Same machine, same wasi-sdk, same sources — so this is not "toolchain/sysroot
drift" as the CLI's message guesses. **Root cause: the emitted artifact embeds
absolute build paths.** `strings` on the local artifact yields
`/home/user/nilbots/src/BotArena.Guest` and `/home/user/nilbots/src/BotArena.Sdk`.
Local builds run from the caller's cache directory; server builds run isolated
as the `botbuild` user from a different directory — different path bytes,
different hash, every time. This also explains the gen-7 result where one bot
matched and another did not: parity is an accident of where each was built.

This matters because bit-identical local↔server is advertised on the NuGet page
and in `submit`'s own output, and "determinism is the product" (CLAUDE.md).

**FIXED** (owner-approved, DECISIONS #72). The controlled build project now sets
`PathMap` from `$(MSBuildProjectDirectory)` to the fixed virtual root
`/nilbots/bot`, plus `Deterministic` and `DebugType=none`, so the compiler never
sees where it actually ran.

Measured after the fix — two cold builds of identical sources under two
different cache roots (i.e. two different workspace paths):

```
build A (BOTARENA_HOME=…/repro-a): 78da86bc9ce7320989bf053abe5a0d78e3bfd48fab48b2e89c896631564001ea
build B (BOTARENA_HOME=…/repro-b): 78da86bc9ce7320989bf053abe5a0d78e3bfd48fab48b2e89c896631564001ea
```

`strings` now finds **no** `/home`, `/tmp` or `/root` path anywhere in the
artifact. Two bonuses fell out: artifacts shrank **2.54 MB → 998 KB (61%)**
because debug info dominated them, and player artifacts stop leaking our build
directories to anyone who downloads one. Fault reporting is unchanged — the
guest reports exception type + message, not line numbers (verified end to end:
`Fault s0: InvalidOperationException: deliberate fault for diagnostics`).

`BuildPipelineVersion` 1 → 2 invalidates every cached artifact, which is the
intended blast radius. Committed champion artifacts are frozen binaries and keep
working untouched. `scripts/e2e.sh` now builds the same bot under two cache roots
and fails if the hashes differ, so this cannot silently regress.

## Fixed: headless onboarding (the friend's-agent path)

`nilbots register` / `login` now accept `--email` / `--password` (plus optional
`--name`) and complete the **same** Authorization Code + PKCE grant with no
browser: the CLI authenticates against the JSON API, and because
`/connect/authorize` is satisfied by that cookie session it answers with the
redirect carrying the code, which the CLI reads off the `Location` header and
exchanges as usual. No new grant type, no weaker flow, no server change — the
browser is the only thing removed. Both are documented in `--help` and in
`nilbots help register` / `help login`.

This closes the blocker that made the whole "point a friend's agent at it"
scenario impossible: an autonomous agent in a container can now go from
`dotnet tool install` to a bot on the ladder without a human at a browser.
Verified end to end against a live server: register → `whoami` → build →
submit → server build → active version.

## Methodology note for future doc tests

The agent disclosed that its context was **pre-loaded by the harness with this
repo's `CLAUDE.md`** — it never read the repo from disk and deliberately did not
use it for mechanics or API questions, but it did use it to recognise that the
wasi-sdk at `/opt/botarena/wasi-sdk-29.0` was pre-provisioned by this container
rather than installed by the tool. So the WASM section is better-informed than a
real player's would be, and the "fresh machine" claim is untested. Future
docs-only tests must run with project instructions stripped from the agent's
context, or the isolation is nominal.

# Round 2 — the friend's-agent test (full flow, including registration)

Second run: an agent told it was "a friend who was sent the CLI", against a
private server, doing the whole journey *including account creation*, which the
first run could not reach. It got there — **rank #2 of 8 on the ladder**, a bot
scoring 184/192 (95.8%) against the built-ins on seeds never used for tuning —
but its verdict was **"partly"**, because it only finished by regex-mining the
docs prose out of the server's minified JS bundle and writing a reflection
dumper to recover the SDK API.

**A flaw in the harness, stated plainly:** the agent's `dotnet tool install`
returned *"Tool 'nilbots' is already installed"* — it silently reused the
**published 0.4.0** left by round 1 rather than the patched build from the local
feed. So its findings on missing headless auth, missing XML docs, broken
`--version` and the parity wording are all against the OLD binary and were
already fixed. A future run must `dotnet tool uninstall -g Nilbots` first, or
install to a private tool path. The findings below are the ones that survive
that correction — all reproduced against current source.

## Fixed in this batch

1. **[S1, worst moment] `register`/`login` printed NOTHING when piped, then hung
   for five minutes.** The fallback URL went to buffered stdout; piping is the
   default for agents and CI, so the guidance never arrived and the command
   looked dead. Now written to **stderr and flushed**, and it also prints the
   headless one-liner as an alternative. This alone would have stalled a less
   stubborn agent at step one.
2. **Parity message blamed the wrong thing and cited a file players cannot
   read** (`docs/DECISIONS.md`, repo-only). It now names the real usual cause and
   checks it for them: the CLI compares its bundled SDK against the server's
   `/api/meta` and, on a gap, says exactly that with the upgrade command. (The
   agent diagnosed this itself — CLI SDK 0.8.0 vs server 0.8.1 — which is
   precisely what the tool should have told it.)
3. **The served site was two days stale.** `web/dist` is what the app serves, and
   it still taught *"150 zone-ticks wins immediately"* — a pre-0.5 mechanic —
   while `DocsPage.tsx` had been corrected to the ControlPressure model. The
   agent trusted the served docs, as anyone would. Bundle rebuilt, and
   `DocDriftTests` now fails if `web/dist` is older than the docs source.
4. **Site docs still said `botarena new` / `botarena set`** — copy-paste gives
   "command not found" since the rename.
5. **`nilbots bots` listed names only**, so newcomers reverse-engineered
   behaviour from replays. Each built-in now has a one-line description
   including the weakness worth exploiting.
6. **`--rules` presented 24 names as equal choices.** The game and the research
   arms are now separated in `--help` and in the error message.

## Still open (ranked)

- **[high] No text mirror of the docs.** `curl /docs` returns an SPA shell; the
  public site renders the single word "nilbots" to anything without JavaScript.
  For a product whose players are largely agents, ship `/llms.txt` or a
  markdown mirror. This is the root cause of the mining the agent had to do.
- **[high] Ranked play is invisible to the CLI.** `nilbots set` is a LOCAL
  simulation; actually entering the ladder needs hand-written curl to
  `/api/matches/ranked` and `/api/leaderboard`, documented only in that buried
  block. The headline activity of the product deserves `nilbots rank` and
  `nilbots leaderboard`.
- **[med] `doctor` ignores the signed-in session** — prints "not signed in (no
  server yet)" moments after `whoami` succeeds, and silently swallows
  `--server`. The server-compatibility check that would have caught the SDK
  skew therefore never ran.
- **[med] Enemy cooldown is unobservable and the reconstruction is
  undocumented** (carried over from round 1; both agents independently built it
  from `VisibleEvents` + the 2-tick rule, and both called it decisive).
- **[low] Unknown API routes return 200 + SPA HTML**, so probing cannot use
  status codes.

## What the second agent praised

`doctor`, the scaffolded README ("the best documentation in the product"), the
`play --seeds` / `set` / `replay --summary` loop, in-process↔WASM agreement over
48 games, and the server surfacing its build log — which caught a real latent
nullable bug in its bot. The ladder data itself (per-set scores, rating deltas,
mirrored starts) it called great; only the path to it is hidden.
