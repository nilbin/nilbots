---
name: sheet-tuning
description: Iterate on Arc Relay tactical sheets, executor or mind behavior, pathing, tactics, and replay-visible gameplay through trace-first six-cell exhibits and phone galleries. Use when changing or debugging Arc Relay sheets, minds, compiler or engine behavior, or when the owner asks to show, watch, or review a tactical change. Never run a balance battery unless the owner explicitly requests one.
---

# Arc Relay iteration and sheet tuning

Iterate by watching concrete games. Treat an exhibit as visual evidence of
what a change does, never as a balance measurement.

## Non-negotiable rules

- Use `scripts/arc-relay-exhibit.sh` for the normal loop. Do not run
  `scripts/arc-relay-battery.sh`, a sweep, or make a balance verdict unless the
  owner explicitly requests that measurement.
- Trace the motivating replay scene before guessing. Make one honest change,
  capture comparable before/after evidence for that scene, then exhibit it.
- Never edit `src/`, `arena-bots/`, a sheet, or a layout while exhibit matches
  are running.
- Give every executor action a non-empty `debugMessage`. Behavior without a
  reason is a bug; follow `docs/EXECUTOR-SILENT-POLICY.md`.
- Report the six outcomes as records only. Six cells show behavior and do not
  establish that a change is better or balanced.
- Commit as work stabilizes. Finish with a clean tree and cite the final SHA.
- Deliver review through a public Cloudflare URL. Localhost is not delivery.

## Keep review and promotion separate

The default workflow is a **design review**, not a hosted promotion. A public
gallery is disposable evidence and does not need release-grade packaging.

During the fast design loop:

- run mind sources in-process through `scripts/arc-relay-exhibit.sh`;
- do **not** build or copy `bot.wasm`, update a build receipt, mint a frozen
  stock directory, bump a playlist/rating era/CLI version, regenerate clients,
  or run a release workflow;
- leave tracked WASM and immutable playlist definitions untouched. A branch
  whose mind source has moved is a review candidate, not merge-ready product;
- run focused compiler/runtime tests for the mechanism under review. Do not run
  the full solution, WASM parity, PostgreSQL, production web build, or release
  gauntlet merely to refresh gameplay replays;
- use one or two targeted exhibit cells while diagnosing. Build the standard
  six-cell exhibit once the behavior is ready for the owner to watch;
- reuse the published CLI and existing viewer. The exhibit script republishes
  its local CLI cache when `src/` is newer; that is a local iteration build,
  not a product release. Rebuild the viewer only when viewer code/assets moved.

Enter the **promotion gate** only when the owner explicitly asks to merge,
promote, host, rank, freeze, or release the candidate, or when the task itself
is about WASM/determinism. Then reconcile tracked artifacts through the
controlled WASM build, prove in-process/WASM gameplay parity, mint immutable
artifact and playlist versions as required, decide rating continuity from
evidence, bump compatibility versions, and run the applicable full suites.

Do not infer promotion from “show me,” “try it,” “gallery,” or approval of the
gameplay idea. Promotion approval is a separate product decision.

## Fast exhibit loop

Use the standard review cells in this order:

```text
w-9001 w-9002 e-9005 e-9009 w-9012 e-9004
```

Run the complete pipeline from the repository root:

```bash
BATTERY_OUT=/tmp/arc-batteries \
EXHIBIT_TITLE="My change, described" \
scripts/arc-relay-exhibit.sh mychange1 \
  arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/hunter-v1.json \
  arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/wellwright-v1.json \
  /tmp/arc-batteries/site \
  w-9001 w-9002 e-9005 e-9009 w-9012 e-9004
```

The script runs matches and broadcasts in parallel, builds the gallery once,
and atomically swaps it into `SITE_DIR`. Reuse that directory to update a live
gallery without changing its URL. Expect roughly one minute for six cells on
the reference machine.

When reviewing the current Tapline product line, set
`EXHIBIT_LOOP_PROFILE=tapline-3` and use the current Tapline-native sheet pair
(`hunter-tapline-v2.json` / `wellwright-tapline-v2.json`). Tapline-3 is the
versioned collision contract that permits an acyclic convoy to enter tiles
vacated in the same tick. Do not silently review that engine behavior on the
historical Tapline-1/2 contracts.

Coordinated pathing is the product and exhibit default. Use
`EXHIBIT_MATCH_ARGS="--mind-stepper greedy"` only for an explicitly labelled
historical-control diagnostic; never use it for an owner review gallery unless
the owner asks to compare steppers. Set `EXHIBIT_CARDS=/path/to/cards.json` for
curated explanations; otherwise the script writes honest minimal cards. The
card schema is documented in the script header.

The published CLI is cached at `$BATTERY_OUT/botarena-pub`. The exhibit script
automatically republishes when tracked source under `src/` is newer than the
cached binary; do not manually delete the cache as normal ceremony. Changes
only under `arena-bots/` need no republish—the mind compiles in-process per
match.

That in-process compilation is intentional. Never replace it with a controlled
WASM build merely to make a review gallery.

Never edit inputs while the script is between `running ...` and completion.

## Trace before changing

Raw canonical replays live at
`$BATTERY_OUT/<name>-<cell>/replay.json.gz`. Start with the owner's exact unit
and tick window when one is supplied.

```bash
scripts/arc-relay-fight-trace.py REPLAY --team N [--unit U] [--timeline]
scripts/arc-relay-unit-audit.py REPLAY [--team N]
scripts/arc-relay-route-read.py REPLAY [--team N] [--reason-contains INTENT]
scripts/arc-relay-traffic-read.py REPLAY [--team N]
scripts/arc-relay-declare-read.py REPLAY [--team N]
scripts/arc-relay-strike-cancel-audit.py REPLAY
scripts/arc-relay-state-digest.py REPLAY
```

- Use `fight-trace` for the reason timeline, fight interruptions, and
  `[allocation: reason]` declines.
- Use `unit-audit` for confinement, territory, pendulum, and lone-kill ledgers.
- Use `route-read` to separate selected move-order body-ticks, movement-channel
  admission, submitted/successful movement, closest own-Well approach, and
  explicit route bypass/no-path reasons. Filter by a semantic intent such as
  `centre-push` when comparing the same order across maps.
- Use `traffic-read` for flaps, reversals, chokes, and blockades.
- Use `declare-read` for declaration latency and cancellation causes.
- Use `strike-cancel-audit` to re-verify every strike cancellation against the
  rules.
- Use `state-digest` to compare gameplay identity across renamed or
  provenance-only replays; `replayHash` is not suitable for that comparison.

In the viewer, select the body under investigation. Its follow camera, order
and action chip, cyan destination lens, next step, and crossed-blades engaged
mark should agree with the trace.

## Curated cards

Write cards before building; do not build, inspect outcomes, and then choose
which cells deserve explanation. Use manifest ids in standard-cell order:

```json
{
  "title": "What changed",
  "cards": [{
    "id": "sample-01",
    "title": "w-9001 — win at t320",
    "win": true,
    "opponent": "Wellwright: stock control doctrine",
    "score": "final match score and tactic result",
    "trigger": "the visible condition that starts the play",
    "tactic": "what the sheet attempts",
    "counterplay": "how the opponent can answer",
    "fallback": "when and how participants return to baseline",
    "watch": "specific ticks, bodies, or events"
  }]
}
```

State the actual opposing sheet and doctrine. Describe trigger, intent,
counterplay, fallback, and the concrete causal evidence to watch. Never imply a
six-cell record is a balance conclusion.

## Serve for phone review

Choose an unused port; 8950-8953 may belong to other sessions. Never stop a
server or tunnel you did not start.

Agent execution sessions reap child processes at turn completion—even children
started with `&` or `nohup`. For a gallery that must survive handoff, start both
processes in uniquely named detached `screen` sessions from the repository
root:

```bash
screen -dmS nilbots-arc-gallery-server-8960 /bin/zsh -c \
  'exec python3 scripts/serve-gallery.py 8960 \
    --directory /tmp/arc-batteries/site \
    >> /tmp/arc-gallery-8960-server.log 2>&1'

screen -dmS nilbots-arc-gallery-tunnel-8960 /bin/zsh -c \
  'exec cloudflared tunnel --url http://127.0.0.1:8960 \
    >> /tmp/arc-gallery-8960-tunnel.log 2>&1'

screen -ls
rg -o 'https://[a-z0-9-]+\.trycloudflare\.com' \
  /tmp/arc-gallery-8960-tunnel.log
```

Capture the `https://*.trycloudflare.com` URL from Cloudflare output and put it
in the report. Verify both named sessions still appear from a later command;
starting them is not proof they survived the tool boundary. Before delivery,
load that exact URL in a phone-sized real browser, wait for the replay UI rather
than only checking HTTP 200, and confirm the index plus one replay payload load
without console or page errors. Follow the `replay-review` skill for broader
viewer QA. When review ends, stop only those exact sessions with
`screen -S <name> -X quit`.

## Owner-authorized measurement only

If—and only if—the owner explicitly calls for a battery or balance read:

1. Run the pre-change sheet on the current binary first. If it no longer
   reproduces its old behavior, attribute the regression to code before tuning
   configuration.
2. Change one factor from a fixed base and keep one published binary for the
   whole cohort.
3. Use unit audits and replay review alongside scores; bars-clean and winning
   do not prove healthy behavior.
4. Present measurements without making the product verdict for the owner.

## Known sheet traps

- **Engagement participants:** an order aimed at an engagement that excludes
  the role silently leaves that role without a fight policy.
- **Terminal routes:** a route with `route[0] != route[^1]` parks at its last
  waypoint. Close it for patrol behavior and re-pin `layout.sha256` in every
  bound playbook.
- **Withdraw destinations:** resolving to the current tile suppresses fighting
  without creating movement. Withdraw away from threats.
- **Acquisition vetoes:** gating fight acquisition often creates passivity in
  zones with remembered enemies. Gate chases or disengagement instead.
- **Sheet paths:** temporary control sheets must live beside the real playbooks
  so relative `library` and `layout` paths resolve.

## Source map

- Sheets and layouts:
  `arena-bots/arc-relay/tactical-playbook-v1-2026-08-03/playbooks/`
- Grammar contract: `docs/SHEET-AUTHORING.md`
- Design history: `docs/SPEC-GHOST-DOCTRINE-V3.md`, `docs/DECISIONS.md`
- Mind: `arena-bots/arc-relay/tactical-playbook-mind-v1/`
- Shared compiler: `src/BotArena.TacticalPlaybooks/`
- Engine: `src/BotArena.Engine/`
