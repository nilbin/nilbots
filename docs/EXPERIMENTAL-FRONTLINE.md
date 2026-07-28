# Experimental Frontline contract

Status: **playable local alpha plus an off-by-default hosted generic Labs
successor; neither is a shipped or ranked ruleset**, 2026-07-28. Official
rules 0.5, replay v1, runtime protocol/configuration 0.1, and the current
ladder are unchanged. The frozen alpha's engine-independent actor SDK/Guest,
protocol/configuration 1.0, and canonical per-life WASM runtime are selectable
through `nilbots experiment frontline`. Historical `play` and ranked play do
not admit Frontline.

This document is the concise player and bot-author contract for the frozen
Frontline engine arm. The complete machine-readable truth is the
`PublicMatchContractManifest` embedded in replay v2. Numeric defaults below
are experiment inputs, not a balance or ship verdict.

## Hosted Labs successor

The App also contains a separate generation-3 contract named
`frontline-labs-1`. It does not reinterpret `frontline-alpha-1`: it uses
resolved match contract 2, rules schema 3, map format 3, exact profile
`generic-actor-match-2`, and replay 3.

The hosted boundary is deliberately narrow:

- `BOTARENA_FRONTLINE_LABS_ENABLED` defaults false and controls catalog
  discovery, new admission, and activation of newly compiled generic-only
  artifacts. While disabled, a new artifact must retain legacy Duel support.
  Disabling it does not deactivate an existing artifact or invalidate an
  already queued, identity-pinned match.
- The immutable playlist also pins generic execution policy and semantic
  engine `1.0.0`. A versioned hosted-definition registry validates that
  identity before a capability-scoped generic job lane executes it. Older
  generic workers leave unknown playlist versions pending; the Duel lane
  fails closed.
- Immutable playlist `frontline-labs`, version 1, admits exactly two distinct
  eligible submitted bots to one head-to-head match. The first bot is owned by
  the caller; both active versions must attest the exact generic profile.
- The match is setless and unranked. It creates no season, ladder, rating,
  series entrant, series settlement, generic leaderboard, or broad
  matchmaking queue.
- Participant team IDs and canonical match-team standings/signed score
  channels are persisted. Replay 3 is returned as a validated broadcast prefix
  with result/hash withheld, then as the complete document after reveal.
- The bot-detail Labs panel opens the existing direct match page and
  version-normalized viewer. Labs matches remain outside legacy Duel feeds,
  bot history/statistics, achievements, and result notifications.
- Pilot compute limits default to 10 Labs starts per account per 24 hours,
  one active match per account, four active matches globally, and two
  creation requests per minute per account plus network. The matching
  `BOTARENA_FRONTLINE_LABS_ACCOUNT_DAILY`, `_ACCOUNT_ACTIVE`, and
  `_GLOBAL_ACTIVE` values are deployment configuration, not game rules.

This first playlist carries Fabricate, Anchor, and Split through the generic
typed catalogs. Split is available only to an eligible untransformed Prime and
retires it into two fresh `replica-mobile` lives with divided health. Those
replicas remain mobile and cannot Anchor. Only a `child-mobile` life created
through Fabricate has the `transform` action that Anchors into a turret.
Broader FFA, 2v2, Deathmatch, ranked, and multi-match-series consumers remain
future architecture work.

Availability is only an execution and integration checkpoint. The Labs map
and numeric values remain `experimental-unvalidated`; they have not passed the
independent doctrine, causal-arm, dynamics, or outcome-blind entertainment
gates. CLI/package 0.9.3 must be published and tagged from the exact final
compatibility revision before this flag may be enabled.

### Author and run the hosted Labs contract locally

The complete standalone player-facing mechanics card is
[`FRONTLINE-LABS-RULES.md`](FRONTLINE-LABS-RULES.md). Use that card for
`IGenericActorBot` authorship; the later **Frozen Frontline-alpha game**
sections describe the older `IActorBot` contract and are not an implicit Labs
specification.

Create the generic actor profile explicitly:

```bash
nilbots new MyLabsBot --profile generic-actor
nilbots new TrainingPartner --profile generic-actor
```

The profile implements `IGenericActorBot` and includes a small
contract-driven objective navigator. Its action helper resolves numeric codes
from each tick's legality catalog; it does not copy the current codes into bot
logic. Both the immutable MatchStart contract and the dynamic legality masks
are normal bot inputs, so the same API can expose changed team/unit counts,
maps, forms, actions, and modes in later playlist versions.

Run two generic projects through the exact hosted v1 definition without App
authentication, queues, or pilot quotas:

```bash
nilbots experiment frontline-labs \
  --bot MyLabsBot \
  --opponent TrainingPartner \
  --runtime in-process \
  --seeds 104729,130363,155921

# Final sandbox/parity run:
nilbots build MyLabsBot
nilbots build TrainingPartner
nilbots experiment frontline-labs \
  --bot MyLabsBot/out/bot.wasm \
  --opponent TrainingPartner/out/bot.wasm \
  --seed 104729
nilbots verify out/frontline-labs/<match>/replay.json
```

The command accepts only `IGenericActorBot` project directories or
generic-actor-profile WASM artifacts. Both entrants are required because
there are no generic built-in opponents. `--swap` reverses participant/team
assignment; `--seeds` preserves one replay under each `s<seed>/` directory.
The output is the same canonical replay-v3 envelope and generic WASM runtime
used by hosted Labs, but the match remains local and unranked. In-process
execution is diagnostic only; cohort evidence and parity claims use WASM.

`--capture-threshold <positive-n>` is the registered local numeric-arm
override. It creates a separate ruleset identity such as
`frontline-labs-1-experiment-capture-12`, changes the rules and match
fingerprints, and leaves immutable hosted `frontline-labs-1` untouched.

`--capture-gain-phase <start-tick>:<gain>` registers a phased pacing arm. For
example, `300:2` preserves the v1 gain through tick 299, then applies gain 2
from tick 300. Its complete ordered `gainSchedule` is contract data, not
engine folklore: bots can call
`frontlineMode.Capture.GainPhaseAtTick(context.Tick)`, and replay v3 embeds the
same schedule for deterministic reconstruction and ML feature extraction.

At the action boundary, select from the contract-delivered catalog rather
than hard-coding the action code:

```csharp
public GenericActorDecision Tick(GenericActorContext context)
{
    GenericActorActionLegality wait = context.Action("wait")
        ?? throw new InvalidOperationException("No wait action.");
    return GenericActorDecision.WithoutArguments(
        wait.ActionId,
        wait.ActionCode);
}
```

The generated project demonstrates typed direction and projectile-heading
arguments as well as reading the active Frontline objective from
`context.Mode` and `StartLife.Contract.ModeMapBinding`.

## Run the frozen Frontline-alpha experiment

```bash
nilbots experiment frontline \
  --bot frontline-rusher \
  --opponent frontline-bastion \
  --seed 42
```

The default all-WASM mode creates an independent isolated runtime for every
body life and writes `replay.json` plus a self-contained Canvas2D
`viewer.html`. Use `--runtime in-process` for the fast diagnostic loop, then
confirm behavior in WASM. `--seeds 7,42,1337` preserves a replay per seed;
`--swap` reverses team assignment. `--bot .` accepts a project whose entry
type implements `IActorBot`, and a prebuilt actor-protocol `.wasm` is also
valid. Run `nilbots help experiment` for the complete options.

To start a local policy without changing the shipped template contract, run
`nilbots new MyFrontliner`, then replace the generated class with an
`IActorBot`:

```csharp
using BotArena.Sdk;

public sealed class MyFrontliner : IActorBot
{
    public ActorDecision Tick(ActorContext context)
    {
        if (context.Action(ActorActionIds.Fabricate) is
            { Available: true, AllowedUnitTargets: { Length: > 0 } targets })
            return Actions.Fabricate(targets[0]);

        if (context.Enemies.Length > 0 &&
            context.Action(ActorActionIds.Shoot) is { Available: true })
            return Actions.Shoot();

        return context.Action(ActorActionIds.MoveForward) is { Available: true }
            ? Actions.MoveForward()
            : Actions.TurnRight();
    }
}
```

Keep the generated `botarena.json` entry type aligned with the class name,
then iterate with `nilbots experiment frontline --bot . --runtime in-process`.
The ordinary `nilbots play` path remains the shipped duel.

The included opponents are deterministic calibration fixtures:

- `frontline-rusher` pressures the objective and never builds;
- `frontline-swarm` fabricates every child and stays mobile;
- `frontline-bastion` fabricates, Anchors children, and uses turret fire;
- `frontline-counterpunch` builds one child and holds a defensive line before
  closing on visible contact;
- `frontline-probe` exercises typed actions for protocol diagnostics.

They were created together to exercise mechanics. They are not independently
authored doctrines and cannot satisfy the product-evaluation cohort gate.

## Frozen Frontline-alpha game

Two submitted policies contest one moving objective across five ordered
positions. It starts in the centre. Sole presence by one team builds capture
progress; empty or contested control decays existing progress. Completing a
capture advances the active position toward the opponent. Advancing through
the last position breaches the base and wins immediately.

An early breach is valid. Fabrication and turrets are escalation for games
that remain close, not mandatory acts every replay must reach.

The current starting envelope is:

- two teams and one submitted artifact per team;
- one active Prime per team at match start;
- up to three stable unit slots per team;
- binary objective presence, so stacking bodies never accelerates capture;
- Prime respawn after 18 complete absent decision ticks;
- child rebuild after 30 complete absent decision ticks;
- child-slot unlocks at ticks 120 and 260;
- maximum 500 executed ticks.

At timeout, the signed territorial score is the active position's displacement
from centre times the capture threshold, plus current capture progress signed
for the claiming team's advance direction. Health and damage are recorded but
do not break a territorial tie. A base breach on the final allowed tick wins
before timeout resolution.

## Teams, units, and lives

A scoring team, submitted participant, stable unit slot, and runtime life are
different identities:

```text
teamId        scoring side
participantId submitted artifact/policy
unitId        stable team-local Prime or child slot
lifeId        one runtime incarnation of that slot
```

The same submitted artifact is instantiated independently for every active
life. Each life has separate private memory, a deterministic random seed, and
its own runtime invocation and per-tick diagnostic budget. The participant's
match-wide diagnostic cap is shared across its lives, and a host/runtime fault
fails the experimental match rather than becoming gameplay. A form change
keeps the same life and runtime memory. Destruction disposes that runtime;
Prime respawn or child (re)fabrication creates a fresh life with fresh private
memory. A child becoming Ready after its rebuild timer does not create a life.

Collections never use array position as identity. Team, participant, unit, and
life counts are explicit public inputs. Allies, enemies, projectiles, forms,
actions, and objectives are variable collections with presence and legality
masks, so neither scripted bots nor neural policies are structurally fixed to
the current three-slot default.

## Fabrication

Only the Prime may submit `fabricate`, and only while standing on its own
protected spawn pad. The action targets one own child slot whose lifecycle is
`Ready`.

Successful fabrication reserves the first free non-Prime pad tile in canonical
Y-then-X order and creates the child at the next tick start, facing the
authored home direction. Pad capacity is evaluated after movement. A full pad
therefore permits a valid attempt but resolves it as `Blocked`; an ally that
vacates a tile on the same tick can make the attempt succeed.

The authored Prime spawn is permanently reserved against own child movement.
Enemy ground units cannot enter an opposing protected pad, but the pad grants
no health or projectile immunity.

A destroyed child enters `Rebuilding`, later becomes `Ready`, and must be
fabricated explicitly again. The new life always starts in the slot's default
`child-mobile` form, even if the destroyed life was a turret.

## Anchor and turret

An active mobile child may submit:

```text
actionId: transform
actionCode: 101
formTargetId: turret
```

Anchor is illegal on every map-authored `anchorForbiddenTile`, including all
objective and protected-pad tiles. It consumes the tick and is irreversible
for that life.

A transform started on tick `T` completes after that tick's objective phase at
the end of `T + windupTicks - 1`. During the windup the life remains
`child-mobile`, keeps contributing its mobile objective weight, continues to
receive observations, and may only `Wait`. Nonlethal damage does not interrupt
the channel. Lethal damage emits `Destroyed` followed by
`FormTransitionCancelled`; a future-due transition at match end remains
pending without an invented cancellation.

Completion keeps the same actor identity, runtime, memory, position, facing,
cooldown, energy, and accumulated damage. Health becomes:

```text
min(turret.maxHealth, currentHealth + anchor.healthGain)
```

The default arm uses a 5-HP turret, `+2` Anchor health, cooldown 1, 360-degree
vision and firing, no movement or rotation, and objective weight zero. A
turret cannot capture or contest.

Turrets submit:

```text
actionId: shoot-direction
actionCode: 102
launchHeading: North | NorthEast | East | SouthEast |
               South | SouthWest | West | NorthWest
```

This launches one straight, non-programmed projectile in the absolute heading
without changing body facing. It uses the match's normal damage, energy,
cooldown, range, wall, unit-contact, and strict diagonal-corner rules.
Programmed curves remain unavailable to turrets.

## Observation and action contract

Every active life receives the complete immutable match contract plus a
canonical public observation. The current team-perception arm shares allied
state and the frozen union of what allied sensors can see, with exact
`observedBy` provenance. A runtime never receives an ally's same-tick action;
all observations are frozen before any decision is executed.

`IActorBot.StartLife` receives the immutable contract and life identity once;
`IActorBot.Tick` receives each observation and returns one typed decision.
Actor protocol 1.0 delivers that contract through the bounded `NBV2` tagged
binary codec. One submitted artifact is compiled once, while every active life
owns an isolated WASM Store/Instance and private memory. See
[`RUNTIME-PROTOCOL.md`](RUNTIME-PROTOCOL.md).

The observation includes:

- exact self identity, current form, state, and pending transition;
- every own stable slot and lifecycle timer;
- active allies and visible enemies as variable collections;
- visible tiles, projectiles, and redacted events with provenance;
- the current frontline state;
- the complete action catalog and exact per-tick legality masks.

The public manifest also carries all gameplay variables that may differ by
map, ruleset, or season: topology and counts, form statistics, respawn and
fabrication timings, projectile rules, capture/victory rules, action
parameters, Anchor policy, and map geometry. The hosted generic MatchStart
extends that same principle to typed mode, victory, format, score, and
replication definitions. A future form such as flight is a new form/action
capability, not a reinterpretation of an existing action. Older artifacts may
remain executable while becoming ineligible or uncompetitive under a newer
contract.

## Replay and ML status

Experimental replay v2 records, for every exact actor and tick:

- the immutable public match contract and fingerprints;
- tick-start state and lifecycle events;
- the same canonical observation delivered to the runtime;
- runtime reply, accepted decision, and action resolution;
- authoritative ordered events, projectile traversal, and post-state;
- terminal team and stable-unit results.

Default slot form and active-life form are separate replay facts. Pending
transitions and explicit start/change/cancel events make Anchor sequences
trainable without re-simulating historical engine logic. Reward shaping is
not embedded in the canonical replay; raw territory, damage, lifecycle, and
terminal facts are.

The architecture is neural-policy friendly, but it does not promise zero-shot
skill on unseen counts or actions. Models should encode variable entity
collections with masks, consume the public rules vector, maintain recurrent
state per life, and apply the supplied legal-action masks. Four- or five-body
maps remain structurally representable: the executable topology fixture runs
five concurrent lives per team, including fabrication, observation, replay,
transformation, and terminal unit rows. Competitive behavior at those counts
still requires suitable training data.

The local alpha CLI can emit and display replay v2. Hosted Labs emits the
separate generic replay 3 through the direct match viewer. Descriptive alpha
evaluation is:

```bash
python3 scripts/frontline-replay-eval.py \
  --group calibration=/tmp/frontline/block-1 \
  --group calibration=/tmp/frontline/block-2 \
  --json /tmp/frontline/report.json
```

The report keeps duration/phase, fabrication, Anchor/turret, territorial,
combat, actorless, stagnation, and action dimensions separate; it deliberately
has no composite fun score. Dataset export, public replay corpora, model-asset
packaging, starter inference, broader App/server admission, general replay-v2
verification/summary, and ranked use remain follow-on work in
[`REPLAY-NATIVE-ML-PLAN.md`](REPLAY-NATIVE-ML-PLAN.md) and
[`FRONTLINE-IMPLEMENTATION-PLAN.md`](FRONTLINE-IMPLEMENTATION-PLAN.md).

## Evidence status

Engine, observation, replay, actor SDK/protocol/WASM, local CLI, evaluator,
hosted Labs execution/persistence, viewer, and mobile-bridge tests establish
determinism, valid mechanics, and integration consistency. They do not
establish fun, duration, or balance.
Small calibration runs are diagnosis only; the strong-turret defaults remain
starting arms. Canvas2D remains the viewer default; the optional lazy WebGL
2.5D renderer shares normalized replay state, is absent from the
self-contained CLI artifact, and still needs manual GPU/mobile QA.

Before any product verdict, Frontline still requires fixed all-WASM candidate
artifacts, at least four independently authored Frontline-native doctrines
with equal iteration budgets, causal arm comparisons, dynamics analysis, and
at least twelve outcome-blind replay reviews under
[`EVALUATION-METHODOLOGY.md`](EVALUATION-METHODOLOGY.md).
