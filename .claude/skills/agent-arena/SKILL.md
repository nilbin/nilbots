---
name: agent-arena
description: Evaluate Bot Arena end to end by having subagents write bots from the docs alone, submit them via the CLI, and fight a ranked tournament. Reports a leaderboard plus developer-experience friction. Use when asked to evaluate the CLI/platform or run the agent tournament.
---

# Agent Arena: multi-agent evaluation tournament

Goal: challenger agent(s) build bots **using only the player-facing docs**
(site `/docs`, `templates/botarena-bot/README.md` — NOT the engine source), then
fight the reigning champions for the title. This evaluates the docs and the full
pipeline; the tournament is the fun part.

**Default: ONE challenger.** A tournament agent costs ~200-400k tokens per
phase, so a 3-agent bracket runs 1.5-2M+ and triples wall-clock (and restart
exposure). Since champions are seeded server bots (DECISIONS #43), a single
challenger vs the champion lineage is a full title fight. Use a multi-agent
bracket (3-4 personas) ONLY when explicitly requested, or when a balance
verdict needs archetype diversity (distinct doctrines fighting each other —
frozen champions can't co-evolve).

## 1. Boot the environment

```bash
bash scripts/setup.sh
service postgresql start   # create role/db per CLAUDE.md if first boot
WASI_SDK_PATH=/opt/botarena/wasi-sdk-29.0 ASPNETCORE_URLS=http://127.0.0.1:8080 \
  BOTARENA_DATA=$PWD/var \
  BOTARENA_BROADCAST_TPS=250 BOTARENA_BROADCAST_DELAY_SECONDS=0 \
  BOTARENA_COMPILE_WORKERS=3 \
  nohup dotnet run --project src/BotArena.App > /tmp/app.log 2>&1 &
```
Verify `curl -s localhost:8080/api/meta` returns 200 before continuing.

The three BOTARENA_* knobs are the eval-speed configuration (DECISIONS #41/#42):
results reveal ~instantly (a 500-tick match broadcasts in 2 s instead of 100 s)
and up to 3 submissions compile in parallel without blocking match execution.
Production keeps the defaults (5 ticks/s, 3 s countdown, 1 compile lane) — never
set these on a server humans spectate. The no-spoiler API semantics are
unchanged either way; polls just resolve much sooner.

## 1.5 Fresh generation

Archive the previous generation's agent state or `scripts/tournament-drive.py`
will resurrect its bots into the bracket:

```bash
[ -d sandbox/agents ] && mv sandbox/agents "sandbox/agents-prev-$(date +%s)"
mkdir -p sandbox/agents
```

Old generations' server bots keep their accounts/elo (that's history); only the
state.json files decide who plays. Note: champion server-bots start at elo 1200
regardless of the elo noted in their README — the ladder position is earned per
deployment.

### Running a rules-experiment tournament (e.g. the energy re-test)

Add `BOTARENA_RULES=<arm>` to the app env in step 1 (the worker log line
confirms the ruleset) and have agents test locally with `--rules <arm>`.
Player-facing docs only describe SHIPPED rules, so append the experiment spec
to every agent brief. For the HILL re-test (the named gen-4 confirmation,
DECISIONS #49):

> EXPERIMENTAL RULES for this tournament (on top of documented v0.3): ZONE
> CONTROL. The map declares zone tiles (see `context.ZoneTiles`; your score is
> `context.MyZoneTicks`, theirs `context.EnemyZoneTicks` — both public). Each
> tick you stand on a zone tile while alive adds 1 zone-tick. Holding 150
> zone-ticks wins immediately (Domination). At tick 500 the tiebreak is
> zone-ticks → health → damage: a health lead WITHOUT the zone loses. The
> zone is the objective; shooting is how you hold it.

For energy re-tests:

> EXPERIMENTAL RULES for this tournament (on top of the documented v0.2): you
> have an energy meter, visible as `context.Energy` (max 6). Each shot costs 2
> energy; +1 energy regenerates every 3 ticks (cap 6). A Shoot without enough
> energy becomes Wait with an OnCooldown result. Enemy energy is not
> observable — count their shots. Sustained fire is therefore ~1 shot per 6
> ticks; bursts of 3 are available from full. Manage it or run dry mid-fight.

## 2. Launch the challenger(s)

Give each agent: a persona (one challenger: pick the archetype most relevant
to what this run tests; bracket mode: aggressive / evasive / ambusher /
adaptive), a distinct bot name + accent, a working dir `sandbox/agents/<name>`,
and this brief:

> Read site docs (`curl -s localhost:8080/docs` renders SPA — instead read
> `web/src/site/pages/DocsPage.tsx` and `templates/botarena-bot/README.md`) and
> `src/BotArena.Sdk/` public API only. Create your bot with
> `dotnet run --project src/BotArena.Cli -- new <Name>`, iterate fast with
> `-- play --runtime in-process --bot . --opponent hunter --seeds 7,42,1337`
> (~2 s builds, batch seed tables, same engine; verify in default WASM mode
> before submitting), analyze losses with `-- replay <file> --summary`
> (never parse raw replay JSON), and rehearse the ranked format with
> `-- set --bot . --opponent <spec>`. Then spar the REIGNING CHAMPION(S) in
> WASM mode: `-- set --bot . --opponent champions/<slug>/bot.wasm`
> (test ≥3 seeds each; the champions are the bar, the built-ins are training
> wheels). Then STOP. Report: your strategy, win rates incl. vs champion, and
> every point of friction — confusing docs, bad errors, missing commands. Your
> final text is data.

Registration (cookie auth is simplest for agents): each agent registers a user via
`curl -c jar -d '{"displayName":...,"email":...,"password":...}' /api/accounts/register`,
creates the bot, and submits sources via `POST /api/bots/{id}/versions`
(files JSON from its .cs sources; poll until Built; record artifact-hash parity vs
`-- build .` local hash).

## 3. Tournament

The bracket is the agents PLUS every reigning champion. Champions are seeded as
system-owned server bots automatically at app startup (`ChampionSeeder` reads
`champions/*/champion.json` + `bot.wasm`; find their botIds via
`GET /api/bots` — slug = the champions/ directory name).

Round-robin: `python3 scripts/tournament-drive.py` fires every agent-vs-agent
and agent-vs-champion ranked set (champions discovered by slug on the server;
each agent's state.json supplies its botId + cookie jar), waits for all sets,
and prints scores + the leaderboard. Rerun it after each improvement
iteration — elo accumulates. The champions' set results ARE the baseline —
don't bother with hunter sets.

Single-challenger experiment runs: also run a MIRROR set — the challenger vs
its own submitted artifact (`botarena set --bot <srcDir> --opponent
var/artifacts/<serverHash>.wasm --rules <exp>`) — as the aware-vs-aware data
point a one-agent bracket otherwise lacks.

## 3.5 Improvement iterations (3-4 rounds)

After the first round-robin, send each agent its lost matches' replays
(`GET /api/matches/{id}/replay` — events + its own per-tick debug/visible data):
analyze why it lost, improve the bot, resubmit, re-run the round-robin. Repeat for 3-4 total
iterations (stop early only if the leaderboard order is unchanged twice running).
Elo accumulates across rounds; the drama is the point.

## 3.6 Crown only a dethroner — and KEEP it

**If a reigning champion finishes #1, there is no new champion**: report
"champion defended the title" (that's a headline, not a failure) and skip this
step. A new generation is crowned only when an agent ends above every reigning
champion on the final leaderboard.

To crown, make the winner a permanent opponent in `champions/<slug>/`
(slug: `<botname>-genN`, lowercase; this dir IS tracked — `artifacts/` is not):
- Its sources, its server artifact (`var/artifacts/<hash>.wasm`) as `bot.wasm`,
  a README (date, elo, record, strategy), and a `champion.json` manifest —
  `{"name", "entryType", "accent", "crownedAt", "elo", "record"}` — which is
  what `ChampionSeeder` reads to put it on every future ladder automatically.
- It is playable locally forever: `botarena play --opponent champions/<slug>/bot.wasm`.
- Commit + push. Keep dethroned generations in place — the ladder of champions
  is the product's history, and future tournaments fight ALL of them.

## 4. Report (the deliverable)

- Final leaderboard (`GET /api/leaderboard`) with set scores and elo — lead
  with where the reigning champion(s) placed.
- Per-bot: strategy summary, result vs the reigning champion, artifact parity.
- **DX findings ranked by severity**: doc gaps, bad error messages, CLI friction,
  submission/build failures — each with the exact reproduction. File none silently.
- Fun: name a champion; quote the best debug lines from replays
  (`GET /api/matches/{id}/replay` → `ticks[].bots[].debug`).

Clean up: kill the app by PID (never `pkill -f` — see CLAUDE.md), leave DB data
(it seeds the next run's opponents).
