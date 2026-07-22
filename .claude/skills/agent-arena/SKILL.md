---
name: agent-arena
description: Evaluate Bot Arena end to end by having subagents write bots from the docs alone, submit them via the CLI, and fight a ranked tournament. Reports a leaderboard plus developer-experience friction. Use when asked to evaluate the CLI/platform or run the agent tournament.
---

# Agent Arena: multi-agent evaluation tournament

Goal: N subagents (default 4) each build a bot **using only the player-facing docs**
(site `/docs`, `templates/botarena-bot/README.md` — NOT the engine source), then
compete. This evaluates both the docs and the full pipeline; the tournament is the fun part.

## 1. Boot the environment

```bash
bash scripts/setup.sh
service postgresql start   # create role/db per CLAUDE.md if first boot
WASI_SDK_PATH=/opt/botarena/wasi-sdk-29.0 ASPNETCORE_URLS=http://127.0.0.1:8080 \
  BOTARENA_DATA=$PWD/var nohup dotnet run --project src/BotArena.App > /tmp/app.log 2>&1 &
```
Verify `curl -s localhost:8080/api/meta` returns 200 before continuing.

## 2. Fan out competitor agents (parallel, isolation: worktree not needed)

Give each agent: a persona (aggressive / evasive / ambusher / adaptive), a distinct
bot name + accent, a working dir `sandbox/agents/<name>`, and this brief:

> Read site docs (`curl -s localhost:8080/docs` renders SPA — instead read
> `web/src/site/pages/DocsPage.tsx` and `templates/botarena-bot/README.md`) and
> `src/BotArena.Sdk/` public API only. Create your bot with
> `dotnet run --project src/BotArena.Cli -- new <Name>`, iterate with
> `-- play --bot . --opponent hunter|coward|wander --seed <n>` (test ≥3 seeds/opponents),
> then STOP. Report: your strategy, win rates, and every point of friction —
> confusing docs, bad errors, missing commands. Your final text is data.

Registration (cookie auth is simplest for agents): each agent registers a user via
`curl -c jar -d '{"displayName":...,"email":...,"password":...}' /api/accounts/register`,
creates the bot, and submits sources via `POST /api/bots/{id}/versions`
(files JSON from its .cs sources; poll until Built; record artifact-hash parity vs
`-- build .` local hash).

## 3. Tournament

Round-robin: for every pair, `POST /api/matches/ranked {botId, opponentBotId}` with
the owner's cookie jar; wait until each set's 6 games complete
(`GET /api/matchsets/{id}`; broadcasts lag execution — poll `Revealed`).
Also run each bot in one ranked set vs `hunter` for a baseline.

## 3.5 Improvement iterations (2 rounds)

After the first round-robin, send each agent its lost matches' replays
(`GET /api/matches/{id}/replay` — events + its own per-tick debug/visible data):
analyze why it lost, improve the bot, resubmit, re-run the round-robin.
Elo accumulates across rounds; the drama is the point.

## 3.6 Crown and KEEP the champion

The final #1 becomes a permanent built-in opponent:
- Copy its sources to `champions/<slug>/` and its server artifact
  (`var/artifacts/<hash>.wasm`) to `champions/<slug>/bot.wasm` (this dir IS
  tracked — `artifacts/` is not). Add a README noting date, elo, record, strategy.
- It is playable locally forever: `botarena play --opponent champions/<slug>/bot.wasm`.
- Commit + push. Future tournaments must beat the reigning champion (include it
  in the bracket via the same .wasm path).

## 4. Report (the deliverable)

- Final leaderboard (`GET /api/leaderboard`) with set scores and elo.
- Per-bot: strategy summary, baseline-vs-hunter result, artifact parity.
- **DX findings ranked by severity**: doc gaps, bad error messages, CLI friction,
  submission/build failures — each with the exact reproduction. File none silently.
- Fun: name a champion; quote the best debug lines from replays
  (`GET /api/matches/{id}/replay` → `ticks[].bots[].debug`).

Clean up: kill the app by PID (never `pkill -f` — see CLAUDE.md), leave DB data
(it seeds the next run's opponents).
