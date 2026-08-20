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
- Every gallery must identify both sides by the authored `sheetId` in its
  index/card copy, replay header, score bug, and end card. A mind name or a
  package filename such as `orders`, `sheet`, or `playbook` is not a sheet
  identity. The exhibit harness rejects this leak; do not bypass the check.
- Scope is literal. If the owner asks to review a combined candidate, every
  cell uses that exact combined profile and sheet against the named control;
  do not mix in isolated arms, old experiments, or historical replays. Audit
  the generated sample manifest before handing over the URL.
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
- leave tracked WASM and immutable playlist definitions untouched. A source
  change may merge to `main` with the first-party WASM mirror temporarily
  stale; hosted sheets execute the registered tactical mind in-process;
- run focused compiler/runtime tests for the mechanism under review. Do not run
  the full solution, WASM parity, PostgreSQL, production web build, or release
  gauntlet merely to refresh gameplay replays;
- use one or two targeted exhibit cells while diagnosing. Build the standard
  six-cell exhibit once the behavior is ready for the owner to watch;
- never bypass the exhibit's movement-sanity or felt-eligibility gates for an
  owner review. A diagnostic that intentionally trips one stays local and is
  labelled diagnostic; it is not a review gallery;
- reuse the published CLI and existing viewer. The exhibit script republishes
  its local CLI cache when `src/` is newer; that is a local iteration build,
  not a product release. Rebuild the viewer only when viewer code/assets moved.

When an App/API/editor change genuinely needs the containerized site for a
browser smoke, build the Docker `runtime` target (the default in
`docker-compose.yml`). That target publishes the trusted App and consumes
committed WASM artifacts; it must not build clang/LLVM, wasi-sdk,
`build-wasm-guest.sh`, or the controlled submission cache. Seeing any of those
in an ordinary browser-review build means the compiler/toolchain target was
selected accidentally: stop and fix the build boundary instead of waiting for
an irrelevant WASM build. Use the `compiler` target only when the custom-mind
submission path itself is under review.

Enter the **deployment checkpoint** only when the owner explicitly asks to
deploy or release, or when the task itself changes the custom-mind sandbox,
SDK/Guest ABI, controlled compiler, or WASM runtime. A behavior-changing
tactical-mind deployment mints a new current playlist version before the new
worker binary ships. Completed historical matches keep their stored replay;
historical Arc Relay playlists are replay-only and are not re-simulated with
today's in-process mind. At the checkpoint, reconcile the tracked first-party
WASM mirror through the controlled build and run the release-only cross-runtime
parity check. The mirror is audit/future-custom-mind evidence, not the hosted
sheet executor.

Do not infer a deployment checkpoint from “show me,” “try it,” “gallery,”
approval of the gameplay idea, merging to `main`, or calling a version the new
default. Deployment approval is a separate product decision.

## Local App review handoff

When the owner will use the local site, editor, or Practice lane, hand over the
processes that are actually running—not merely current source or a successful
build. A long-lived App process keeps the .NET assemblies it loaded at startup;
building or committing newer code does not update that process. The App also
serves `web/dist`, so a web source change is not visible there until the web
production build has refreshed that directory.

Before giving the owner a local URL:

1. Rebuild every layer changed by the task. Build the relevant .NET projects
   after App, engine, compiler, mind, or server changes; run `npm run build`
   from `web/` after site/editor/viewer source or assets change. Do not rebuild
   an unchanged layer as ceremony.
2. Restart the exact local App or frontend process when it has already loaded a
   changed layer. Preserve the configured persistent PostgreSQL container,
   object store, authentication environment, and other local data; an App
   restart is not authorization to recreate the database. A static `web/dist`
   refresh normally needs no App restart, while a development server may need
   one when its configuration or dependencies changed.
3. Resolve the current listener and process before stopping anything. Stop only
   the session/PID belonging to this review, account for surviving child
   processes that may still hold the port, and verify the replacement has a new
   start time and is listening on the intended URL.
4. From a separate command, load the exact local URL and exercise one cheap
   end-to-end path that uses the changed layer. For Practice behavior, confirm
   the newly created match/replay was produced after the replacement process
   started; an old replay does not prove the new runtime is loaded.

Record the running build/commit in the server log or session name when
practical. If the handed-over server predates the implementation under review,
the handoff has failed even when all tests passed. Galleries served from their
own built directory remain independent and do not require restarting the App.

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

The exhibit harness defaults to the current `tapline-8` product line. It uses
the accepted Tapline geometry and coordinated movement plus the promoted
1/2/4 Core economy, veteran command, public respawn waves, seven-class roster
floor, durable deployments, tenfold combat scale, immediate post-combat Arc
Toss release at three flight tiles per tick, and source-local Well supply:
each initial Core remains its Well's sole active Core until banked, then that
Well publicly recharges for 46 ticks. Hosted playlist v24 remains
the immutable `tapline-6` deployment until an owner-authorized promotion
checkpoint. Pin an older
`EXHIBIT_LOOP_PROFILE` only for an explicitly labelled historical
control. Do not use the now-inadmissible Hunter/Wellwright sheets as current
controls; choose a current seven-class sheet and name it on every card.

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

The exhibit is not publishable merely because every process exited zero. Its
raw-replay motion gate rejects closed allied movement cycles, uncontested
recovery with no health/geographic progress, and a move episode that remains
in a local orbit beyond the traffic-debt escalation window. The gallery
builder then applies
the frozen felt-degeneracy bars, including Core-level home non-progress across
handoffs and equal-distance movement. Do not pass
`--skip-arc-relay-eligibility` in this workflow. Finally inspect every end tick:
multiple horizon games fail the exhibit by default and require tracing even
when no bar trips. `EXHIBIT_ALLOW_MULTIPLE_HORIZON=1` exists only for an
explicitly labelled horizon-focused diagnostic, never as acceptance ceremony.

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

## Decide whether the sheet or movement owns a fault

Classify the scene before editing either side of the seam:

1. Read the selected order path, semantic destination, and authored urgency
   from `debugMessage`. A wrong order, goal, condition, capacity, urgency, or
   posture is a sheet defect.
2. Read the `epibt-*`, `goal-*`, `next-*`, traffic, and avoidance clauses. A
   correct authored intent that produces no plausible physical action, repeats
   a local circuit, or contradicts its own right-of-way is a planner defect.
3. Read the authoritative movement event and tile state. A plausible command
   rejected by terrain, an enemy home region, a construct, a projectile, or an
   occupied tile is engine/world evidence; decide whether the sheet chose an
   impossible place or the planner failed to react.

Authored urgency is absolute. Queue debt, passage lease, blocked age,
activation time, and unit id may order peers only inside one urgency tier. Do
not add carrier-, class-, role-, objective-, map-, or coordinate-specific
traffic precedence. Urgency also owns STANDING ground: a body resting on a
strictly more urgent intent's claimed tile yields it (planner
YieldRestingClaims, DECISIONS #345) — same-tier rests are never unseated. Carrying may affect route safety, but never who wins a
contested allied tile. A trace that both claims
`traffic-right-of-way-X-Y` and retains `avoid-denied-X-Y` is contradictory and
must fail the motion gate.

Treat scene tests as evidence, not snapshots to silence. Preserve the sheet and
fix the executor when its selected intent is sound. Update the sheet and its
scene only after the trace proves an authored fault, such as fewer connected
legal zone tiles than simultaneous bodies, a via endpoint inside or behind an
unenterable region, or the wrong urgency. Keep the scene test and rewrite its
comment to name the caught behavior and ownership decision; do not merely
relax its threshold. Then run the exact scene, the full movement-scene gate,
`scripts/arc-relay-motion-sanity.py`, and finally the standard six-cell exhibit.

Every product sheet must resolve its own `mapPlan` directly into the current
map's native directory. Do not rely on the App embedding a newer layout than
the path used by the CLI or browser fixture, and never project old geometry.
For every movement zone, count its largest corner-strict connected set of
legal, non-spawn-protected tiles against the bodies its order tree can send.
Audit both team bindings; a rectangle that merely contains some floor is not
a viable staging area.

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
