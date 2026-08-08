---
name: agent-arena
description: Evaluate nilbots end to end by having subagents write bots from the docs alone, submit them via the CLI, and fight a ranked tournament. Reports a leaderboard plus developer-experience friction. Use when asked to evaluate the CLI/platform or run the agent tournament.
---

# Agent Arena: multi-agent evaluation tournament

Goal: challenger agent(s) build bots **using only the player-facing docs**
(site `/docs`, `templates/botarena-bot/README.md` — NOT the engine source), then
fight a tournament. Under shipped rules they challenge reigning champions for
the title. Under a substantial rules experiment, a rules-native cohort fights
itself for the primary product verdict while historical champions act as
non-voting sentinels. This evaluates the docs and the full pipeline; the
tournament is the fun part.

**Legacy Duel default: ONE challenger.** A tournament agent costs ~200-400k tokens per
phase, so a 3-agent bracket runs 1.5-2M+ and triples wall-clock (and restart
exposure). Since champions are seeded server bots (DECISIONS #43), a single
challenger vs the champion lineage is a full title fight. Use a multi-agent
bracket (3-4 personas) ONLY when explicitly requested, or when a balance
verdict needs archetype diversity. A substantial-rules ship verdict is that
exception: use at least four independently authored or substantially adapted
candidate-aware doctrines, because frozen champions cannot reveal a strategy
space they were never written to use.

Frontline Labs uses the separate **cohort sprint** below. It is setless and
unranked, so legacy ladder, Elo, champion-seeding, and crowning semantics do
not apply.

## Run length (pick before launching, per the request)

- **trial** — a pipeline shakedown before committing to a long run: one
  challenger with a TIME-BOXED brief (see §2), one round-robin + mirror set,
  NO improvement iterations, then the report. It answers "are the mechanics,
  logs, and docs on point?", not "who is champion" — losing every set is a
  valid trial outcome. A healthy trial PROMOTES in place: keep the agent's
  state.json, message the agent to iterate, rerun the driver — elo
  accumulates, nothing restarts.
- **standard** (the default) — trial shape plus ONE improvement iteration,
  then a final round. Cost intuition: round-robins are nearly free (server
  matches + the driver, no agent tokens); iterations are the expensive part
  (~200k tokens per agent each). One adapt-and-rematch cycle answers "does
  counter-play change the picture?"; further cycles are mostly drama.
- **full** — 2-4 iterations and/or a multi-agent bracket; explicit request
  only. Drop champion pairings from later rounds once they're proven
  uninformative (0-6 sweeps every time) — rerunning them is elo noise.
- **cohort sprint** — the Frontline Labs default: four independent doctrines,
  one authoring pass, zero strategy-improvement passes, then one mirrored
  native round-robin. Mechanical compile/contract/fault repairs are allowed
  without opponent results. If a shared brief or scaffold defect needs a
  repair pass, give every author the same opportunity and archive all
  revisions. This is a T1/T2/DX and exploratory screen, not a bot-quality
  proof, numeric balance authority, or ship verdict.

## Frontline Labs cohort sprint

Do not infer mechanic strength from a one-pass cohort's standings. Before a
Frontline run may support numeric balance claims, every voting entrant must
pass pre-registered capability gates relevant to its doctrine, receive the
same small improvement budget, and run unchanged across mirrored A/B arms and
holdout seeds. Keep every revision. A failed doctrine demonstration is a bot
finding; reroute the rules verdict rather than calling the mechanic weak.

Use the cumulative T1–T8 individual ladder and independent C0–C5 coordination
grades in `docs/BOT-CAPABILITY-AND-SOLVABILITY.md`. Probe definitions and the
canonical evidence shape live in `docs/BOT-QUALIFICATION-SUITE.md`. State the
minimum T/C grade before authoring or selecting entrants. The first Frontline
pilot targets at least four independent cumulative T4+ lineages, including
T5-capable policies where available. T7/T8, automated best-response search,
and equilibrium estimation are explicitly deferred until that credible
population exists.

Population targets are tier-dependent. T1/T2 and most T3 artifacts are
calibration instruments: retain a small canonical set of behaviorally distinct
archetypes that passes Tn and demonstrably fails T(n+1). At the T5/T6 verdict
band, commission independently authored doctrine briefs and continue until
there are at least six effective doctrines, the declared strategy cells are
covered, and leave-one-doctrine-out conclusions are stable. Similar payoff
rows plus similar dynamics/action signatures count as redundancy evidence;
artifact count alone is never population breadth. Preserve intermediate
passing revisions, but never count revisions of one lineage as independent
policies. Never infer a tier from tournament wins.

Five separately versioned dynamic suites now exist:

```bash
# Frozen positional component:
nilbots experiment frontline-labs qualify \
  --bot <generic-project-or-wasm> \
  --suite frontline-qualification-1 \
  --out <evidence-dir>

# Frozen WASM-only foundation component:
nilbots experiment frontline-labs qualify \
  --bot <generic-wasm> \
  --suite frontline-qualification-2 \
  --out <evidence-dir>

# Cumulative T2 union profile:
nilbots experiment frontline-labs qualify \
  --bot <generic-wasm> \
  --suite frontline-qualification-3 \
  --out <evidence-dir>

# Cumulative T3 tactical profile (reruns exact T2):
nilbots experiment frontline-labs qualify \
  --bot <generic-wasm> \
  --suite frontline-qualification-4 \
  --out <evidence-dir>

# Current cumulative T4 positional profile (reruns exact T3):
nilbots experiment frontline-labs qualify \
  --bot <generic-wasm> \
  --suite frontline-qualification-5 \
  --out <evidence-dir>
```

Suite 1 mirrors the `entry-initiative` probe; retain it as immutable T4
component evidence and never label one passing artifact T4 by itself. Suite 2
repeats both assignments under the automatic-companion topology and requires
verified identical replay hashes, zero faults, and the declared automatic
child life. Its current report has `profileComplete: false`, deliberately
awards no tier, and is not balance-evidence eligible. Use it to separate
current-toolchain contract competence from strategy, not as T1/T2 proof.

Suite 3 is the current prerequisite for population work. It exercises
non-default participant identities and all three unit slots per team, useful
automatic child lives, objective navigation/holding, direct fire,
straight-projectile evasion, and explicit fabrication under both assignments.
A clean pass awards T2 under
`frontline-duel-depth-union-t2-v1`; it remains authoring/fun-floor evidence,
not a numeric balance vote.

Suite 4 reruns and hash-links the exact suite-3 prerequisite, then tests a
required legal curve, strict wall termination, declared remaining-range
cadence, missed-shot cooldown tempo, and local transform safety from both
assignments. A clean pass awards T3 under
`frontline-duel-depth-union-t3-v1`; it also remains below the numeric-balance
voting floor. Run suite 3 directly for an assigned T2 boundary and suite 4 for
an assigned T3 boundary.

Suite 5 reruns and hash-links exact T3, then tests suppression, proactive
pressure entry, objective-preserving response, front rotation, and the
thin-fronts holdout from both assignments. A clean pass awards T4 under
`frontline-duel-depth-union-t4-v1` and sets entrant-level balance eligibility.
That does not waive the independent-lineage, study-block, or evidence-layer
requirements. Use suite 5 for every directional-pilot author.

Every Balance Lab entrant must pin
the exact suite/version, qualification profile and contract fingerprint,
evidence-file SHA-256, artifact hash, and awarded T/C values. Never copy a
tier label between profiles or seasons. A new strategically necessary
capability such as Air or FFA coalition play requires a new/extended profile;
historical qualification remains valid only for its original profile.

The causal manual-versus-automatic factorial requires one union-qualified
population that can handle explicit Fabricate/rebuild and declared automatic
activation/return. Rules-native product comparisons use separately authored
or adapted populations and must not be mislabeled as the causal A/B.

Do not boot the hosted App or use `scripts/tournament-drive.py` for Labs. The
hosted path has quotas and no ladder; the exact local generic runner is the
evaluation boundary.

Do not rerun bot authoring for an ordinary numeric or rules-policy A/B. Reuse
the frozen source and WASM artifacts from the most relevant retained cohort so
only the declared rules variable changes. Commission a fresh rules-native
cohort only when actions, observations, objectives, or strategic affordances
change enough that the retained bots cannot exercise the hypothesis. For a
narrow falsification screen, adapt the smallest retained micro-cohort that
covers the competing policies; promote to a four-doctrine sprint only if the
screen survives.

For duel-depth map A/Bs, use one frozen artifact set with
`--duel-map current`, `--duel-map thin-fronts`, and
`--duel-map outer-shoulder-bypass`. These arms share one-bend rules and differ
by content-identified map fingerprints. Do not treat a map win table as
balance evidence until the voting artifacts pass the relevant qualification
probes.

For new retained population work, give every independent author
`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, the player rules, an
explicit target tier/role/budget, and a deliberately distinct doctrine brief.
The historical `docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md` and
`docs/FRONTLINE-LABS-COHORT-BASELINE.md` remain frozen inputs only when
reproducing the original four-doctrine sprint. Authors may inspect only the
player-facing material permitted by the selected packet. They create projects with
`nilbots new <Name> --profile generic-actor`, receive one implementation pass,
and do not see another entrant, standings, or replay evidence before freeze.

Every author writes `DX.md` after source freeze and before tournament
disclosure. It records player-facing friction and mechanical repairs but
cannot trigger strategy editing. Synthesize all four reports into
`DX-SYNTHESIS.md`, grouped by severity, before opening tournament outcomes.

Use `--runtime in-process` for authoring smoke matches. It avoids the
controlled-compiler round trip but is diagnostic only. Compile each frozen
revision to WASM once, preserve its artifact, and run the pre-registered
tournament with those artifacts. Rebuild only after a source or compiled
SDK/Guest input changes; a rules-only experiment does not require bot
recompilation.

Archive **every** entrant and revision under
`arena-bots/frontline-labs/<cohort-id>/<entrant-id>/`, never just the leader
and never under `champions/`. Preserve source, project metadata,
`botarena.json`, README, `DX.md`, every repair revision, canonical `bot.wasm`,
and its SHA-256. Fill the cohort manifest described by
`arena-bots/frontline-labs/README.md`; freeze all hashes before the first
match.

Treat source plus WASM as one immutable population revision. A mechanically
qualified, entertaining revision may later be explicitly promoted as a
system-owned playlist opponent. Do not publish Lab-only ablations or
adversarial sentinels, and do not edit an archived revision during promotion.

```bash
python3 scripts/labs-cohort-drive.py \
  --manifest arena-bots/frontline-labs/<cohort-id>/cohort.json \
  --output arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm
```

The current cohort-sprint default is four bots × six unordered pairs × seed
`104729` × both participant assignments = 12 matches. The historical first
baseline used `104729,130363,155921` for 36 matches; it established that the
current deterministic bots produce identical behavior across those seeds.
Add pre-registered seeds only when a ruleset or bot consumes randomness, or a
specific causal arm requires them. The driver uses
`nilbots experiment frontline-labs` in WASM mode, requires
`nilbots verify` for every complete replay v3, retains every entrant, replay,
log, and result, and computes W/D/L/points without Elo. Use `--resume` after
interruption; it adds an immutable attempt instead of overwriting evidence.

Before reading `results.json`, freeze the review sample and notes:

```bash
python3 scripts/replay-review-sample.py \
  arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/matches \
  --count 12 --seed 20260724 --blind-identities \
  --copy-selected arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/blind-review \
  --output arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/review-sample.json
python3 scripts/labs-replay-eval.py \
  --group baseline=arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/matches \
  --json arena-bots/frontline-labs/<cohort-id>/evidence/baseline-wasm/dynamics.json
```

Report W/D/L/points, participant-assignment bias, terminal reasons, dynamics,
outcome-blind notes, and DX synthesis. Do not crown the leader. Counterplay
adaptation is a new equal-budget, pre-registered run on fresh seeds, not the
default continuation of the sprint.

## 1. Boot the environment (legacy Duel)

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
confirms the ruleset) and tell agents to set `"rules": "<arm>"` in their
project's botarena.json — every play/set they run then defaults to the
experiment arm (an explicit `--rules` still wins; gen-3 lost practice games
to silently dropped flags).

Ranked play matchmakes opponents by rating for players (DECISIONS #95), but a
tournament needs scripted pairings, so `opponentBotId` still works on a local
`all` server — the role the arena runs on. The same flag governs the RULESET:
only the ladder a server is actually running accepts new sets (DECISIONS #97), so
`{"rules":"cone"}` is refused unless the flag is on or `BOTARENA_RULES=cone` makes
that arm the live ladder. Setting the env knob is the cleaner way to run a whole
tournament on an arm; the flag is for mixed runs. Point the harness at a non-local
server and you need `BOTARENA_ALLOW_PINNED_RANKED=true` there or every set comes
back 400.

Since DECISIONS #54 a ranked request can also
pin rules per set (`{"rules":"<arm>"}` on POST /api/matches/ranked) and
each ruleset rates on its own ladder — experiment tournaments no longer
pollute official elo either way; the env knob just keeps a whole run on
one arm without trusting every request. Player-facing docs only describe
SHIPPED rules, so give every author the same experiment spec for unshipped
arms. Territorial control and programmed skill-shots shipped in official 0.5
(DECISIONS #75); use the site/template plus
`docs/PLAYER-GUIDE.md` for those mechanics. For historical or other
unshipped arms, append only the matching paragraphs below.

For substantial changes, follow `docs/EVALUATION-METHODOLOGY.md`: give every
native-cohort author the same iteration budget, freeze final artifact hashes,
and judge the candidate primarily on native-vs-native play. Historical bots
still catch compatibility failures and obvious exploits, but their candidate
record is not a ship veto. Compare the previous native cohort on its previous
rules with the new native cohort on the candidate as a generational product
comparison; do not call that comparison a causal A/B.

For the hardened 0.5 slate (gen-7; arms `cone` / `bolts` / `conebolts` /
`conebolts1`, plus the `0.5-control` baseline, RULES-0.5-DESIGN §E —
include only the paragraphs matching the arm):

> EXPERIMENTAL RULES for this tournament (on top of documented v0.4):
> DIRECTIONAL VISION. You see only a 90° cone toward your facing — the
> quadrant where |lateral| ≤ forward — plus the 8 adjacent tiles; turning
> is also looking, and your back is genuinely blind (so is theirs: unseen
> approaches land unanswered shots). LOUD events (shots, damage,
> destruction) beyond your sight arrive within Chebyshev 8 as
> `context.HeardSounds`: event kind, an 8-way bearing, a near/medium/far
> band — NEVER exact positions or slots. Sound is a cue to investigate,
> reorient, or be decoyed; you cannot aim from it. Events you SEE stay
> full-detail in `context.VisibleEvents`, never duplicated as sounds;
> quiet events (moves, turns) only exist when seen. A spinning scan
> sweeps the full circle in 4 ticks but points your gun wherever you are
> looking — eyes and muzzle are one resource.

> EXPERIMENTAL RULES for this tournament (on top of documented v0.4):
> PROJECTILES. Shoot launches a bolt onto the adjacent tile in your
> facing; it advances 1 tile every 2 ticks (1 per tick under the
> conebolts1 arm), up to range 8, and despawns on walls. A bolt's tile
> is LETHAL — standing on it or stepping into it costs 1 health, checked
> both before AND after the bolt advances (you cannot slip onto the tile
> it is just leaving) — but a bolt never hits its owner, and a
> point-blank shot still hits instantly. Dodge by arithmetic:
> `context.VisibleProjectiles` gives each bolt's `TicksUntilAdvance`
> (1 = it moves THIS tick, right after movement) and `RemainingTiles`.
> Missing has value: bolts deny lanes for seconds at a time. Suppress
> the zone tile a camper stands on and it must vacate or bleed; note a
> 2x2-zone camper can surf BEHIND a one-sided volley — the executable
> eviction is bolts + your body on the forced refuge tile.

For energy re-tests:

> EXPERIMENTAL RULES for this tournament (on top of the documented v0.2): you
> have an energy meter, visible as `context.Energy` (max 6). Each shot costs 2
> energy; +1 energy regenerates every 3 ticks (cap 6). A Shoot without enough
> energy becomes Wait with an OnCooldown result. Enemy energy is not
> observable — count their shots. Sustained fire is therefore ~1 shot per 6
> ticks; bursts of 3 are available from full. Manage it or run dry mid-fight.

For the revision-v4 active-control arms (`cone-active`,
`cone-active-bolt1`, `cone-active-bolt2`; RULES-0.5-DESIGN §J), append:

> EXPERIMENTAL ACTIVE CONTROL. Occupying the zone does not score by itself.
> Only a successful `Wait` while alive on a zone tile exerts control. Move,
> turn, scan, shoot, blocked actions, and faults exert none. The objective is
> a signed shared pressure meter (`context.ControlPressure`, positive for
> slot 0; limit in `ControlPressureLimit`): one sole active holder gains 1,
> two holders freeze it, and no holder decays it one point toward zero every
> two ticks. ±100 dominates. At MaxTicks, pressure sign → health → damage.
> Collections are nullable across comparison arms: iterate
> `context.HeardSounds ?? []` and `context.VisibleProjectiles ?? []`.
> HeardSound exposes Kind/Bearing/Distance. VisibleProjectile exposes
> Position, Direction, OwnerSlot, TilesPerAdvance, TicksUntilAdvance, and
> RemainingTiles.

> EXPERIMENTAL FAST BOLTS (bolt arms only). A new shot still appears on the
> adjacent tile and does not travel farther that firing tick. It advances on
> every following tick: one ordered tile in bolt1, two ordered tiles in
> bolt2. Every intermediate tile checks walls, bots, and final range, so
> speed-two never tunnels. `TicksUntilAdvance == 1` means movement happens
> this tick after bot movement. Missing can deny a holder's Wait or force a
> defensive action that earns no pressure.

For `cone-active-bolt2-arcs` (v7), append the active-control and fast-bolt
briefs above plus:

> EXPERIMENTAL PROGRAMMED SKILL SHOTS. `context.ShotPrograms` is the nullable
> capability and exact numeric envelope. When non-null, Shoot may carry a
> private immutable `ShotProgram`: initial aim −1/0/+1 45° octants, bend
> direction −1/+1, first bend after 1–4 tiles, later bends every 1–3 tiles,
> and 1–3 bends. Use `limits.IsValid(program)` and
> `ShotPaths.Preview(position, facing, program, limits.MaxPathTiles,
> rememberedWallPredicate)` to enumerate. Return `Actions.Shoot(program)`;
> ordinary `Actions.Shoot()` remains straight. A shot enters one tile on its
> firing tick and two ordered tiles on each later tick. Diagonal travel is
> strict-corner blocked.
>
> The future path is NOT in opponent observations and cannot change after
> firing—this is prediction, not homing or randomness. A visible bolt's exact
> currently manifested eight-way `Heading` is public, but an already committed
> future bend remains private. Remember your own program. Dodge against the
> revealed heading and possible remaining programs rather than treating
> `Direction` as a guaranteed future lane. A miss that makes a holder Move or
> Turn denies its control tick.

For the historical `cone-occupancy-bolt2-arcs` v8 alias, the mechanics are now
official 0.5 and documented in `docs/PLAYER-GUIDE.md`. The canonical
territorial summary is:

> EXPERIMENTAL TERRITORIAL CONTROL. Any action may score. After movement,
> projectile collision, damage, and faults, exactly one active bot occupying
> any zone tile gains signed control pressure: Wait, move within the zone,
> turn, scan, and shoot all count. If both active bots occupy any zone tiles,
> the zone is physically contested and existing pressure decays one point
> toward zero every two ticks; an empty zone decays the same way. Dead or
> disqualified bots do not contest. Evict the opponent completely—body
> position, straight shots, and private programmed curves are all tools—then
> keep fighting while the sole occupant scores. A lead cannot remain banked
> through an unresolved contest.

After a native holdout, apply every pre-registered gate literally. If one bot
alone exceeds the strategy-share ceiling while the candidate passes safety,
dynamics, objective, and blind-viewer gates, keep both the rules and leader
frozen. A follow-up arena run may give the losing doctrines one equal,
bounded, docs/SDK/CLI-only counterplay iteration on fresh seeds. Do not waive
the ceiling, tune rules, or expose the leading bot's source unless the product
owner explicitly changes the acceptance policy; in that case preserve the
failed result and record a separate override per the balance-harness skill.

## 1.9 Pre-flight gate — never commission authors against an unverified mechanism

Agent authoring time is the expensive resource; a mechanism or DX defect
discovered by a running author wastes the whole round. Before launching
any authoring or revision wave that touches new rules, verbs, or arms,
every item below must pass — this gate is what made the wave-1 revision
round go six-for-six:

- **Contract truth**: each new action/skill appears in
  `--print-candidate-contract` output and in real legality masks; a
  scripted probe match exercises the full lifecycle (attempt → windup →
  completion/cancellation → events) and the replay verifies.
- **Template safety**: run the scaffold starter against itself on every
  new arm and confirm it *acts* (moves, fires, uses no-trap helpers).
  Precedent: the facing-locked starter freeze — three isolated agents
  independently burned hours rediscovering the same
  mask-as-search-space trap that one pre-flight self-match would have
  caught.
- **Doc truth**: rule card, class addendum, and author packet agree
  with the engine (DocDrift green is necessary, not sufficient — read
  the prose against the contract).
- **Fresh tooling**: republish the sandbox CLI authors will use; clear
  any DX-ledger items that would block or mislead authors.

`python3 scripts/preflight-authoring-gate.py --arm "" --arm "--pendulum
ratchet" …` mechanizes every item above except the prose read — per-arm
contract identity, a starter self-match that must ACT, a foreign-doctrine
smoke, a rebuild of every frozen cohort artifact, doc-vs-CLI token
agreement, and published-CLI freshness — with evidence paths and a
nonzero exit on any FAIL.

If any item fails, fix it first. Commissioning "to see what the agents
hit" is strictly worse than one probe match — agents report friction at
the END, in their DX notes, after the budget is spent.

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

Trial runs: append a time-box to the brief —

> TIME-BOX (trial run): this is a pipeline shakedown, not a title bid. Once
> your bot beats hunter on a seed batch, play ONE set vs each champion,
> submit, and report — even if you lost every champion game. Do not keep
> tuning; if the trial is promoted you'll get the replays and iterate then.

Registration (cookie auth is simplest for agents): each agent registers a user via
`curl -c jar -d '{"displayName":...,"email":...,"password":...}' /api/accounts/register`,
creates the bot, and submits sources via `POST /api/bots/{id}/versions`
(files JSON from its .cs sources; poll until Built; record artifact-hash parity vs
`-- build .` local hash).

## 2.5 Babysit the run

Agents have died silently before (environment restarts killed two
mid-tournament in gen-2); completion notifications can't distinguish "still
thinking" from "dead". After launching, schedule a self check-in every
~20-30 min (send_later if available) until every agent has reported. Each
firing, check PASSIVELY — don't message a healthy agent:

```bash
find sandbox/agents -newermt '-15 minutes' -type f | head   # fresh mtimes = alive
curl -s localhost:8080/api/bots       # registered/submitted yet?
```

Fresh workdir mtimes → healthy, re-arm silently. Stale >15 min with no
completion notification → the agent likely died with the environment:
verify postgres + the app are up (restart per §1 if not), then SendMessage
the agent — it resumes from its transcript. Stop the check-ins once all
reports are in.

## 2.6 Friction watch — kill a doomed round early

At each §2.5 check-in, also grep the agents' fresh partial output and
draft DX notes for **critical-friction** signals — defects in the game,
template, docs, or tooling that no author can overcome:

```bash
grep -rn -i -E "template (bug|trap)|never (legal|available)|contradicts|\
unsatisfiable|exited before|freezes|schema|cannot qualify" \
  sandbox/*-scratch-*/ arena-bots/**/DX.md 2>/dev/null | tail
```

Critical friction: the starter misbehaves under the arm's rules; an
action the brief describes is never legal; the contract contradicts the
docs; a qualification clause is unsatisfiable; frozen artifacts crash on
the new contract. Ordinary struggle — losing matches, doctrine churn,
slow progress — is NOT a kill signal; leave healthy agents alone.

On a critical signal, do not let the rest of the round keep paying for
it: **kill the affected agents, fix the root cause once, re-verify per
§1.9, then relaunch a fresh isolated round.** Never message the fix into
running isolated authors — mid-flight information contaminates the brief
and creates unequal authoring conditions. The fairness rule stays what
it has always been: same brief, same opportunity, every author; a
kill-fix-relaunch round satisfies it, a selectively-patched round does
not. Archive the killed round's partial work (it is forensic evidence,
not waste).

## 3. Tournament

Under shipped rules, the bracket is the agents PLUS every reigning champion.
Under a substantial rules experiment, first run the complete native-cohort
round-robin; add every reigning champion as a separately reported sentinel.
Champions are seeded as system-owned server bots automatically at app startup
(`ChampionSeeder` reads `champions/*/champion.json` + `bot.wasm`; find their
botIds via `GET /api/bots` — slug = the champions/ directory name).

Round-robin: `python3 scripts/tournament-drive.py` fires every agent-vs-agent
and agent-vs-champion ranked set (champions discovered by slug on the server;
each agent's state.json supplies its botId + cookie jar), waits for all sets,
and prints scores + the leaderboard. Rerun it after each improvement
iteration — elo accumulates. Under shipped rules, champion sets are the title
baseline; do not substitute hunter sets. Under a substantial experiment,
native-vs-native sets are primary and champion sets are sentinel evidence.

Single-challenger experiment trials: also run a MIRROR set — the challenger vs
its own submitted artifact (`botarena set --bot <srcDir> --opponent
var/artifacts/<serverHash>.wasm --rules <exp>`) — as the aware-vs-aware data
point a one-agent trial otherwise lacks. It is not enough for a substantial
rules ship verdict.

Preserve all final replays by cohort/rules/block. Run
`scripts/replay-dynamics-eval.py` on the native bracket, and freeze at least 12
outcome-blind viewer samples with `scripts/replay-review-sample.py` before
opening the outcome table. Review at normal presentation speed using the
rubric in `docs/EVALUATION-METHODOLOGY.md`; highlights are a separate,
explicitly labeled gallery.

## 3.5 Improvement iterations (per run length)

Trial runs skip this section entirely — report after the first round-robin.

After the first round-robin, send each agent its lost matches' replays
(`GET /api/matches/{id}/replay` — events + its own per-tick debug/visible data):
analyze why it lost, improve the bot, resubmit, re-run the round-robin.
Standard runs do 1 iteration, full runs 2-4 (stop early only if the
leaderboard order is unchanged twice running). Elo accumulates across
rounds; the drama is the point.

## 3.6 Crown only a legacy Duel dethroner — and KEEP it

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
  with where the reigning champion(s) placed for an official tournament. For a
  substantial experiment, lead with the native-cohort table and list
  historical sentinels separately.
- Per-bot: strategy summary, native-cohort record, historical-sentinel record
  where applicable, and artifact parity.
- Dynamics scorecard: combat incidence/tempo, action variety, stagnant/repeated
  play, objective evictions, duration guardrails, and zero-fault evidence.
- Outcome-blind replay-study manifest and notes, followed by separately labeled
  highlights.
- **DX findings ranked by severity**: doc gaps, bad error messages, CLI friction,
  submission/build failures — each with the exact reproduction. File none silently.
- Fun: name a champion; quote the best debug lines from replays
  (`GET /api/matches/{id}/replay` → `ticks[].bots[].debug`).

Clean up: kill the app by PID (never `pkill -f` — see CLAUDE.md), leave DB data
(it seeds the next run's opponents).
