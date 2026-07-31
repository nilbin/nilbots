# THE MIND: participant-scoped controller architecture

Status: **build-ready design memo**, commissioned by DECISIONS #190. It does not
change any shipped contract by itself; it specifies the contract generation,
API, runtime, replay, qualification, and migration that the build ruling will
authorize. Every value here is a design candidate until a phase lands it.

The ruling this memo executes (DECISIONS #190): *"I want the most drastical one
— the 'one mind controls all bots'… I think this will let the player focus more
on the real fun complexity than ergonomics."* Plus the owner's rider that fixes
the boundary: **the mind is the PARTICIPANT, not the team.** Future 2v2/FFA
formats are teams of allied minds.

Eight later owner additions are folded in as first-class sections rather than
appendices: mixed-class compositions (§9), chassis-at-activation headroom
(§10), inter-mind intents (§11), role tags (§12), draft-phase headroom (§13),
win conditions (§14), map scale (§15), and ratings/compositions (§16). §17 is
the consolidated build plan.

---

## 0. The one-page version

**Name.** The product word is **mind**. The profile is
`generic-mind-match-1`. The player implements `IGenericMindBot`; the SDK type
is `MindContext`; the reply is a `MindDecisions` map. Argument in §2.6.

**Profile.** A new all-or-nothing contract profile beside
`generic-actor-match-2`, not a widening of it. Framing protocol stays 1.0;
runtime *configuration* mints 2.0 (budgets change); MatchStart, observation,
and decision schemas mint 1 in a new namespace; the resolved match contract
schema stays 2 (**the game is unchanged — only who is driving it changes**).

**API.** One call per tick, unconditionally, from tick 0 to the terminal tick:

```csharp
public sealed class MyMind : IGenericMindBot
{
    private readonly Memory _memory = new();          // lives the whole match

    public void StartMatch(MindStart start) { … }

    public void Think(MindContext mind)               // ONCE per tick
    {
        _memory.Observe(mind);                        // team perception, ONCE
        var plan = _memory.Plan(mind);                // one plan, no agreement problem

        foreach (MindBody body in mind.Bodies)        // own bodies only
            plan.Role(body) switch
            {
                Role.Channel => Channel(mind, body),
                Role.Screen  => Screen(mind, body, plan.ChannelerFor(body)),
                Role.Courier => Courier(mind, body),
                _            => body.Hold(),
            };
    }
}
```

**Numbers.**

| | per-life today | mind | ratio |
|---|---:|---:|---:|
| Host→guest observation bytes / tick / participant @ 9 bodies | ~127 KB (9 × 14.1 KB, measured) | ~16–20 KB | **≈ 6–8×** less |
| Fraction of the 1 MiB host frame cap | 1.3% × 9 frames | ~1.9% × 1 frame | — |
| Tick fuel / participant @ 9 bodies | 1.80 B (9 × 200 M) | 2.05 B (250 M + 200 M × bodies) | +14% |
| Tick fuel / participant @ 1 body | 200 M | 450 M | +125% |
| Startup fuel / participant / match | 5 B × every life start | 5 B × 1 | **25–40×** less |
| Peak linear memory / participant @ 9 bodies | 576 MiB (9 × 64 MiB) | 128 MiB (1 × 128 MiB) | **4.5×** less |
| Guest threads / participant @ 9 bodies | 9 | 1 | 9× less |
| Observation projection cost / team / tick | `O(N² × mapArea)` | `O(N × mapArea)` | **N×** less |
| Replay **chronology** bytes @ 9 bodies | `N × obs` | `1 × obs` | **≈ 7×** less |
| Replay **document** bytes @ 9 bodies | ~107 MB (est., 750 ticks) | ~36 MB | **≈ 3×** less |

**Persistent memory verdict: lean in, no damper.** The perception union was
already spatial common knowledge; the mind adds only the *time* dimension.
Ship one registered diagnostic (fog effectiveness) and rewrite one
qualification requirement. Full analysis and the one near-degenerate in §3.

**Migration.** `GuestHost.RunDetected<TBot>` already selects programming models
by static type analysis, so an artifact implementing only `IGenericActorBot`
gets a Guest-side `WrappedPerLifeMind` facade automatically. **The eight
measured lineages become mind-profile-playable by rebuilding their archived
sources — zero source edits.** That rebuild is the A/B null pin.

**The ergonomics tax being removed, measured.** Six of eight wave-8 lineages
ship a dedicated coordination layer — 3,788 lines whose only job is making N
independent runtimes agree without a channel — inside a 63,861-line cohort.
That is ~6% of the entire population's source spent on agreement, and it is the
fragile 6%: same-destination blocks were 82% of one lineage's predecessor's
blocked moves, and 8 of 8 authors asked the platform to publish `movedThisTick`
because the channel's central fact *"is derivable only from life-scoped memory
a newborn lacks."*

**The owner's additions, verdicts in one line each.**

| § | Verdict |
|---|---|
| 9 Mixed compositions | Chassis moves to the **slot**; the per-slot machinery already exists. Fabricate produces the **target slot's** chassis. Ship a **registered set of five** (3 monos byte-identical to today + `spearhead` + `warden`) → 15 cells, not 6,561 armies. Composition tokens live in the **topology** profile ID, never the ruleset ID (which is at 60 of 64 chars). |
| 10 Chassis at activation | Reserve a `slotChassis` tagged shape, a `ClassTarget` parameter kind, a declared default, and two inert observation fields. v1 hardcodes `fixed` and **refuses** the alternative. FOUNDRY then costs one upgrade track plus numbers. |
| 11 Inter-mind intents | Reserve field IDs for ≤8 tagged intents out and `alliedIntents` in, **one tick delayed** (which preserves the frozen-observation invariant and makes them replay-verifiable). Empty and `Rejected` in v1. This is where TeamRandom's value actually is — and why #188 measured it null intra-team. |
| 12 Role tags | Free-vocabulary kebab ID, ≤24 bytes, sticky, non-authoritative, **public on visible enemies too** (precedent: the economy telegraphs with no visibility requirement) — which makes deception a real move and the viewer legible. |
| 13 Draft phase | Costs nothing to keep open, and v1 must do exactly one thing: **publish the slot table every tick, not only at start.** |
| 14 Win conditions | **Keep breach + timeout.** The wall is a numbers problem on a doctrine-confounded read, and #189 says so itself. Late-game capture acceleration needs **zero new capability** (the phase-schedule already exists); a lower breach threshold is **a number**. Register the **elimination window** (zero live bodies for W consecutive ticks, eligible teams only) as the one genuinely new variant. **Refuse economy-victory.** The first mind cohort is the first fresh read and attributes the wall for free. |
| 15 Map scale | **Keep `-03-legion` for v1** (the null pin requires it), register **`frontline-labs-04-march` ~35×23** as an arm. Legion endgame is already **2.8× denser** than the game everything was tuned on (13.7 vs 38.8 open tiles/body); FFA-4 would be 5.6×. A bigger map is ~198 KB/tick under per-life and **~25 KB/tick under the mind** — the mind is what makes it practical. FFA is a **mode** problem, not a map problem. |
| 16 Ratings | Rating stays **participant-scoped**; composition is recorded contract metadata; **playlist versions already pin the contract profile**, so admitting the mind profile is a data row. The App needs one nullable `CompositionId` column, inheriting the shipped "permanently classed" rule. |

---

## 1. The contract profile

### 1.1 Why a new profile and not a widening

CLAUDE.md's compatibility-generation discipline and
`GAME-MODE-ARCHITECTURE.md` §2 are the template, and RUNTIME-PROTOCOL.md
§"Versioning" states the rule that decides this case: *a version moves when a
field ID is reused, a meaning changes, or a guest is asked to attest a contract
it cannot.* The mind does all three. `Self` becomes `Bodies[]`; `Allies` changes
meaning from "my team's other bodies" to "allied bodies I do not control"; the
decision changes from one action to a map. No trailing-tagged-field trick
reaches that. It is a generation, not an extension.

Equally, it must not *replace* `generic-actor-match-2`. The shipped duel
product is untouched either way, but three live assets depend on the per-life
generation staying byte-exact: the hosted `frontline-labs` v1 playlist and its
pinned fingerprints, the eight measured wave-8/legion lineages, and every frozen
cohort's evidence. RUNTIME-PROTOCOL.md already records why minting
`generic-actor-match-3` was rejected for a smaller change (*"the capability
tuple rides inside the fingerprinted match contract, so bumping it relabels
every immutable generic ruleset"*). That argument applies with full force here
and is the reason the two profiles coexist **beside** each other rather than in
sequence.

### 1.2 The exact tuple

Beside `ActorContractProfile.GenericV2` in
`src/BotArena.Sdk/ActorContractProfile.cs`:

```csharp
public static ActorContractProfile MindV1 { get; } = new(
    ProfileId:                 "generic-mind-match-1",
    RuntimeContractVersion:    1,   // fresh: participant-scoped host behaviour
    MatchStartSchemaVersion:   1,   // fresh: MindStart, not per-life MatchStart
    ObservationSchemaVersion:  1,   // fresh: union-once + Bodies[]
    DecisionSchemaVersion:     1,   // fresh: decision MAP
    MatchContractSchemaVersion: 2); // CARRIED OVER — unchanged
```

| Capability | Decision | Why |
|---|---|---|
| Framing protocol | **carry 1.0** | 12-byte NBV2 header, tagged fields, correlated request/reply, frame caps, `Fault`/`Unsupported` — all unchanged. New message types are a profile matter, not a framing matter. |
| Resolved match contract schema | **carry 2** | The rules, map, forms, actions, transitions, lifecycle, mode and economy are *identical*. The mind plays the same game. Carrying schema 2 is what keeps `frontline-labs-1`'s rules fingerprint valid on the mind profile and makes the A/B pin meaningful. |
| Map generation | **carry 3** | Untouched. |
| Rules schema | **carry 3** | Untouched, except the additive per-slot chassis of §9, which follows the #156 additive-canonical pattern (emitted only when a ruleset declares compositions). |
| Runtime **configuration** | **mint 2.0** | Fuel formula, memory ceiling, and instance topology all change (§4). Configuration 1.0 stays exactly as pinned for the per-life generation. |
| MatchStart / observation / decision schemas | **mint 1** | Structurally new objects; a new namespace so the numbers never collide with the actor line's 2s. |
| Replay format | **carry 3** | Replay 3 grows a `mindTurns` alternative to `actorTurns`, keyed by the contract profile (§5). |
| Topology schema | **carry** + additive slot chassis | §9. |

The Engine mirror goes in `BotArenaVersions.cs` beside the
`GenericActor*` block as a `GenericMind*` block, and
`GenericActorContractVersions.cs` gets a `GenericMindContractVersions.cs`
sibling. `SubmissionContractProfileProbe`
(`src/BotArena.App/Bots/SubmissionContractProfileProbe.cs:22`) gains a third
`TryProbe` arm, so `supportedContractProfiles` can carry
`generic-mind-match-1` alongside the other two with no shape change to the
probe result.

### 1.3 The participant boundary, designed for 2v2 from day one

The rider is the load-bearing constraint, and the observation shape is where it
becomes real. Today `GenericActorContext` has `Self` (one own body) and
`Allies` (every other body on the scoring team, regardless of who controls it).
That single collection conflates two relationships that
`GAME-MODE-ARCHITECTURE.md` §5 explicitly keeps distinct: *"One submitted
artifact controlling several clones is one participant with several stable unit
slots. A 2v2 team is two participants sharing one scoring team. Those are
different relationships and stay different in contracts, replays, datasets, and
ladders."*

The mind observation splits them:

```text
MindContext
  Bodies[]        own bodies — I command these       (was: Self + my share of Allies)
  Allies[]        allied bodies I do NOT command     (was: the rest of Allies)
  Enemies[]       visible enemy bodies               (unchanged)
```

In head-to-head, `Allies` is **always empty** — one participant per team. In
2v2 it carries the other mind's bodies with exactly today's `ObservedAllyState`
shape and exactly today's team-perception policy. In FFA-N it is empty again
(one participant per scoring team). Nothing about the format definitions needs
to change: `MatchFormatDefinition`'s `head-to-head` / `free-for-all` / `teams`
variants already express all three, and the mind reads its allied-participant
set from `MindStart.Topology`.

Three consequences fall out naturally, which is the test of the boundary:

1. **Team perception is unchanged and stays team-scoped.** The observable union
   is computed per scoring team exactly as today; the mind receives its team's
   union once. In 2v2, both allied minds receive the same union. Nothing about
   fog, provenance, or `observedBy` moves.
2. **A mind can only command what it owns.** The decision map is keyed by
   `(unitId, lifeId)` within the mind's own participant; a key naming a slot
   the participant does not control is `Rejected` (§2.4). There is no way to
   express "move my ally's body", which is the whole point.
3. **TeamRandom survives one level up.** Its stream is derived per *scoring
   team* (DECISIONS #185) and is delivered unchanged. Intra-mind it becomes
   pointless — a single mind does not need to agree with itself — but
   inter-mind it becomes its actual use case: two allied minds flipping a coin
   the enemy cannot predict. This retroactively explains #188's finding that
   *"TeamRandom's first doctrine verdict is null-to-negative"*: coordinated
   unpredictability had no buyer because **coordination**, not unpredictability,
   was the scarce thing. Under the mind, coordination is free and
   unpredictability is the only part left worth buying — and only across minds.

The common-knowledge toolkit therefore does not dissolve; it relocates.
TeamRandom moves from intra-team to inter-mind, and the declared-intent surface
(§11) is reserved at the same level.

---

## 2. The developer API

This is where the owner's ruling lives, so this section is written against
the actual code an author writes today.

### 2.1 What an author writes today, quoted

Every wave-8 lineage independently rebuilt the same machine. `spark-line`'s is
representative — `arena-bots/frontline-labs/classes-wave-8-2026-07-31/spark-line/Squad.cs`,
whose class doc states the problem exactly:

> The wave-6 coordination layer: how four bodies of one team stay out of each
> other's way without any shared memory.
>
> Nothing here is a channel. Every life is a fresh instance with empty private
> fields, so the only thing four bodies can agree on is a FUNCTION of the frozen
> observation they all receive… A rule that says "the other body goes first" is
> worthless unless both bodies compute the same "other".

The machine itself (`Squad.cs:110-170`), rebuilt from scratch **every tick in
every body** — `SparkLine.cs:156` constructs it fresh, `:417` re-resolves it:

```csharp
public void Resolve(ContractLens lens, GenericActorContext context, …)
{
    _members.Clear(); _claimNow.Clear(); _claimNext.Clear();
    _takenRuns.Clear(); _siblings.Clear();
    _selfIndex = -1; …

    Add(lens, context.Self.ActorId, context.Self.Position, context.Self.Facing,
        context.Self.FormId, objectiveField, isSelf: true);
    foreach (GenericActorContext.ObservedAllyState ally in context.Allies)
        Add(lens, ally.ActorId, ally.Position, ally.Facing,
            PendingForm(ally), objectiveField, isSelf: false);

    // Precedence order, ascending. Every later step reads this order and
    // nothing reads iteration order, so the answer does not depend on which
    // body is asking.
    _members.Sort(static (a, b) => Key(a).CompareTo(Key(b)));
    …
}

private static long RightOfWay(int distance, bool canFabricate, int unitId, int lifeId) =>
      ((long)Math.Clamp(distance, 0, 4095) << 32)
    + ((long)(canFabricate ? 0 : 1) << 31)
    + ((long)(unitId & 0xFFFF) << 15)
    + (lifeId & 0x7FFF);
```

**The measured tax.** Six of eight wave-8 lineages ship a dedicated
coordination file — `spark-line/Squad.cs` 576, `arc-light/ArcTraffic.cs` 568,
`vector-edge/Traffic.cs` 562, `march-wall/Column.cs` 662,
`still-water/Convoy.cs` 727, `ledger-fly/Convoy.cs` 693 — **3,788 lines** that
exist for one reason: nine independent runtimes must reach the same conclusion
without a channel. `gate-stone` and `iron-root` have theirs embedded in larger
files. Against a cohort total of 63,861 authored lines (mean 7,983 per
lineage), that is roughly **6% of the entire population's source, spent on an
agreement problem the mind deletes outright**. Not on tactics. On agreeing.

And it is not merely volume; it is the *fragile* volume. `Squad.cs:239-244`
records that **82% of its predecessor's blocked moves** were two siblings
choosing the same destination tile. `arc-light`'s DX (`DX.md:422-428`) records
the deeper failure: the channel's central fact — did an allied body change tile
— *"is derivable only from life-scoped memory a newborn lacks"*, which 8 of 8
authors independently asked the platform to publish. TeamRandom (#185) exists
because the scaffold's shared direction ordering was a *"wave-6 trap that
invalidated an author's sweeps"*. Every one of those is an artifact of
agreement-without-a-channel.

### 2.2 The tick entry

```csharp
public interface IGenericMindBot
{
    /// Called exactly once, before tick 0. The mind instance then lives
    /// the entire match.
    void StartMatch(MindStart start) { }

    /// Called exactly once per tick, unconditionally, for every tick of the
    /// match — including ticks on which the mind owns no live body.
    void Think(MindContext mind);

    /// Called exactly once, after the terminal tick.
    void EndMatch(MindEnd end) { }
}
```

`Think` returns `void`. Commands are written onto the body handles, not
returned — see §2.4 for why that is the right call and not merely a taste.

### 2.3 `MindContext` — the full shape

Grouped by the three lifetimes that matter, and annotated with what it replaces.

```csharp
public sealed record MindContext
{
    // ── join / header ─────────────────────────────────────────────────────
    int    SchemaVersion { get; }
    int    Tick { get; }                        // zero-based, about to execute
    string MatchContractFingerprint { get; }

    // ── MY BODIES — the new centre of gravity ─────────────────────────────
    ImmutableArray<MindBody> Bodies { get; }    // own live bodies, canonical order
    ImmutableArray<MindSlot> Slots { get; }     // ALL own slots, live or not

    // ── TEAM PERCEPTION — delivered ONCE ──────────────────────────────────
    ImmutableArray<ObservedAllyState>   Allies   { get; }  // allied MINDS' bodies; empty in H2H
    ImmutableArray<ObservedEnemyState>  Enemies  { get; }
    ImmutableArray<ObservedTile>        VisibleTiles { get; }
    ImmutableArray<ObservedProjectile>? VisibleProjectiles { get; }
    ImmutableArray<ObservedEvent>       VisibleEvents { get; }
    ImmutableArray<ObservedSound>?      HeardSounds { get; }

    // ── MODE / ECONOMY / SCORE — delivered ONCE ───────────────────────────
    ModeObservationState  Mode { get; }         // Frontline: objective, channel, scrap
    ScoreboardState       Scoreboard { get; }
    ImmutableArray<ObservedParticipantStatus> Participants { get; }

    // ── INTER-MIND (reserved, §11) ────────────────────────────────────────
    ImmutableArray<AlliedIntent> AlliedIntents { get; }   // always empty in v1

    // ── services ──────────────────────────────────────────────────────────
    IBotRandom Random     { get; }   // private to this mind, advances across ticks
    IBotRandom TeamRandom { get; }   // shared with allied minds, re-derived per tick
    IBotDebug  Debug      { get; }
}
```

`MindBody` is today's `ObservedSelfState` plus the per-body legality mask plus
the command surface:

```csharp
public sealed class MindBody
{
    // identity & state — exactly today's ObservedSelfState field set
    ActorIdentity ActorId { get; }         // (teamId, unitId, lifeId)
    int      UnitId  { get; }              // the STABLE handle: survives death
    int      Generation { get; }
    string   FormId { get; }
    string?  ClassId { get; }              // per-body chassis (§9)
    Position Position { get; }
    Direction Facing { get; }
    int      Health { get; }
    int      Cooldown { get; }
    int?     Energy { get; }
    int      CarriedScrap { get; }
    ImmutableArray<ObservedRouteCooldown> RouteCooldowns { get; }
    PendingSameLifeTransition? PendingSameLifeTransition { get; }
    GenericActorActionResolution? PreviousActionResolution { get; }

    // NEW — facts the mind is entitled to and a per-life bot was not
    Position? PreviousPosition { get; }    // null on a life's first tick
    bool      MovedLastTick { get; }       // ← 8/8 authors asked for this
    int       LifeStartedTick { get; }
    LifeOrigin Origin { get; }             // was StartLife.Origin, now per body
    string?   RoleTag { get; }             // currently published label (§12)

    // legality
    ImmutableArray<GenericActorActionLegality> ActionLegalities { get; }
    GenericActorActionLegality? Action(string actionId);

    // command surface — see §2.4
    void Command(string actionId, int actionCode, params GenericActorActionArgument[] args);
    void Hold(string? why = null);
    void SetRole(string? roleTag);
}
```

`MindSlot` is today's `ObservedUnitSlot` — `UnitId`, `UnitSlotState State`
(the existing closed union: `Active` / `AvailabilityPending(DueTick)` /
`AutomaticReturnPending(DueTick, TargetFormId)` / `Ready` / `FabricationPending`
/ `ReplicationPending` / `PermanentlyDormant`) — plus `ClassId` and
`CandidateClassIds` (§9, §10). **`Slots` is published every tick, not only at
start.** That is deliberate and costs nothing now; §13 explains why it is the
single thing v1 must do to keep a draft phase possible.

`MovedLastTick` deserves a note. It is the top-ranked platform ask of wave 8
(8/8 authors), and under the mind it is not a favour — it is *free*. The mind
holds last tick's `Bodies` in its own fields; the engine publishing it merely
saves every author from writing `arc-light`'s nine lines and its documented
footgun ("every bot must remember to update it on every early return or be
wrong in exactly the way that is hardest to see"). Publish it. Note it solves
the fact only for **own** bodies; the enemy half remains open (§7.4).

### 2.4 The decision map, and why commands are written not returned

First, a correction to a common reading of the boundary. CLAUDE.md's
*"`PrepareTick()` → exact keyed `StepActors(...)`… the decision dictionary must
contain exactly those keys"* describes the **frozen historical
`FrontlineMatchSession`**. The generic session inverted that contract:
`GenericActorMatchSession` exposes `PrepareTick()` → `Step()`, **owns the
participant runtimes, and pulls decisions itself** through
`GenericActorMatchHost.CollectTickDecisions`. The
`Step(IEnumerable<GenericActorRuntimeObservation>)` overload exists only as a
test seam and requires reference-identical objects from `PrepareTick`. So the
exactness rule now lives one layer down, in
`GenericActorRuntimeCoordinator.ValidateExactObservationBatch` — *"Observation
batch must contain every and only active actor life exactly once."*

**The mind changes none of that host structure.** `PrepareTick()` → `Step()`
stays; what changes is that `CollectTickDecisions` invokes **one runtime per
participant** instead of one per life, and fans the returned command set out
across that participant's bodies. The 16 canonical tick phases, the
`SessionOperation` re-entrancy guard, the memoized `_preparedTick`, and the
`(ParticipantId, ActorId)` invocation ordering are all preserved — which is why
§7.2's null pin is achievable at all.

The player-facing exactness rule is what changes, and it should. That
strictness was right when the host was mapping N independent runtimes onto N
keys: a missing key meant the *host* had lost track of a life.

Under the mind it would be actively hostile. A mind that forgets a body has an
ergonomics bug, and the ruling this memo serves says ergonomics bugs are what
we are removing. The contract becomes:

- **Every own live body defaults to `Wait`.** The mind overrides what it wants
  to move. Forgetting a body costs that body one tick, visibly, in the replay —
  not the match.
- **A key naming a body the participant does not own, or a body that is not
  live this tick, is `Rejected`.** This reuses the existing grammar verbatim
  (`GAME-MODE-ARCHITECTURE.md` §7: *"Unknown/malformed actions are `Faulted`,
  catalog actions outside the current form mask are `Rejected`… only `Faulted`
  increments the participant fault counter"*). Commanding a body that died this
  tick is an easy and *forgivable* mistake under persistent memory, so it must
  not be a fault.
- **Two commands for the same body, a malformed action, or a malformed argument
  is `Faulted`**, exactly as today, and increments the participant counter.

Writing commands onto `MindBody` rather than returning a dictionary is what
makes the default mechanical: the host pre-fills every live body with `Wait`
and the mind overwrites. There is no key set for the author to get right, no
`ToDictionary` to forget, and no `KeyNotFoundException` class of bug. The
`Command`/`Hold`/`SetRole` calls are buffered on the handle and harvested by
the Guest after `Think` returns; a second `Command` on the same handle throws
in-guest (a fault the author sees immediately) rather than silently winning.

Ergonomically this is the difference between

```csharp
// per-life today: reach the right conclusion in nine independent runtimes
_squad.Resolve(lens, context, _objective, _objectiveField, _coupling);
if (_squad.SelfIsChanneler) return HoldTheChannel(context);
if (_squad.SelfIsScreenFor(_squad.Channeler)) return Screen(context, _squad.Channeler);
…
```

and

```csharp
// mind: state the conclusion once
var channeler = mind.Bodies.OnPoint(objective).MostDurable();
channeler.SetRole("channeler");
channeler.Hold("stationary claim");
foreach (var screen in mind.Bodies.Nearest(channeler.Position, 2).Take(2))
{ screen.SetRole("screen"); Interpose(screen, channeler, threatAxis); }
```

### 2.5 The four worked scenarios

Each shows the *before* pattern the wave-8 cohort actually wrote, then the
mind form. These are the ergonomics pitch, concretely.

#### (a) The escorted channel — the game's central set-piece (#187/#188)

*Before.* Every body independently: build a total precedence order over self
plus allies (`Squad.Key`); decide whether *it* is the channeler; if not,
recompute who is; reconstruct whether that body moved last tick from its own
observation history (`ArcMemory.MovedLastTick`, nine lines, footgunned); decide
whether *it* is a screen and which side; hope all three agree. Roughly 120–200
lines per lineage, and any disagreement wastes a tick of stationary claim —
which #188's converged doctrine prices as *"the most expensive tick this
doctrine has ever had to price"*.

*After.*

```csharp
void Channel(MindContext mind, Objective point)
{
    // Who channels is a decision, not a derivation.
    MindBody channeler = mind.Bodies
        .Where(b => b.ObjectiveWeight > 0 && point.Contains(b.Position))
        .OrderByDescending(b => b.Health).ThenBy(b => b.UnitId)
        .FirstOrDefault() ?? Nearest(mind.Bodies, point);

    channeler.SetRole("channeler");
    if (point.Contains(channeler.Position)) channeler.Hold("claim");
    else StepToward(channeler, point);

    // Screens go where the damage comes from. The mind knows which of its own
    // bodies is stationary because it commanded them last tick.
    foreach ((MindBody screen, Direction axis) in ThreatAxes(mind, channeler).Take(2))
    {
        screen.SetRole("screen");
        Interpose(screen, channeler, axis);
    }
}
```

The `movedThisTick` problem is gone for own bodies (`body.MovedLastTick`), the
agreement problem is gone (there is one decision), and the assignment is
published to the viewer (`SetRole`).

#### (b) A courier run — the economy's unbought game (#188 finding 5)

*Before.* Wave 8 measured *"most of the cohort banks at the tile and refuses
the carry"* — the deep-carry game the memo designed is **mostly unbought**. The
reason is legible in the source: a courier is a *multi-life, multi-body*
commitment (go out at t≈44, collect, walk home, bank) and a per-life bot cannot
own a plan longer than its own life, nor guarantee the front knows one body is
away. Committing was strictly riskier than not.

*After.* The plan is a field on the mind and outlives every body in it:

```csharp
sealed class CourierPlan { public int UnitId; public Position Target; public int DueTick; }
CourierPlan? _run;                                   // persists all match

void Economy(MindContext mind)
{
    // The plan survives its executor. This is the thing per-life could not do.
    if (_run is not null && !mind.TryBody(_run.UnitId, out _))
        _run.UnitId = Reassign(mind, _run);          // hand the run to another body

    ScrapPile? pile = NextWorthwhilePile(mind);       // mode.ScrapPiles is public
    if (_run is null && pile is not null && FrontIsHeld(mind))
        _run = new CourierPlan { UnitId = FreeBody(mind).UnitId, Target = pile.Position, … };

    if (_run is not null && mind.TryBody(_run.UnitId, out MindBody courier))
    {
        courier.SetRole(courier.CarriedScrap > 0 ? "courier-home" : "courier-out");
        RouteTo(courier, courier.CarriedScrap >= CarryCap ? HomePad : _run.Target);
    }
}
```

`FrontIsHeld(mind)` is a one-line query over `mind.Bodies` — the allocation
decision that #188 found nobody was making is now a single expression over the
whole army instead of a per-body guess about what the others are doing.

#### (c) A build order — impossible per-life, trivial per-mind

*Before.* There is no build order in the wave-8 cohort, and there structurally
could not be: the Prime that fabricates dies, its successor starts with empty
memory, and "which child do I build next and why" has no carrier. The
`--roster legion` fabricator opens with **three fabrications before contact**
(#189) and had to re-derive its intent every life.

*After.*

```csharp
readonly Queue<BuildStep> _order = new([                 // authored once, at StartMatch
    new(Slot: 1, Role: "screen"),
    new(Slot: 2, Role: "screen"),
    new(Slot: 3, Role: "courier"),
]);

void Build(MindContext mind, MindBody prime)
{
    if (_order.Count == 0) return;
    BuildStep next = _order.Peek();
    var fab = prime.Action("fabricate");
    if (fab is { Available: true }
        && fab.UnitTargets().Any(t => t.UnitId == next.Slot))
    {
        prime.Command(fab.ActionId, fab.ActionCode,
            new UnitTargetArgument(new UnitTarget(prime.ActorId.TeamId, next.Slot)));
        _pendingRole[next.Slot] = next.Role;             // role assigned before it exists
        _order.Dequeue();
    }
}
```

Note the last line: a mind can decide a body's job **before the body exists**,
and the tag is applied on its first tick. That is a genuinely new strategic
object, not a nicer spelling of an old one.

#### (d) Target focus — the C2/C3 coordination grades, deleted

*Before.* Focus fire is coordination grade C2 (*"ally-only visibility enables a
useful attack; focus target changes when health/position changes"*) and C3
(*"two perpendicular shooters establish the executable crossfire pattern"*).
Per-life, it is a shared-plan recomputation with the same agreement fragility
as the escort: every shooter must independently select the same target from the
same frozen observation and hope the tie-breaks match.

*After.*

```csharp
void FocusFire(MindContext mind)
{
    ObservedEnemyState? focus = mind.Enemies
        .OrderBy(e => e.Health)                      // finish what is nearly dead
        .ThenByDescending(e => e.CarriedScrap)       // then rob the courier
        .ThenBy(e => e.ActorId.UnitId)
        .FirstOrDefault();
    if (focus is null) return;

    foreach (MindBody gun in mind.Bodies.Where(b => b.Cooldown == 0 && CanReach(b, focus)))
    {
        gun.SetRole("gun");
        gun.Command(...Solve(gun, focus));           // each solves ITS OWN geometry
    }
}
```

`OrderBy(...).First()` is the entire C2/C3 competency. §6 draws the conclusion:
**the mind dissolves the coordination axis of the qualification suite.**

### 2.6 The product word

Three candidates, one disqualified on collision:

- **controller** — *already taken*. `GAME-MODE-ARCHITECTURE.md` §1 defines the
  topology as "submitted participant/**controller**", and
  `PublicUnitSlot.ControllerParticipantId` is the field name. Reusing it for a
  new concept would make "the controller controls the controller" a sentence in
  our own docs. Rejected.
- **commander** — reads as an in-world unit in every RTS the audience knows
  (a commander has a position, HP, and can die). Ours has none of those, and
  the confusion would be exactly at the point the design is trying to make
  clear. Also 9 characters, which matters (below).
- **mind** — no in-world referent, so it cannot be mistaken for a body; pairs
  with "body", which is already the campaign's word (#189: *"initial number of
  bots higher"*, #190: *"driving every body that participant owns"*); makes
  "allied minds" read correctly for 2v2 in the rider's own words; and is **4
  characters**, which is real budget in a system where canonical IDs are capped
  at 64 and `FrontlineLabsDefinition.cs:1648` already records the full game's
  ruleset ID sitting at 60 of 64.

The owner already used it as the title of DECISIONS #190. Adopt it: `mind`,
`IGenericMindBot`, `MindContext`, `MindBody`, `MindStart`,
`generic-mind-match-1`, `nilbots new --profile generic-mind`.

### 2.7 Lifetime, start payload, and the tick invariant

**Constructed once.** `IGenericMindBot` is instantiated once per participant per
match, before tick 0, and disposed after the terminal tick. Its fields *are*
the persistent memory; there is no `Memory` API to learn.

**`MindStart` carries:**

| Field | Note |
|---|---|
| `SchemaVersion`, `RuntimeContractVersion` | as today |
| `Contract` (`GenericActorResolvedMatchContract`) | **byte-identical to today's** — same canonical JSON, same fingerprints, schema 2 |
| `ParticipantId`, `TeamId` | who I am |
| `AlliedParticipantIds` | empty in H2H; the 2v2 hook |
| `Topology` | teams, participants, slots (with per-slot chassis, §9), initial lives |
| `MindRandomSeed` | private stream, derived in the participant domain |
| `TeamRandomSeed` | the #185 team seed, unchanged, now consumed at mind scope |
| `BotName` | the framework selector, as today |

**Deliberately NOT in `MindStart`:** the slot table's *state*, and any
`LifeOrigin`. Slot state is published every tick in `MindContext.Slots`
(§13 explains why), and origins move onto `MindBody.Origin` — a body's origin
is a per-body fact under the mind, delivered on the tick that body first
appears, not a start-time fact about "me".

**The tick invariant — state it loudly, it is the biggest simplification here:**

> `Think` is called exactly once per tick, for every tick of the match, from
> tick 0 to the terminal tick, regardless of how many bodies the mind owns or
> whether any of them has a legal action.

Three sub-cases and the argument for each:

- **Total body loss (all bodies dead, respawn timers running).** The mind
  still ticks. `Bodies` is empty; `Slots` shows every pending return with its
  `DueTick`; `Mode`, `Scoreboard`, `ScrapPiles`, `ScrapTeams` and
  `Participants` all continue and all continue to *change* (the enemy is
  capturing, veins are landing, piles are expiring). A mind that went dark here
  would (i) lose the ability to plan the return — which is exactly the "real
  fun complexity" the ruling wants, especially under `--pendulum hull` where
  every arrival is a home walk (#189); (ii) accumulate silent memory staleness
  and make tick counting a footgun; (iii) be unable to decay its enemy-position
  beliefs during the very window when they decay fastest. It costs only the
  base fuel term (§4.2), which is cheap. Tick it.
- **Bodies alive, zero legal actions** (all in Wait-only windup, e.g. anchoring).
  Ticks. Costs nothing; the legality masks say wait-only and the host pre-fills
  `Wait`. The mind can still update memory and set role tags.
- **Before the first body exists** — a fabricator composition whose slots are
  all `DormantUnlockAtTick`, or a delayed first-life activation. Ticks from
  tick 0.

The value of the invariant is that it converts *"am I alive?"* from a
control-flow question into a data question (`mind.Bodies.Length`). Every
per-life bot in the cohort has branches for "I am new" and "I am about to die";
a mind has none.

---

## 3. Persistent memory, eyes open

The information game genuinely changes. Three changes, analyzed against the
mechanisms this game actually has.

### 3.1 What the mind adds is TIME, not SPACE

The single most important correction to the intuitive reading: **allied
perception is already an immediate union.** `FRONTLINE-LABS-RULES.md`: *"every
life receives current allied body state and the union of what declared allied
sensors see, including `observedBy` provenance."* Nine bodies already share one
picture *within* a tick.

So the mind does not pool information across bodies — that was already pooled.
It pools information **across ticks**. The delta is one dimension, not two, and
that reframes every consequence below.

### 3.2 Consequence 1: scouting knowledge survives body death

Today, killing the body that saw something deletes what it saw *for that body's
successor* — but not for its living siblings, who saw it too through the union.
So the deletion only bites when the team's knowledge is carried by bodies that
all die, or when a body is the last one alive.

Under the mind it never bites. **Verdict: small real change, large ergonomic
change.** The strategic value of "kill the scout to blind them" was already
weak because of the union; what dies is the *bookkeeping* penalty, which is
what authors complained about, not a mechanic anyone was playing.

### 3.3 Consequence 2: respawn amnesia disappears

This is the largest change and deserves care, because it is the one thing that
looks like a removed mechanic.

Was it a mechanic? Search the record: nothing in #158–#189 prices respawn
amnesia. #189's `--pendulum hull` deliberately re-introduced **the home walk**
(*"with no free forward placement anywhere, the fabricator's field-placed
children are the only forward body delivery in the game"*) — it priced the
*distance*, not the *ignorance*. The 18-tick prime return prices *absence*. No
arm, factor, or registered prediction in the campaign has ever cited a returning
body's empty memory as load-bearing.

What it *was*, is friction. Its two live traces in the record are both DX
complaints: `arc-light` (*"the channel's central fact is derivable only from
life-scoped memory a newborn lacks"* — 8/8 authors) and `iron-root` (*"a life
with no previous observation counts as stationary"* — a rule an author had to
derive and could get silently wrong).

**Verdict: removing it removes friction, not a mechanic.** The home walk
survives intact and becomes a *coordinated* approach instead of a blind one,
which is strictly more watchable.

One honest cost: the qualification suite's documented T2 requirement
`respawn-reorient` (*"Resumes mode-directed play after a fresh life with
isolated memory"*) becomes vacuous. It is a doc-level requirement — the
implemented suite-3 probe list does not contain it — so the fix is a rewrite,
not a code deletion (§6.2).

### 3.4 Consequence 3: enemy-position memory persists all match

This is where the only near-degenerate lives, so it gets the full treatment.

A mind can integrate, over up to 750 ticks, every channel the game publishes:

| Channel | What it leaks | Already public? |
|---|---|---|
| Vision union | exact enemy positions, `observedBy`, `carriedScrap`, `classId` | yes, per tick |
| **Hearing, radius 8** | attacks, damage, destruction, with coarse bearing + distance band | yes, per tick |
| `mode.scrapPiles` | *"a body died here within 80 ticks"* | yes, fully public |
| `mode.scrapTeams` | bank + tier levels; a purchase telegraph with **no visibility requirement** | yes, fully public |
| `captureProgress` / `claimingTeamId` | someone is on the point, and under the channel, someone *stationary* is | yes |
| `Participants[].RuntimeFaultCount` | the enemy is misbehaving | yes |

Integrated across a match, hearing is the concerning one. Radius 8 on a 23×15
map covers a large fraction of the board, and a mind that maintains a
particle-filter-ish belief map fed by 750 ticks of bearing+band reports gets a
persistent low-resolution enemy tracker for free. Fog gets *softer*.

Four reasons not to damp it in v1:

1. **It is not new information, only new integration.** Every one of those
   channels is published every tick today and a per-life bot could already
   integrate them across its own life. Under `--horizon long` a surviving
   bulwark prime already has a 400-tick window. The mind widens the window; it
   does not open a channel.
2. **The counterplay is real and already priced.** Deny hearing by not shooting
   (the stillness doctrine already does this). Deny death sites by not dying.
   Deny the purchase telegraph by not buying — and #189 made the economy
   *deliberately* loud (*"scraps should decide the game"*). Fog was never the
   only thing hiding you.
3. **It is symmetric.** Both minds get it. #187's own reasoning about the pile
   leak applies verbatim: *"it is symmetric, it is small… and the alternative is
   a race you cannot see."*
4. **The owner chose drastic**, and the campaign's method is to measure rather
   than pre-emptively tune (`EVALUATION-METHODOLOGY.md`; #174 "everything
   provisional").

**Verdict: lean in. No damper in v1.** Two cheap safeguards instead:

- **Register one diagnostic** on the first mind balance read:
  *fog effectiveness* = the fraction of enemy-body-ticks during which the
  winning mind held a position estimate within 3 tiles, computed from the
  replay (which stores the full observation, so it is computable without
  instrumenting bots). If it approaches 1.0, fog has measurably stopped
  existing and the lever is a *rules-side* knob that already exists —
  `ActorVisionProfileDefinition` range and the hearing radius, both per-form,
  both data. **No architecture change is ever needed to damp this**, which is
  precisely why it is safe to ship undamped.
- **Do not add durable *team* memory.** `GAME-MODE-ARCHITECTURE.md` §7 already
  rules that *"durable team memory would be a separately bounded, replayed
  blackboard capability rather than an accidental consequence of clones sharing
  an artifact."* The mind gives durable **participant** memory, which is the
  ruled boundary. In 2v2, allied minds still do **not** share memory — they
  share perception and, later, declared intents (§11). Keep that line.

### 3.5 One thing that gets strictly harder, and should

A kill no longer destroys information, so the *informational* component of a
kill is gone. Its other components — removing a body, its objective weight, its
gun, its `carriedScrap`, and dropping `2 + load` scrap at the death tile
(#189) — are unchanged and are where killing was always priced. Nothing in the
campaign ever attributed value to the information component. No action.

---

## 4. Runtime and protocol

### 4.1 Instance topology

RUNTIME-PROTOCOL.md today: *"One submitted artifact factory owns one Wasmtime
Engine and one compiled Module. Every active `(teamId, unitId, lifeId)` owns an
independent Store, Instance, linear memory, globals, deterministic clock/random
shims, guest thread, and bot object."*

Under the mind:

> One submitted artifact factory owns one Wasmtime Engine and one compiled
> Module. **Every submitted participant owns exactly one Store, Instance,
> linear memory, globals, deterministic shims, guest thread, and mind object,
> for the whole match.** Bodies are data inside that instance. A body's
> destruction disposes nothing; a participant's disqualification or the match's
> end disposes the Store.

`WasmGenericMindRuntimeFactory` sits beside
`WasmGenericActorRuntimeFactory.cs`; `WasmGenericMindRuntime` beside
`WasmGenericActorRuntime.cs`; `GenericMindWasmProtocol.cs` beside
`GenericActorWasmProtocol.cs`. The module cache (`WasmModuleCache.cs`) is
shared unchanged.

Host-side scaling, which is a real operational win and not just tidiness:

| @ 9 bodies, per participant | per-life | mind |
|---|---:|---:|
| Concurrent Stores / Instances | 9 | 1 |
| Guest threads | 9 | 1 |
| Peak linear memory | 9 × 64 MiB = 576 MiB | 1 × 128 MiB |
| Store creations per match | one per life start (~25–40 with respawns) | 1 |
| Startup fuel per match | 5 B × (~25–40) = 125–200 B | 5 B |

Peak match memory (2 participants) falls from ~1.13 GiB to 256 MiB. That
matters for `BOTARENA_MATCH_WORKERS` headroom on the single production VPS.

### 4.2 Fuel: flat or body-count-scaled?

Today: **200 million fuel per tick** per life, **5 billion startup fuel**, a
30-second wall-clock backstop, epoch armed before `_start`, on every released
message, and for `MatchEnd` (actor runtime configuration 1.0). At 9 bodies a
participant therefore commands 1.8 B fuel per tick, spread across 9 calls.

Three candidate policies:

| Policy | @1 body | @9 bodies | Problem |
|---|---:|---:|---|
| **Flat at 200 M** | 200 M | 200 M | Catastrophic. A mind does 9 bodies' work in one call; this is a 9× cut disguised as parity. Rejected. |
| **Flat at 1.8 B** (roster max × 200 M) | 1.8 B | 1.8 B | Works, but a 1-body mind gets 9× the compute of a 9-body mind *per body*. Creates a (weak, weird) gradient toward having fewer bodies, and makes the budget a function of the roster arm rather than of the work. |
| **Scaled: `base + perBody × liveOwnBodies`** | 450 M | 2.05 B | Tracks the work. Recommended. |

**Recommendation: `mindTickFuel = 250,000,000 + 200,000,000 × liveOwnBodies`.**

- The `perBody` term is **exactly today's per-life budget**, so per-body
  compute is preserved unchanged and the A/B null pin (§7.2) cannot be
  confounded by a compute difference.
- The `base` term (250 M, 1.25× a body's budget) funds the once-per-tick shared
  work that has no per-body home: digesting the union, updating the belief map,
  assigning roles. It is available even at zero bodies, which is what makes the
  §2.7 "ticks with no bodies" invariant affordable.
- At 9 bodies this is 2.05 B — **14% more total fuel than today's 9 × 200 M**,
  which is the honest price of the shared reasoning and is a rounding error
  against the 30-second wall-clock backstop.
- **Determinism**: `liveOwnBodies` is authoritative tick-start state, fixed by
  `PrepareTick` before the call and recorded in the replay, so the budget is a
  pure function of replayable state. Two hosts compute the same number.
- The fuel actually **consumed** is recorded per mind-turn in the replay (as
  today per actor-turn), so the diagnostic story is unchanged.

Startup fuel stays **5 B** — paid once per participant per match instead of
once per life. The wall-clock backstop stays 30 s (and note it spans *both*
halves of an exchange, not just the guest's compute). Epoch interruption is
armed identically: before `_start`, on every released message, and after every
reply.

One host detail the new message types must preserve: **the per-tick fuel budget
is refilled only when the released message is an `Observation`** — `Hello`,
`MatchStart` and `MatchEnd` all draw from the one-time startup pool, and the
budget does not accumulate across ticks. The mind path must refill on
`MindObservation` and on nothing else, or startup and shutdown silently gain a
per-tick budget.

### 4.3 Memory and the other limits

Actor runtime configuration 1.0 pins 64 MiB linear memory, 16,384 table
elements, one instance/table/memory per Store, deterministic
`clock_time_get`/`random_get`, immediate `NOSYS` for `poll_oneoff`, no start
section, `_start` export.

Runtime configuration **2.0** (mind) changes exactly two numbers and keeps
everything else:

- **linear memory 64 MiB → 128 MiB.** The mind is the only instance and now
  holds match-long belief state for up to 9 bodies over up to 750 ticks. Even
  doubled, per-participant memory falls 4.5×.
- **fuel per tick** → the §4.2 formula.

Table elements, instance/table/memory counts, shims, `poll_oneoff`, start
section, and `_start` are unchanged. The guest→host frame cap stays 64 KiB
(a 9-entry decision map with role tags is under 1 KB — §4.5) and the host→guest
cap stays 1 MiB.

### 4.4 Attestation and negotiation

Unchanged in mechanism, extended in content. `Hello` may require one exact
profile; the generic mind generation requires `generic-mind-match-1`; an
unknown or unavailable profile produces a typed terminal
`Unsupported("actor-contract-profile", …)` before `MatchStart`.

`Ready` attests the mind schemas — **runtime contract, MindStart, mind
observation, and mind decision** — compiled into the artifact, never echoing
host-supplied versions. The two new things it must attest:

- the **mind runtime contract version**, which is what pins the fuel/memory
  semantics of configuration 2.0; and
- the **decision-map schema**, because an artifact compiled against a
  single-decision reply cannot answer a mind observation at all.

A `generic-actor-match-2` artifact answering a mind `Hello` is classified
exactly as a protocol-0.1 artifact is today: executable, but
mind-profile-ineligible. It remains fully playable on its own profile. The
one exception is the wrap adapter (§7.1): an artifact whose *Guest* is new
enough attests **both** profiles even though its author only wrote
`IGenericActorBot` — `GuestHost.RunDetected<TBot>` already does this kind of
static capability detection (`GuestHost.cs:31-62`).

### 4.5 Wire envelopes

Framing is unchanged: 12-byte header (`NBV2`, major 1, message type, reserved
flags, LE payload length), tagged fields (`uint16` id, `int32` length, bytes),
collections as `int32` count plus length-delimited items, unknown field IDs
skipped, everything else fails closed.

Two new message types on the existing framing, plus a reuse:

```text
MindStart      (host→guest)  — replaces MatchStart for this profile
MindObservation(host→guest)  — replaces Observation
MindDecisions  (guest→host)  — replaces Decision
Ready / Fault / Unsupported / MatchEnd — reused verbatim
```

**`MindObservation` frame shape** (field groups; each is a tagged field):

```text
 1  schemaVersion            int32
 2  tick                     int32
 3  matchContractFingerprint string
 ── delivered ONCE ─────────────────────────────────────────────
10  allies[]        ObservedAllyState     (allied MINDS' bodies; empty in H2H)
11  enemies[]       ObservedEnemyState
12  visibleTiles[]  ObservedTile
13  visibleProjectiles[]?  ObservedProjectile   (null = capability absent)
14  visibleEvents[] ObservedEvent
15  heardSounds[]?  ObservedSound
16  scoreboard      ScoreboardState
17  mode            ModeObservationState  (tagged union: Frontline / Deathmatch)
18  participants[]  ObservedParticipantStatus
 ── per body ──────────────────────────────────────────────────
20  bodies[]        MindBodyState {
                      actorId, generation, formId, classId?, position, facing,
                      health, cooldown, energy?, carriedScrap, routeCooldowns[],
                      pendingSameLifeTransition?, previousActionResolution?,
                      previousPosition?, movedLastTick, lifeStartedTick, origin,
                      roleTag?, actionLegalities[] }
21  slots[]         MindSlotState { unitId, state, classId?, candidateClassIds[] }
 ── reserved (§11) ────────────────────────────────────────────
30  alliedIntents[] AlliedIntent { participantId, tagId, value }   (empty in v1)
```

Every nested record type is **the existing SDK type, unchanged** — the codecs in
`GenericActorWireObservationCodec.cs` for tiles, enemies, projectiles, events,
sounds, mode, scoreboard and legality are reused verbatim. That is deliberate:
it means the mind observation and the actor observation encode the same facts
the same way, which is what makes the §7.2 null pin checkable field by field.

**`MindDecisions` frame shape:**

```text
 1  schemaVersion   int32
 2  tick            int32          (echoed; a stale tick fails the exchange)
10  commands[]      MindCommand {
                       1 unitId    int32
                       2 lifeId    int32
                       3 actionId  string
                       4 actionCode int32
                       5 arguments[] GenericActorActionArgument
                       6 roleTag?  string   (≤24 UTF-8 bytes, §12)
                       7 debugMessage? string (≤4 KiB, as today)
                    }
20  intents[]       DeclaredIntent { tagId, value }   (reserved, §11; Rejected in v1)
```

Bytes, at 9 bodies: a command is ~50–70 B plus arguments; nine of them with role
tags is **well under 1 KB** against the 64 KiB guest→host cap. The decision side
was never the constraint and still is not, even at a 20-body roster.

### 4.6 Payload: the union-once win, quantified

The measured anchor is DECISIONS #189: *"Full-roster observation: 14.1 KB, 1.3%
of the payload cap"* — confirming the cap is the 1 MiB host frame
(14.1 KB / 1 MiB = 1.34%).

The independent structural measurement comes from a real replay's section
breakdown (`out/frontline-labs/stillwater-vs-stillwater-s7`, JSON, so the
proportions are indicative of shape rather than of wire bytes). As a fraction of
one stored observation:

| Group | Members | Share of one observation |
|---|---|---:|
| **Team-shared** | `visibleTiles` 33.1%, `visibleEvents` 16.2%, `visibleProjectiles` 6.1%, `teamUnits` 5.0%, `enemies` 3.9%, `scoreboard` 2.4%, `mode` 2.0%, `participants` 2.0%, `heardSounds` 0.5% | **71.2%** |
| **Allies** | `allies` (N−1 full body records) | **10.8%** |
| **Self + masks** | `self` 7.4% + `actionLegalities` 6.9% | **14.3%** |

`visibleTiles` is the single largest term in the entire system, and it is
100% team-shared. Each tile carries `position`, `isWall`, an `observedBy[]` of
`ActorIdentity`, and a `spawnReservation?` — so it grows as *union size ×
observers*, i.e. it gets **more** shared as the roster grows.

Modelling the per-life observation as `O(N) = S(N) + P + (N−1)·A`:

```text
actor:  N · O(N)  =  N·S(N) + N·P + N(N−1)·A
mind:                   S(N) + N·P
saving:            (N−1)·S(N)      + N(N−1)·A
                   ─────────────    ──────────
                   N−1 redundant    the ENTIRE
                   copies of the    quadratic
                   union            ally term
```

Anchored on the measured `O(9) = 14,438 B`:

```text
today:  9 × 14,438 B                    = 129,942 B  ≈ 127 KB, across 9 frames
mind:   S(9) ≈ 10 KB  +  9 × P ≈ 0.7 KB ≈  16 KB,     in 1 frame
                                          ────────
                                          ≈ 8× less, ≈111 KB/tick saved
```

The 16 KB figure is an estimate built from the measured total and the measured
proportions; the honest band is **16–20 KB, i.e. 6–8×**. Both ends are
comfortable.

Three structural notes that make this more than an efficiency story:

- **The `Allies` term vanishes entirely in head-to-head.** Today it is
  quadratic: N bodies each carry N−1 full body records — 72 records at 9
  bodies, 380 at 20. The mind makes it linear (N body records, once). *That* is
  why the win grows with the roster, and why `--roster legion` (#189) and any
  future larger roster make the case stronger rather than weaker.
- **The projection cost falls by the same factor, and that is the sharper
  win.** `ProjectObservation` is invoked once per live actor, and there is **no
  per-team memoization of the union** — `VisibleTilesFor` runs a full map scan
  per sensor, `ObserversAt` runs per tile per life, `SpawnReservationAt` runs a
  `SingleOrDefault` over the reservation lists *per visible tile per life*, and
  `ModeWorldView()` + `_mode.Project(...)` rebuild the entire world view once
  per life. The result is byte-identical across all N calls. The mind turns an
  `O(N² × mapArea)` per-team-per-tick computation into `O(N × mapArea)`.
  §15 shows this is what makes a larger map affordable at all.
- **Frame headroom improves.** One ~16 KB frame at **1.5% of cap** replaces
  nine 14 KB frames. The measured test
  (`GenericActorLegionObservationSizeTests.AFullRosterObservationFitsTheHostPayloadBudget`)
  carries a 32 KB tripwire beside the 1 MiB contract; the mind observation sits
  under it, and a 20-body roster would still sit near 4% of cap.

### 4.7 Fault semantics

**Ruling: a mind fault is participant-scoped — it costs the participant all its
bodies' decisions that tick, and the existing tolerance disqualifies the
participant and all its slots.**

This is not a new policy; it is the *existing* policy applied to a coarser
unit. `GAME-MODE-ARCHITECTURE.md` §9 already says *"Runtime faults are
participant-scoped across every controlled slot, life, and runtime stage"*, and
already disqualifies a participant across all its slots. The fault **counter**
is already participant-scoped, so its allowance needs no change.

**And under the shipped contract the blast radius does not change at all**,
which is the fact that settles the whole question.
`FrontlineLabsDefinition` constructs
`new ActorRuntimeFaultDefinition(faultsAllowedBeforeDisqualification: 0)`, so
the disqualification threshold is **1**: under `frontline-labs-1`, the *first*
runtime fault of any kind already disqualifies the participant and permanently
dormants every one of its slots. A per-life trap today costs the entire
participant the match. A mind trap costs exactly the same. There is no
regression to price.

The graduated machinery (saturating counter, synthetic `Wait`,
retry-fresh-once) exists and is tested but is unobservable under the shipped
allowance. It only becomes observable on a contract with a non-zero allowance,
and there the difference is:

| | per-life | mind |
|---|---|---|
| A trap costs | that life's decision (synthetic `Wait`) | **every own body's** decision that tick (synthetic `Wait` each) |
| Recovery | discard instance, one fresh create-and-start before that life's next decision | discard Store, one fresh create-and-start before the next tick |
| What is lost | that life's private memory (which respawn would have cleared anyway) | **the mind's entire match-long memory** |

That last row is the only genuinely new consequence, and the design should keep
it rather than paper over it:

- Snapshotting mind memory across a trap is not cheap (128 MiB of linear
  memory), not deterministic in general (a trap can leave a torn heap), and
  would reward writing fragile minds.
- Keeping it makes robustness a real design pressure — a *good* addition to a
  game about writing programs, and consistent with #188's observation that
  *"the population is now the engine's best fuzzer"*.
- It must be **loud in the docs**: "a mind that traps forgets the match — and
  under this contract, loses it." One sentence in the rules card and the author
  packet, plus a distinctive replay presentation (§5.3).

**Recommendation on the allowance: leave it at 0 for the mind profile too.**
Raising it for minds would be a silent difficulty change confounding the §7.2
null pin, and the honest argument for raising it ("one mind fault now costs
nine bodies") is answered by the fact that one per-life fault already costs
nine bodies.

Everything else in §9's fault ordering is inherited unchanged: faults ordered by
participant then create/start/tick/validation stage; decision-validation faults
retain the healthy instance; disqualification cancels all owned pending clocks,
fabrication, replication and same-life work in canonical order, releases claims,
removes surviving projectiles after already-collected damage, retires active
lives without kill/death credit, and permanently dormants owned slots. A
multi-participant scoring team stays eligible while any participant remains —
which is exactly the 2v2 semantics the rider needs, already written.

---

## 5. Replay v3: mind turns

### 5.1 The shape

Replay 3's per-tick chronology today carries `ActorTurns[]` — one entry per
active life, each embedding the exact observation delivered to that life, its
submitted and accepted decision, validated arguments, masks, and resolution.
That is what makes replay 3 *"sufficient for deterministic playback and ML
dataset extraction without re-simulating engine logic"*
(`GAME-MODE-ARCHITECTURE.md` §13) and what its validators check.

For a mind-profile match, `ActorTurns` is replaced by `MindTurns` — one entry
per **participant** per tick:

```text
MindTurn
  participantId
  observation        the exact MindObservation delivered (union ONCE)
  commands[]         { unitId, lifeId, actionId, actionCode, arguments[], roleTag?, debugMessage? }
  resolutions[]      one per own live body, in canonical (unitId, lifeId) order:
                       { unitId, lifeId, submitted?, accepted?, validated?, outcome, runtimeFault? }
  intents[]          reserved, empty in v1
  fuelConsumed       as today
```

Rules:

- A document carries **exactly one** of `actorTurns` / `mindTurns` per tick,
  determined by the contract profile recorded in its header. There is no mixed
  document and no inference from payload.
- `resolutions[]` covers **every own live body**, including bodies the mind did
  not command (outcome carries the synthetic `Wait`). That preserves the
  property the per-life format had — every body's tick is accounted for — which
  the validator and the ML story both depend on.
- `commands[]` may be shorter than `resolutions[]` (the default-`Wait`
  contract, §2.4) and may contain `Rejected` entries naming non-owned or dead
  bodies. Both are recorded; neither is elided.

### 5.2 Size, measured

Replay v3 is JSON, and real documents run **14–31 MB raw / 0.5–0.9 MB gzipped**
(`stillwater-vs-stillwater-s7` 25.5 MB, `spark-line`'s cited wave-8 bastion
match 25.8 MB, the largest found 30.7 MB). Two measured breakdowns:

| Section | s7 (500 ticks, ~2.1 bodies/side) | wave-8 3v3 (445 ticks, ~1.7 turns/tick/side) |
|---|---:|---:|
| `ticks[].actorTurns` | **70.1%** | **76.7%** |
| → of which `.observation` | 65.1% of doc (93% of turns) | 73.2% of doc (95% of turns) |
| → → `.visibleTiles` alone | **21.5% of the whole document** | — |
| `ticks[].tickStart` (mostly `.state`) | 12.2% | 9.8% |
| `ticks[].postState` | 11.8% | 9.3% |
| `ticks[].events` + `.traversals` | 5.6% | 3.9% |
| `header` + `initialFrame` + `result` | 0.11% | 0.16% |

Per-turn cost is **8.4 KB** (s7, no economy) to **13.1 KB** (wave-8, with the
scrap economy).

Modelling a tick as `N·O + N·d + F`, where `F` is the fixed pre/post
`WorldState` pair plus events and traversals:

```text
actorTurns:  N × (O + d) + F
mindTurns:        O + N·d + F
```

At the measured wave-8 3v3 (`O` = 12,509 B, `d` = 595 B, `F` = 13,531 B/tick),
the mind halves the document — 58.1 KB/tick → 28.1 KB/tick, i.e. **25.8 MB →
~12.5 MB**. Projecting to a 750-tick legion match at 9 bodies/side, with `F`
grown to ~25 KB for the larger world state:

```text
actor:  9×12.5 KB + 9×0.6 KB + 25 KB  = 143 KB/tick  → ~107 MB
mind:   1×17.5 KB + 9×0.6 KB + 25 KB  =  48 KB/tick  →  ~36 MB
```

**State the two ratios separately and honestly**: the *chronology* term shrinks
by ≈7× (nine embedded observations become one), but the *document* shrinks by
≈3×, because the pre/post `WorldState` pair — already 24% of a small match's
bytes and growing with body count — is untouched by this change. Anyone quoting
a single number should quote 3×.

This matters operationally beyond disk: `iron-root`'s DX records `evidence/t4`
arriving at **214 MB** (193 MB of it unrequested `viewer.html` files), and the
cohort README already warns that a run too large for ordinary Git must go to
the durable artifact store. A 3× cut on the dominant term keeps mind-era
cohorts inside normal tooling — and if more is wanted, the *next* obvious
reduction is the redundant `tickStart.state`, which the format stores
deliberately so the validator can check the boundary rather than trust it. That
is a separate, later decision; do not bundle it here.

### 5.3 Validator implications

**C# validator (chronology re-derivation).** Strictly *less* work and a
*stronger* check. Today it re-derives and compares N per-life observations per
participant per tick; under the mind it re-derives **one** union per
participant per tick. The union is the thing that was always the interesting
invariant; the per-life specialization was mostly a projection of it.

Refusal rules — the existing ones carry over, and three are added:

- (carried) A document whose stored observation disagrees with the re-derived
  one is refused. Self-consistent-but-impossible histories are refused, as
  today.
- (carried) The DECISIONS #185 team-seed re-derivation: the seed is recorded,
  re-derived, and a forged or **team-swapped** document is refused. Unchanged —
  the seed is still team-scoped; only its consumer moved.
- **(new)** A `commands[]` array containing **two entries for the same
  `(unitId, lifeId)`** is refused as malformed. The engine would never have
  written one: it faults such a decision at submission.
- **(new)** A `resolutions[]` array that does not cover exactly the participant's
  own live-body set for that tick is refused. This is the mind-era equivalent of
  the per-life "exactly those keys" rule, and it is where it belongs — in the
  document validator, not in the player's face.
- **(new)** A published `roleTag` that does not match the last tag the mind set
  for that body is refused (§12). Cheap, and it stops a doctored document from
  narrating a strategy that never happened.

The distinction to keep sharp: **engine-refused-at-runtime** (a `Rejected`
command naming a dead body — legitimate, recorded, replayable) versus
**document-malformed** (a shape the engine could not have produced — refused).
Conflating them would either let forgeries through or make honest replays
unverifiable.

**TypeScript mirror.** Note the file boundary precisely: `web/src/types.ts` is
the frozen **replay-v1** mirror; the v3 mirror is `web/src/replayWireV3.ts`
(wire types) plus `web/src/replayV3Normalize.ts` (validator + normalizer), with
`web/src/replayNormalize.ts` dispatching on `header.replayVersion`.

The v3 mirror gains `ReplayV3MindTurn` beside `ReplayV3ActorTurn`, and
`ReplayV3TickFrame`'s `exact([...])` key list grows a `mindTurns` alternative
discriminated by the header's contract profile. Bounds checks follow the
existing primitives: `integer` (safe-integer), `canonicalUnsigned`/`int64`
(BigInt over decimal strings — never widened to floats), `semanticId` for the
role tag with an added 24-byte cap, and `ensureUnique` over
`(unitId, lifeId)`. The relational pass gains the mind-turn analogue of
*"turns must cover exactly the active actors"*: `resolutions` must be the same
set as the participant's own live bodies.

The division of labour on validation stays exactly as `replayV3Normalize.ts`
already states it for the #185 seed — *"the viewer refuses only what it can
decide alone"*. The mirror bounds-checks; the C# validator re-derives.

**Viewer.** No port required, and one property makes it fit unusually well.
Selection is already keyed by **stable unit**, not by life:
`ReplayStableUnitKey` is `generic:${teamId}:unit:${unitId}` while
`ReplayActorLifeKey` adds `:life:${lifeId}` — so a body that dies and respawns
keeps its panel card and keeps the user's selection. That is precisely the
mind's model of a body, already implemented.

Per-body facts come from `replayPresentation.ts`'s `presentUnit(...)`, which
derives health, cooldown, energy, form, status, position, respawn/unlock/rebuild
ticks, objective presence, carried scrap and channel role **from world state**,
and takes only `actionId`, `actionResult`, `debug`, `visibleTiles` (a count) and
`visibleEnemies` from the actor turn. Under the mind those five come from the
per-body resolution and the single shared observation instead — a change local
to `presentUnit` and `actorTurnFromV3`. Everything else is untouched.

Three additions worth making:

1. **Show role tags** (§12) as a caption under each body and as a line in the
   `BotPanel` card, beside the existing "Doing" and "Objective" lines. This is
   the watchability deliverable: a spectator sees
   `channeler / screen / screen / courier` and understands the set-piece *while
   it happens*. #189 already established that rendering the mechanics is the fix
   for *"didn't see the new mechanisms"*.
2. **Group bodies under their mind.** On a mind-profile replay the panel header
   is the participant and bodies are rows beneath it; on a per-life replay it is
   unchanged. `replayParticipants.ts` already resolves
   `unit.controllerParticipantId` → participant, so the grouping key exists.
3. **Mark a mind fault distinctively** — under a threshold-0 contract "this mind
   forgot the match, and lost it" is a single dramatic frame, and §4.7 makes it
   a real event.

Fog of war needs one decision. Today it is drawn from the *selected unit's*
`turn.observation.visibleTiles` (`render/drawArena.ts`,
`render3d/arenaActors.ts`, `render3d/arenaOverlays.ts`). Under the mind there is
one observation per participant, so per-body fog no longer exists as data.
Recommend: **fog follows the selected body's participant** — i.e. show the
mind's union. That is truthful (it is what the mind actually saw) and it is
also the more interesting picture, since the union is the thing the mind
reasons over. Keep the existing checkbox; relabel it "Show this mind's field of
view".

---

## 6. Qualification, scaffold, and the author packet

### 6.1 What the suites actually do today

The suites are `frontline-qualification-3/4/5` (cumulative T2/T3/T4) on the
`frontline-duel-depth-union-t{2,3,4}-v1` profiles, in
`src/BotArena.Cli/FrontlineLabs{Fundamentals,Tactical,Positional}QualificationCommand.cs`,
with every probe's resolved contract in
`src/BotArena.Engine/FrontlineLabsQualificationDefinition.cs`.

The load-bearing fact for this redesign: **almost every probe is already
one-body.** `CreatePrimeOnlyProbe`, `CreateTacticalSingleLifeProbe` and
`CreatePositionalSingleLifeProbe` strip the topology to `UnitId == 0` and
remove population actions. Only two probes use three slots:
`contract-matrix` (tagged T1) and `automatic-life-cycle` (T2).

### 6.2 The minimal redesign

**T1–T4 probe contracts are untouched.** A mind commanding one body is a
perfectly good subject for `direct-fire`, `straight-evade`, `objective-path`,
all six T3 cases and all five T4 cases. Only the runner's bot hosting changes.
That is the cheapest possible answer and it preserves comparability with the
existing reference artifacts (`house-apprentice` T2, `arc-apprentice` T3,
`breach-apprentice` T4).

Four changes, in order of necessity:

1. **Mint parallel suite and profile IDs.** `frontline-mind-qualification-{3,4,5}`
   on profiles `frontline-mind-union-t{2,3,4}-v1`. A profile ID is a
   pre-registration in this project (`TopologyProfileIdFor` throws rather than
   borrow a neighbouring label); a mind T4 and a per-life T4 must never be
   confused in balance evidence. Exit-code contract (0 / 3 / 2), the
   prerequisite hash-linking chain, `tierAwarded` retention on clean failure,
   and `balanceEvidenceEligible = t4Passed` (#150/#152) all carry over verbatim.
2. **Rewrite the documented T2 `respawn-reorient` requirement.** Its current
   text — *"Resumes mode-directed play after a fresh life with isolated
   memory"* — is false under the mind. Replace with a mind-native competency
   that measures the same thing and is strictly harder:
   **`body-handoff`** — when the body executing a task is destroyed, the mind
   resumes the task with another body within K ticks. Implementable inside the
   existing `ProbePlan` machinery with the existing `Wait`/`OneShot`
   controllers.
3. **Add one T4 mind-native probe: `escort-integrity`.** With three slots, keep
   a channeler stationary on the objective with at least one allied body
   adjacent on the threat axis for K consecutive ticks, from both assignments.
   This is the #187/#188 set-piece and the thing the mind exists to make
   authorable.
4. **Fold the coordination axis into the tiers, for mind artifacts.** C1
   (stable roles, no blocking, no duplicated single-owner task), C2 (focus
   target changes with health/position) and C4 (composition/rotation) are, under
   the mind, one-line expressions (§2.5d). They stop being emergent properties
   worth a separate grade and become T2/T4 pass predicates. C3 and C5 retire for
   mind artifacts — C5's *"avoid synchronized predictability"* is trivially
   satisfiable and no longer diagnostic. The C-axis stays alive for per-life
   artifacts. Side benefit: this finally resolves `coordinationGradeAwarded`
   being *"silently null beside `passed: true`"*, which `arc-light`'s DX has
   raised for four waves.

### 6.3 The scaffold IS the pitch

`templates/botarena-generic-mind/` beside the existing
`templates/botarena-generic-actor/`. `NewCommand.cs` already copies every file
in a template directory with `BOTNAME`/`SDKVERSION`/SDK-reference substitution,
so this is additive.

The default's job is to make **body-role assignment trivial and obvious** —
the scaffold's structure is the argument for the whole architecture:

```text
templates/botarena-generic-mind/
  BOTNAME.cs      the mind: StartMatch / Think / EndMatch, a Role enum,
                  Assign(), and one small method per role
  Roles.cs        the assignment function — the file the author actually edits
  Recall.cs       persistent memory, pre-written: enemy last-seen with staleness,
                  pile ledger, per-body previous positions. THE ergonomics pitch:
                  the thing every wave-8 author hand-rolled, shipped working.
  ArenaBasics.cs  helpers, mind-shaped (see below)
  botarena.json   name / entryType / sdkVersion / appearance / composition (§9)
  README.md       the brief
```

`BOTNAME.cs`'s `Think` ships as a role loop that compiles and plays a coherent
escorted channel out of the box:

```csharp
public void Think(MindContext mind)
{
    _recall.Observe(mind);                          // persistent memory, free
    RoleMap roles = Roles.Assign(mind, _recall);    // ← the file you edit first

    foreach (MindBody body in mind.Bodies)
    {
        body.SetRole(roles[body].Name);             // visible in the viewer
        switch (roles[body])
        {
            case Role.Channeler: Channel(mind, body); break;
            case Role.Screen:    Screen(mind, body, roles.ChannelerFor(body)); break;
            case Role.Courier:   Courier(mind, body); break;
            case Role.Builder:   Build(mind, body); break;
            default:             body.Hold("reserve"); break;
        }
    }
}
```

Two `ArenaBasics` corrections ride along, both from wave-8 unanimity:

- **Fix `Capture`'s channel misread.** `CaptureRules.SurplusWeightScalesGain`
  currently tests `controlPolicy.Contains("net-positive-objective-weight-difference")`
  and therefore returns `false` for the channel policy — *"a bot that trusts the
  scaffold on the arm the scaffold shipped alongside prices every push as 'one
  body nulls any number of opposing bodies', which is the opposite of the
  truth."* Every author hand-wrote the same 20-line replacement. Fix it, and
  make an unrecognized policy **throw** rather than silently answer `false`.
- **Split `ClaimWeight` / `DenialWeight`** out of `ObjectivePresence`. Every
  channel decision needs the split; the helper returns only the sum.

And `ClassOf` must stop recovering a class by splitting form IDs on `-` —
`ClassId` is published per body (§9 makes that load-bearing rather than
cosmetic).

### 6.4 Author-packet deltas

Against `docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md`'s six sections:

| § | Delta |
|---|---|
| 1 Purpose | "one deterministic `IGenericActorBot` per doctrine" → `IGenericMindBot`. One-pass authorship discipline unchanged. |
| 2 Permitted material | add the mind template + `RUNTIME-PROTOCOL.md`'s mind section; the deny-list is unchanged. |
| 3 Common implementation requirements | **rewrite the memory bullet** — "per-life instance + private memory, team state shared through observations only" becomes "one mind instance for the whole match; its fields are your memory; **a runtime fault forgets the match**". **Delete** the coordination-via-recomputation guidance entirely. **Add**: the tick invariant (§2.7), the default-`Wait` contract (§2.4), role tags (§12), and the composition declaration (§9). |
| 4 Budget and repair | unchanged. |
| 5 Deliverables | unchanged, plus: `DX.md` should report *what the mind made easy* as well as hard — the whole point is to measure the ergonomics claim, not assert it. |
| 6 Doctrine assignments | unchanged in form; the doctrine sentences now describe an *army's* behaviour rather than a body's. |

`FRONTLINE-LABS-RULES.md`'s "Runtime, memory, and determinism" section needs a
mind-profile counterpart: today it states *"each active life receives its own
isolated bot instance, deterministic random stream, and private memory"* — under
the mind profile every clause of that paragraph inverts.

---

## 7. Migration

### 7.1 The wrap adapter

`WrappedPerLifeMind` — an `IGenericMindBot` shipped **in the Guest**, not in
player source. It hosts N sub-brains, one per live body:

```text
StartMatch  → keep the contract; construct nothing
Think       → for each own live body:
                if no sub-brain for (unitId, lifeId):
                    construct a fresh IGenericActorBot
                    call StartLife(reconstructed GenericActorMatchStart)
                reconstruct that body's GenericActorContext from the mind observation
                call Tick(...) and forward the decision into the command map
              dispose the sub-brain for any (unitId, lifeId) that is no longer live
```

Properties, each deliberate:

- **No shared memory between sub-brains.** A sub-brain is created on its
  body's first tick and destroyed on its death — reproducing per-life memory
  semantics exactly, including that a same-life form change preserves the
  instance.
- **The reconstruction must be exact.** The per-life `GenericActorContext` a
  sub-brain sees must be field-for-field what the per-life profile would have
  delivered: `Self` from the matching `MindBody`, `Allies` from the *other*
  own bodies plus `MindContext.Allies`, and every team-shared collection passed
  through unchanged. §4.5's decision to reuse the existing nested codecs
  verbatim is what makes this a projection rather than a translation.
- **It lives in the Guest**, because `GuestHost.RunDetected<TBot>`
  (`GuestHost.cs:31-62`) already selects programming models by static type
  analysis. An artifact whose type implements only `IGenericActorBot` gets
  `generic-mind-match-1` support automatically, with **zero source edits**.

The cost is a rebuild, and the cohort archive is designed for exactly that: the
immutable archive unit is a **source/artifact pair**, and the README is explicit
that *"a missing source snapshot makes an artifact reproducible for play but
unusable as an auditable population lineage."* Rebuilding the eight lineages'
archived sources against the new Guest is a scripted operation, and the build
cache key already covers the Sdk/Guest DLL hashes (DECISIONS #84), so it
invalidates correctly on its own.

### 7.2 The null pin — the A/B that proves the profile

Two reads, in order. The first must be boring; the second is the interesting one.

**Read 1 — the null pin.** Take the eight wrapped lineages. Run the full
cohort matrix **twice**: once on `generic-actor-match-2`, once on
`generic-mind-match-1`. The result must be **outcome-identical**.

Not hash-identical — that is impossible and should be said plainly. The
contract profile differs, so the aggregate match fingerprint differs; the turn
shape differs, so the replay bytes differ. The pin is therefore a **comparator**,
not `sha256`:

> For every (pair, seed, assignment): identical winner, identical end tick,
> identical completion reason, identical team scores, and — the strong form —
> an identical per-tick, per-body sequence of accepted actions and authoritative
> world states.

A comparator over the two replay documents proving that is a small, honest
tool, and it is the *only* thing that establishes the mind profile is a change
of driver and not a change of game. If it fails, the reconstruction in §7.1 is
wrong and the failure localizes to a field.

**Read 2 — the value measurement.** Hand-port a small number of lineages
(recommend three, one per class, chosen for the sharpest doctrine contrast) to
native minds. Run **ported-vs-its-own-wrapped-self**, same seeds, same cells.
That measures the architecture's value directly, with the doctrine held as
constant as an honest port allows, and with the wrapped self as a
same-source control — the cleanest A/B this campaign has ever been able to run,
because for once the two arms share an author *and* a strategy.

**A constraint on read 2, stated up front:** every wrapped lineage is
**mono-class by construction** (a per-life artifact declares one `class` in
`botarena.json`), so read 2 can only price the *controller*, never the
*composition*. Mixed compositions (§9) are a separate, later read against
mono-composition minds. Do not let one read carry both factors — that is exactly
the two-factor confound #189 called out when `--roster legion` superseded FIVE
SLOTS' schedule.

### 7.3 Porting the eight lineages

The doctrine transfers as functions; the scaffolding is deleted.

- `Decide(context) → GenericActorDecision` becomes
  `DecideFor(mind, body) → void` (writes a command). Mechanical.
- The `ContractLens` / `ArcFacts` / `MatchLens` / `StoneContract` family —
  contract readers, 800–1,020 lines each — **transfers unchanged**. It reads
  the resolved contract, which is byte-identical.
- The gunnery/stance/aim families (`ShotSolver` 1,025, `ArcGun` 857,
  `Gunnery` 758/670, `StoneAim` 533) — **transfer unchanged**. They solve
  per-body geometry, which is still per-body.
- The coordination layer (`Squad` 576, `Convoy` 727/693, `Column` 662,
  `Traffic` 562, `ArcTraffic` 568) — **deleted and replaced by an assignment
  function**, typically 30–80 lines. This is the port's payoff and the metric to
  report: *lines deleted, and which bugs went with them* (the same-destination
  block that was 82% of `spark-line`'s predecessor's blocked moves; the
  `MovedLastTick` reconstruction footgun; the TeamRandom draw-order desync
  hazard).
- The per-life memory workarounds (`ArcMemory`, previous-position maps) —
  **deleted**, replaced by mind fields.

### 7.4 The platform ask list

From #188's unanimity-ranked list, plus #189's queue:

**Dissolves:**

| Ask | Why it dissolves |
|---|---|
| `movedThisTick` **for own bodies** (8/8 authors) | The mind commanded last tick and holds last tick's positions. Publish `MindBody.MovedLastTick` anyway (§2.3) — it is free and removes the last nine-line footgun. |
| The invest same-tick race | *"Two teammates investing on the same tick against a bank that covers only one… the second is `Blocked`."* One mind, one decision — it simply does not issue two. |
| Sibling collisions | Same-destination blocks, swap blocks, follow-a-vacated-actor blocks. The rules are unchanged; they stop being *accidents*, because assignments are coherent by construction. |
| Duplicated single-owner tasks (coordination C1) | An assignment cannot duplicate itself. |

**Remains:**

| Ask | Status |
|---|---|
| **Enemy `movedThisTick`** | Genuinely remains, and stays the top ask. Persistent memory derives it for a *continuously visible* enemy; it is still underivable for an enemy that left vision and returned — `iron-root`'s exact complaint. The channel's other half is still partial information. Recommend publishing a `claimWeight` / `denialWeight` pair on the Frontline mode state, which the engine computes authoritatively on the tick it publishes the progress. |
| Inter-mind coordination | New home for TeamRandom and declared intents (§11), in 2v2/FFA. |
| `ArenaBasics.Capture` channel misread | Still a helper bug; fixed in the mind scaffold (§6.3). |
| Enemies do not publish `routeCooldowns` | Orthogonal. |
| Abort paths that exit 0 | Orthogonal, and still the reason harnesses must count replay files. |
| `carriedScrap` absent from `activeLives` in replay analysis | Orthogonal; worth fixing while replay turns are being touched anyway. |
| `qualify` writing 36 unrequested `viewer.html` files (214 MB → 21 MB) | Orthogonal, fourth wave, and cheap. |

---

## 8. Build plan — phase rationale

**§17 is the authoritative consolidated plan** (it folds in §§9–16). This
section gives the reasoning for each phase; read them together.

Phases in dependency order. **S** ≈ one agent-session, **M** ≈ two–three,
**L** ≈ four or more with a review gate.

### P0 — Decision record and reserved identifiers (S)

Write DECISIONS #191 from the build ruling. Land, in code but inert:
`generic-mind-match-1` profile constants, the `GenericMind*` version block in
`BotArenaVersions.cs`, and — critically — the **field-ID allocations** for role
tags, allied intents, per-slot chassis, and candidate chassis. Reserving field
IDs is the one thing that is expensive to retrofit; everything else in this memo
can be built late.

*Publishes:* nothing.

### P1 — Engine: mind session and contract (L)

`PrepareTick()` → `Step()` and all 16 tick phases stay exactly as they are.
The changes are contained to three places: a `GenericMindRuntimeCoordinator`
that invokes one runtime per **participant** and fans its command set across
that participant's bodies; a union-once `ProjectMindObservation` that computes
`VisibleTilesFor`/`ObserversAt`/`ModeWorldView` **once per team per tick**
instead of once per life; and the decision-map resolution with the
default-`Wait` / `Rejected` / `Faulted` grammar (§2.4). Plus participant-scoped
fault semantics (§4.7, unchanged in policy), per-slot chassis in topology, and
composition-aware `TopologyProfileIdFor` (§9).

*Gate:* the same match, driven by a mind that trivially forwards one action per
body, produces the identical world chronology as the actor session.
*Publishes:* nothing.

### P2 — SDK / Guest / Runtime.Wasm and codecs (L)

`IGenericMindBot`, `MindContext`, `MindBody`, `MindSlot`, `MindStart`,
`MindDecisions`, role tags. NBV2 mind observation/decision codecs reusing every
existing nested codec. `WasmGenericMindRuntimeFactory` /
`WasmGenericMindRuntime` with one Store per participant. Runtime configuration
2.0 budgets (§4.2/§4.3). `Ready` attestation of the mind schemas. The
`WrappedPerLifeMind` adapter and its `GuestHost.RunDetected` wiring.

*Gate:* host/guest codec parity tests; the wrap adapter reconstructs a per-life
`GenericActorContext` field-for-field.
*Publishes:* SDK/Guest version bump — invalidates the build cache by design
(DECISIONS #84), player-invisible until P4.

### P3 — Replay, validators, TypeScript, viewer (M)

`MindTurns` in `ReplayV3` + serializer. C# validator: union re-derivation, the
three new refusals, #185 seed re-derivation carried over. `replayWireV3.ts` +
`replayV3Normalize.ts` mirror with the profile discriminator and bounds checks
(**not** `types.ts`, which is the frozen replay-v1 mirror). Viewer: role-tag
captions, mind-grouped bot panel, mind-scoped fog, distinctive mind-fault
presentation.

*Gate:* a mind replay verifies; forged documents (duplicate body command,
wrong resolution set, forged role tag, team-swapped seed) are refused.
*Publishes:* web build.

### P4 — CLI, qualification, scaffold, docs (M)

`nilbots new --profile generic-mind` + the template. `nilbots experiment
frontline-labs --profile mind`. `frontline-mind-qualification-{3,4,5}` with the
`body-handoff` and `escort-integrity` probes and the folded coordination axis.
`ArenaBasics` fixes. Docs: `RUNTIME-PROTOCOL.md` (mind profile + configuration
2.0), `GAME-MODE-ARCHITECTURE.md` (fourth compatibility generation),
`CLAUDE.md` (architecture + invariants), `FRONTLINE-LABS-RULES.md` (memory
section), the author packet, `BOT-QUALIFICATION-SUITE.md`, CLI help.

*Publishes:* **CLI.** This is the release gate that matters — CLAUDE.md's #93
rule means `CliVersion` bumps and the release workflow's `publish-cli` runs
**before** `publish-and-deploy`, with `scripts/assert-cli-release.sh` enforcing
the `cli-v<version>` tag. Nothing hosted changes; the mind profile stays local
and behind the existing off-by-default flag.

### P5 — The null pin (S)

Rebuild the eight archived lineages against the new Guest. Run the full matrix
on both profiles. Build and run the outcome comparator. Publish the result as
evidence under the cohort archive.

*Gate:* **outcome-identical, or the profile does not proceed.** This is the
phase that is allowed to stop the project.

### P6 — The port wave (L)

Hand-port three lineages (one per class) to native minds under the standing
one-pass authoring discipline and frozen `DX.md`. Run ported-vs-wrapped-self.
Report: outcome deltas, lines deleted, bug classes removed, and the DX verdict
on whether the ergonomics claim is real.

*Gate:* the first evidence that answers "did this help?" with numbers.

### P7 — Mixed compositions (M)

Per-slot chassis wiring end to end, the registered composition set (three mono
+ two mixed), manifest `composition`, topology profile registration, and the
composition balance read against mono-composition minds.

*Depends on:* P6's verdict — do not stack a second factor on an unmeasured
first (§7.2).

### P9 — Reservations exercised (S, opportunistic)

*(§17 inserts a pacing/map arm phase as P8; this is P9 there.)*

Only when a format with allied minds is actually admitted: turn on
`alliedIntents`, and if the FOUNDRY direction is taken, flip
`chosen-at-activation` from refused to supported (§10). Both are numbers-and-
switch changes by then, which is the entire purpose of §§10–11.

---

---

## 9. Mixed-class compositions

The owner's first addition, and the one with the largest surface: *"multiple bot
classes under the same mind."* Move chassis identity from the **participant** to
the **slot**. A participant commands a *composition*; mono-class becomes the
special case.

### 9.1 The machinery already exists — this is a rewiring, not a capability

Three facts make this far cheaper than it looks.

**(a) The rules catalog is already per-class and already carries several
classes at once.** `FrontlineLabsClassDefinition` expands each chassis into
class-prefixed identifiers — `{id}-prime`, `{id}-child`, `{id}-prime-turret`,
`{id}-child-turret`, `{id}-prime-{stance}`, `{id}-vision`, `{id}-bolt`,
`{id}-volley`, `{id}-prime-respawn`, `{id}-child-ready`,
`{id}-late-child-ready` — and `CreateClassesRules` already builds the union
catalog for two distinct classes (collapsing only on a mirror). A
bulwark-vs-striker contract *today* contains every form, profile, route and
lifecycle profile of both chassis. A mixed composition needs no new catalog
entries; it needs different *references* into the catalog that is already
written.

**(b) The binding point is already per slot.**
`ActorUnitSlotLifecycleAssignmentDefinition` is keyed `(teamId, unitId)` and
already carries `LifecycleProfileId` and `AllowedFormIds` **per slot**. Writing
`bulwark-prime-respawn` + `[bulwark-prime, bulwark-prime-turret]` on slot 0 and
`striker-child-ready` + `[striker-child]` on slot 1 is a legal contract *today*.
The engine kernel needs zero changes: forms, transitions, fabrication, split,
respawn and the legality mask all resolve through the slot's assignment.

**(c) Per-body class is already observable.** Since SDK 0.10.6, `classId` is
published on `self`, on every ally, on every visible enemy, and on participant
status. A mixed army is already *readable* by any contract-driven bot; it is
merely not yet *expressible* by a contract writer.

What is genuinely missing is one field and one expansion path.

### 9.2 The contract change

**`PublicUnitSlot` grows `ClassId`:**

```csharp
public sealed record PublicUnitSlot(
    int TeamId, int UnitId, int ControllerParticipantId,
    string? ClassId = null);          // ← additive, null on a classless ruleset
```

Emitted under the #156 additive-canonical discipline: the canonical writer emits
`classId` on a slot **only when the ruleset declares compositions**, and both
mirrors reject an explicit null as a second encoding — so every existing
contract keeps byte-identical topology and match fingerprints, and pinned
`frontline-labs-1` is untouched.

`PublicScoringTeam.ClassId` and `PublicParticipant.ClassId` change *meaning*
rather than shape: they carry the **composition token** (a registered ID) rather
than a chassis. For a mono composition the token is the chassis ID, so every
existing contract's bytes are unchanged and `--classes bulwark-vs-striker` keeps
meaning exactly what it means today.

`CreateClassesRules`'s `distinct` array generalizes from
`teamZero.Id == teamOne.Id ? [one] : [both]` to the distinct union of chassis
across both compositions. The skill predicates (`HasStance`, `HasVolley`,
`HasShell`) are already per-chassis and need no change.

### 9.3 How skills bind

**Skills are class-bound and stay class-bound: each body carries its chassis's
kit.** A striker body inside a bulwark-led composition bends its shots (1–4 tile
envelope), fires on cadence 2, sees a facing quadrant at range 6, and **cannot**
Anchor — because `MayAnchor` is false on its chassis and the `transform` action
is simply absent from `striker-child`'s `AllowedActionIds`. A bulwark body in
the same army anchors, shells, sees omnidirectionally at range 4, and cannot
bend. Nothing new is needed: the legality mask is computed from
`form.AllowedActionIds` per body and already tells each body exactly what its
own chassis can do.

The one genuinely new authoring fact, which belongs in the rules card: **your
army's capability set is per body, not per team.** A composition's `shoot`
availability, anchor routes, and stance routes differ *between your own
bodies*. That is a strict generalization of the existing "read the mask, don't
assume your class's shape" rule that the qualification suite already enforces
via the duel-depth union profile.

### 9.4 What Fabricate produces in a mixed army

**Ruling: `fabricate` produces the TARGET SLOT's declared chassis, not the
fabricator's own.**

Four arguments, in order of weight:

1. **The contract already has exactly one answer, written per slot.** The target
   slot's lifecycle assignment names its `AllowedFormIds` and
   `LifecycleProfileId`. `CreateLife` already validates that the created form is
   in `slot.Assignment.AllowedFormIds`. The alternative would require the
   fabricator to *override* the slot's declared shape, which is a new rule
   contradicting an existing validator.
2. **The alternative makes a composition's declared shape a lie.** If a
   fabricator built its own chassis, the same declared composition would yield
   different armies depending on which body happened to build — and the topology
   profile ID, which travels into balance evidence, would no longer identify the
   army. `TopologyProfileIdFor` throws rather than borrow a neighbouring label
   for exactly this reason.
3. **The verb's identity is forward placement, not chassis choice.** DECISIONS
   #154 made explicit forward fabrication the fabricator's one exclusive verb;
   #187 kept body count as its monopoly; #189 made it *"the only forward body
   delivery in the game"*. All three survive intact: the fabricator still
   decides *whether* and *when* a body arrives and *where*. It simply does not
   also decide *what*, which was never part of the verb.
4. **It makes §10 a numbers change instead of a rework.** If the slot owns the
   chassis, then "let the slot's chassis be chosen at activation" is a
   well-formed later extension. If the fabricator owned it, choosing would mean
   changing the verb.

A composition whose fabricable slots are all one chassis is exactly today's
behaviour, so the mono case is unchanged.

The `fabricate` legality mask needs one addition to stay truthful: the
`UnitTargetConstraint`'s allowed values should be readable *alongside* the slot
table's `classId`, so a mind can see "slot 4 is Ready and slot 4 is a striker"
without a second lookup. `MindContext.Slots` (§2.3) already carries both — no
new constraint kind.

### 9.5 Keeping the balance read countable

This is the part that decides whether compositions are shippable.

Free composition is combinatorially unreadable: 3 chassis over 8 slots is
6,561 armies per side. Even restricting to multiset shape it is 45. The
campaign's method requires countable cells — wave 8 ran 21 class pairings × 3
seeds × 4 arms = 252 matches, and that was already a large round.

**Recommendation: v1 ships a REGISTERED composition set of five.**

| Token | Prime | Companions | The question it prices |
|---|---|---|---|
| `striker` | striker | striker | control (byte-identical to today) |
| `bulwark` | bulwark | bulwark | control (byte-identical to today) |
| `fabricator` | fabricator | fabricator | control (byte-identical to today) |
| `spearhead` | fabricator | striker + bulwark | **Does mixing beat mono at all?** A fabricator opening that builds a mixed line — the composition the mind's API most obviously enables. |
| `warden` | bulwark | fabricator + striker | **Does the fabricator's monopoly survive being a companion?** A fabricating body that is not the prime tests whether the verb's value is the verb or the chassis. |

The three monos being **byte-identical to today's class arms** is the load-bearing
property: every wave-1..8 cell, every registered factor, and the whole measured
campaign remain valid and comparable. The two mixed presets are the only new
cells.

Cell count: 5 compositions → **15 unordered pairs including mirrors** (vs 6 for
three monos). At 3 seeds that is 45 matches per arm — comfortably inside the
wave-8 envelope, and each mixed cell answers a pre-registered question rather
than filling a grid.

**Free composition is a later LEVEL**, `--composition free`, pre-registered
separately with its own evaluation policy (population sampling rather than
exhaustive cells). Do not ship it alongside the profile: §7.2 already warns
against stacking a second factor on an unmeasured first.

### 9.6 Manifest and CLI

`botarena.json` grows `composition`, and `class` becomes an accepted alias:

```jsonc
{ "name": "Warden", "entryType": "Warden", "sdkVersion": "…",
  "composition": "warden" }              // registered token
// or, at the free level (later):
{ "composition": ["bulwark", "fabricator", "striker", "striker", …] }
// and, unchanged and still valid:
{ "class": "striker" }                   // ≡ "composition": "striker"
```

`BotProject.Class` keeps its JSON name for compatibility and gains
`Composition`; the CLI's existing rule — *"a declared class must agree with any
explicit `--classes`"* and *"a bot is permanently classed"* — generalizes
verbatim to compositions (§16 explains why permanence is the right rule for the
ladder).

`--classes bulwark-vs-striker` gains `--compositions warden-vs-spearhead`, with
the same canonical alphabetical pair ordering and the same `--swap`.

### 9.7 Identity, topology profiles, and the 64-character budget

`TopologyProfileIdFor` currently switches on per-team **slot counts alone**
(`[3,3]`, `[8,8]`, `[9,8]`, `[9,9]`, …) and throws on an unregistered shape,
because *"a profile ID is a pre-registration"*. Under compositions the same
counts can mean different armies, so it must key on **(counts, composition
tokens)** — a small extension of an existing switch, registering e.g.
`frontline-labs-legion-mirror-warden` and
`frontline-labs-legion-warden-vs-spearhead`.

**The budget question, and the answer: composition tokens live in the TOPOLOGY
profile ID, never in the ruleset ID.** The ruleset ID is already at 60 of its
64 characters for the full game (`FrontlineLabsDefinition.cs:1648`), which is
why #189 had to mint short registered composites (`vigil`, `warren`,
`bastille`, `warpath`) rather than spell factors. Adding composition tokens
there would immediately overflow and force another naming round.

The split is also *correct*, not merely convenient: **the ruleset spells
mechanics; the topology spells the army.** Two compositions playing the same
mechanics share a rules fingerprint and differ in format/topology and aggregate
fingerprints — which is exactly the property `GAME-MODE-ARCHITECTURE.md` §3
already requires (*"The rules fingerprint does not change between 1v1 and FFA-4
Deathmatch; the format/topology and aggregate fingerprints do"*). Compositions
are a topology fact by the architecture's own definition.

Composition tokens must still be short (one word, ≤10 characters) because the
topology profile ID has its own 64-byte semantic-ID cap. `spearhead` (9) and
`warden` (6) fit; register anything longer with a shorter name.

### 9.8 The API, commanding a mixed army

The §2 API needs no change — which is the test that the boundary is right. A
mixed composition is just bodies whose `ClassId` and legality masks differ:

```csharp
void Think(MindContext mind)
{
    _recall.Observe(mind);

    // Roles follow CAPABILITY, not name — the rule the class brief already
    // teaches ("prefer conditioning on stats and routes over the name").
    MindBody? anchorer = mind.Bodies.FirstOrDefault(b => b.Action("transform")?.Available == true);
    MindBody? builder  = mind.Bodies.FirstOrDefault(b => b.Action("fabricate")?.AllowedByForm == true);
    var guns           = mind.Bodies.Where(b => b.Action("shoot")?.Constraints
                                                 .OfType<ShotProgramConstraint>()
                                                 .Any(c => c.Allowed) == true);

    // The bulwark holds the point because it is the one that can shell.
    anchorer?.SetRole("shell");
    // The fabricator builds because it is the one that can.
    if (builder is not null) Build(mind, builder);
    // The strikers interrupt because they are the ones with the bend envelope.
    foreach (MindBody gun in guns) { gun.SetRole("interrupt"); Interrupt(mind, gun); }
}
```

This is the composition argument in code: under the per-life model, the three
bodies would each have to derive *the same* answer to "which of us shells,
which builds, which interrupts" from the shared observation — the exact
agreement problem §2.1 measures at 3,788 lines. Under the mind it is three
`FirstOrDefault`s. **Mixed compositions are the feature the mind makes
authorable, and they are close to unauthorable without it.** That is the
strongest single argument for pairing them.

---

## 10. Production headroom: chassis chosen at activation

The owner's second addition: design slot semantics so a slot's chassis **may**
be chosen at activation time, so that FOUNDRY — spend scrap to decide what the
late tranche becomes — is later a numbers change rather than a rework. Build
nothing.

### 10.1 The reserved shape

The slot's chassis becomes a tagged selection rather than a bare string:

```text
slotChassis
  kind : "fixed" | "chosen-at-activation"          // enum, v1 admits only "fixed"
  fixedClassId       : string?                     // required when kind = fixed
  candidateClassIds  : string[]?                   // required when kind = chosen-at-activation, ordered
  selectionActionId  : string?                     // null in v1
```

**v1 hardcodes `kind: "fixed"` on every slot**, the canonical writer omits the
whole block on a ruleset without compositions (#156 discipline), and
`ActorResolvedMatchDefinitionValidator` **refuses** `chosen-at-activation` with
a typed unsupported result. Nothing executes; the shape is reserved.

### 10.2 Exactly what the reservation buys

Four things that are cheap now and expensive later:

1. **The slot's `AllowedFormIds` becomes the UNION over candidates.** The
   resolved-match validator already proves every allowed form has a legal
   placement, a compatible spawn anchor, and satisfiable transition placement
   regions — it would simply check more forms. Retrofitting this after v1 would
   mean re-proving every already-fingerprinted contract.
2. **One reserved parameter kind on the bounded tagged argument union.**
   `ActorActionParameterKind` gains `ClassTarget`, and `fabricate` may declare
   it. `GAME-MODE-ARCHITECTURE.md` §7 is explicit that *"Actions keep stable
   string IDs and numeric codes. Arguments use a bounded tagged union"* — adding
   a variant later is a schema change; reserving the enum value now is free.
   The matching `ClassTargetConstraint(ImmutableArray<string> AllowedClassIds)`
   is reserved in the legality-mask union at the same time.
3. **A declared default, so the engine never asks mid-phase.** An automatic
   activation (`DormantAutomaticActivationAtTick`) resolves at tick start,
   inside `ApplyInitialUnlocks`, where no bot is running and none can be. So a
   `chosen-at-activation` slot **must** declare a default —
   `candidateClassIds[0]` — used whenever no selection was made. This is the one
   piece of the design that is genuinely load-bearing to get right up front,
   because the alternative (a mid-lifecycle-phase callback) would break the
   frozen-observation invariant.
4. **Two observation fields, inert in v1.** `MindSlot.CandidateClassIds`
   (empty = fixed) and `MindSlot.SelectedClassId` (`null` until chosen). Both
   are trailing tagged fields with inert defaults, so an artifact compiled
   before they existed keeps negotiating.

### 10.3 What FOUNDRY then costs

With the above reserved, the FOUNDRY concept — *spend scrap to decide what your
late tranche becomes* — is:

- one new `upgrade-track` entry (`foundry`) in the existing scrap economy,
  whose tier effect is "unlock candidate N on slots declared
  `chosen-at-activation`";
- flipping the validator from refusing `chosen-at-activation` to admitting it;
- a `class-id` argument on `fabricate` for the explicit case;
- numbers.

No new action kind, no new lifecycle family, no new observation shape, no new
profile. That is the whole point of §10.

**One design warning to record now:** a chassis chosen at activation interacts
with the §9.7 topology profile ID, because the army is no longer fully
determined by the contract. The resolution is that the *candidate set* is the
registered topology fact and the *realized* composition is a match outcome
recorded in the replay — the same relationship the economy already has between
declared tracks and purchased tiers. Say it in the FOUNDRY pre-registration, not
in v1.

---

## 11. Inter-mind intents (reserved, ships nothing)

The owner's third addition, and the direct consequence of the #190 rider: 2v2
and FFA are teams of allied minds, so the common-knowledge toolkit needs a home
one level up. Reserve the envelope; ship nothing.

### 11.1 The shape

**Guest → host**, on the mind decision frame (field 20, §4.5):

```text
intents[]  DeclaredIntent {
             1  tagId : semantic id, lowercase kebab, ≤ 32 UTF-8 bytes
             2  value : int64
           }
           at most 8 entries
```

**Host → guest**, on the mind observation frame (field 30, §4.5):

```text
alliedIntents[]  AlliedIntent {
                   1  participantId : int32
                   2  tagId         : semantic id
                   3  value         : int64
                 }
                 at most 8 × (allied participant count)
```

### 11.2 The one-tick delay is the design, not a limitation

`ActorTeamPerceptionDefinition` hard-codes `SameTickDecisionSharing => None`,
and it is one of the oldest invariants in the system: *"Observations are frozen
before any same-tick decisions execute. A life never sees an ally's current
action."* An intent channel that delivered same-tick would break it.

Delivering tick `T`'s `alliedIntents` from tick `T−1`'s decision frames
preserves it exactly, and buys three properties for free:

- **Deterministic.** The intents are a pure function of the previous tick's
  recorded decisions, which are already authoritative and already in the replay.
- **Replayable and forgery-refusable.** The validator re-derives tick `T`'s
  `alliedIntents` from tick `T−1`'s `mindTurns[].intents` — the same
  re-derivation discipline as the #185 team seed, and equally cheap.
- **Symmetric with the game's own telegraph grammar.** A purchase, a windup, an
  anchor route and a claim are all one-tick-visible commitments already. An
  intent is one more, and it reads the same way.

### 11.3 What v1 does

- The codecs know the field IDs and encode/decode them.
- The Engine writes an **always-empty** `alliedIntents` collection; the SDK
  exposes an always-empty `MindContext.AlliedIntents`.
- A non-empty `intents` submission is **`Rejected`** (recorded, non-fatal — the
  §2.4 grammar), not `Faulted`, until a format with allied minds is admitted.
- In head-to-head the collection is empty by construction (one participant per
  team), so the wire cost is one tagged field with a zero count and the bytes
  are effectively unchanged.

**The thing this buys is the field-ID allocation**, which is the only part that
is expensive to retrofit — RUNTIME-PROTOCOL.md's versioning rule is explicit
that *"Reusing a field ID, changing its meaning… requires a new version."*

### 11.4 Why this is where TeamRandom's value actually is

#188's honest finding: *"TeamRandom's first doctrine verdict is
null-to-negative… the capability is sound and its docs praised; no doctrine has
yet found where coordinated unpredictability pays."*

The explanation the mind supplies: intra-team, the scarce thing was
**agreement**, not unpredictability — a shared coin only helps once you have
already solved the harder problem of agreeing on what to do with it, and
solving that (via `Squad`, `Convoy`, `Column`) also solved the coin. Between
allied minds, agreement is genuinely unavailable — two separately authored
artifacts cannot compute each other's plan — so a shared stream the enemy
cannot derive is the *only* channel that exists at tick 0, before any intent
has been delivered.

TeamRandom is therefore not deprecated by the mind. It is delivered unchanged
(team-scoped seed, re-derived per tick), it becomes inert in the shipped H2H
format, and it becomes load-bearing the day a 2v2 format is admitted. Record
that so nobody deletes it in the meantime.

---

## 12. Role tags

The owner's fourth addition: a cosmetic public per-body role label the mind
attaches, published in observations and replays, shown by the viewer. Small,
fully specified for v1.

### 12.1 The specification

| Property | Value |
|---|---|
| Where set | `MindBody.SetRole(string? roleTag)`, buffered like a command |
| Where carried | mind decision frame, `MindCommand` field 6 |
| Shape | canonical lowercase kebab-case semantic ID, **≤ 24 UTF-8 bytes** |
| Vocabulary | **free** — not a closed enum |
| Authority | **none** — cannot affect simulation state, is never an action parameter, the engine never branches on it |
| Stickiness | persists until changed; absent field = unchanged; empty string = clear |
| Published on | `self`, every ally, **and every visible enemy** |
| Replay | in the mind turn's command, and in the per-body observation snapshot |
| Validation | charset + length; and the published tag must equal the last tag set (chronology re-derivation) |

24 bytes rather than the 64-byte semantic-ID cap because this is a display
label sent per body per tick, and the budget should be visibly tight: 24 × 9 =
216 bytes worst case, and only on change.

Free vocabulary rather than a closed enum is the whole point. A closed enum
would publish *our* taxonomy; a free one publishes **the mind's own strategic
vocabulary**, which is the thing worth seeing. `channeler`, `screen`,
`courier`, `interrupt`, `bait`, `anvil`, `sacrifice` — the words an author
chose are themselves the strategy made legible.

### 12.2 Enemy role tags: public

**Recommendation: publish them on visible enemies too.**

- **Precedent.** This game telegraphs on purpose. `mode.scrapTeams` publishes
  both teams' banks and tier purchases **with no visibility requirement at
  all** (#187); anchor routes, windups and reversibility are public in the
  contract before tick 0 (#153/#154); a claim, a hold and its expiry are
  published rather than inferred (#168/#169); the pile ledger deliberately leaks
  enemy death sites because *"the alternative is a race you cannot see"*.
  A visible body's declared job is a smaller leak than any of those.
- **It creates a real player move.** A public label the engine never reads is a
  *free deception channel*: label your channeler a screen. That is a genuine
  strategic object costing zero engine complexity, and it is exactly the kind of
  thing the owner's "real fun complexity" ruling asks for.
- **It is the watchability payoff.** Half the drama of seeing a set-piece is
  seeing both sides' assignments and knowing one of them is wrong.
- **It is bounded.** Only *visible* enemies carry it, so it rides the existing
  perception union with no new visibility rule.

The counter-argument (hidden labels preserve information asymmetry) loses to the
precedent above, and if the owner disagrees the alternative is one boolean on
the contract (`roleTagVisibility: own-team | all`) — cheap to add later, so this
is not a trapping decision.

### 12.3 Viewer

Render the tag as a small caption under the body in both renderers, and as a
line in the `BotPanel` card beside "Doing" and "Objective". Colour by a stable
hash of the tag so `channeler` is the same colour all match and across matches.
Where a tag is absent, render nothing (not "none") — an unlabelled body should
look unlabelled, not broken.

This is the single highest watchability-per-line item in the whole memo, and it
is the direct answer to #189's *"didn't see the new mechanisms"*: a spectator
reading `channeler / screen / screen / courier` understands the escorted channel
without being taught the rules.

---

## 13. Draft-phase headroom

The owner's fifth addition: note what a pre-match composition pick/ban phase
would need, so the design does not foreclose it. Build nothing.

### 13.1 What it would need

**Protocol — cheap.** A phase-0 exchange after `Ready` and before tick 0:

```text
Hello → HelloAck → MindStart → Ready
      → DraftRequest → DraftReply      (repeated K times)
      → MindObservation → MindDecisions …
```

New message types on the **existing** 12-byte NBV2 framing, obeying the
existing correlated request/reply rule (one released request, exactly one
reply, unsolicited/duplicate/stale/wrong-kind fails the participant). No
framing change; new message types are a profile-version matter, and the mind
profile is minting fresh schema numbers anyway.

**Lifecycle — the real problem.** Today's invariant is that the complete
resolved contract is delivered before tick zero and stored in the replay, with
its fingerprint. A draft changes the topology *after* the contract is
delivered. The resolution that preserves the invariant:

1. deliver a **draft contract** at `MindStart` — the candidate pool, the draft
   schedule, and the pick/ban rules — fingerprinted as usual;
2. run the draft;
3. deliver a **resolved-topology addendum** whose fingerprint is computed
   post-draft;
4. store **both** in the replay, so the aggregate match fingerprint covers the
   draft *inputs* and the draft *outcome*.

**Determinism and replay.** Draft picks are bot decisions, so they are already
replayable — but they need their own fuel/epoch budget per step (the mind's
base term is the natural allocation) and their own turn kind, `draftTurns[]`,
beside `mindTurns[]`.

**Fault and secrecy.** A fault during the draft disqualifies before tick 0, so
the format needs a declared default pick (the pool's canonical first) or the
match cannot run. Banning affects the opponent's pool, so draft state must be
delivered per participant under the standard broadcast-secrecy rule.

### 13.2 The one thing v1 must do — and it costs nothing

**Publish the slot table as an OBSERVATION fact, every tick, rather than as a
start-only fact.** That is `MindContext.Slots` in §2.3, and it is already the
recommendation there.

If the slot table lived only on `MindStart`, a draft that changes it would
require an API change to every mind ever written. Published per tick, a draft
that resolves the topology between `Ready` and tick 0 simply produces a slot
table the mind reads at tick 0 like any other. No mind needs rewriting; no
schema needs bumping.

It costs a few hundred bytes per tick against a ~16 KB observation, it removes
a start-time/tick-time asymmetry authors would otherwise have to learn, and it
is the difference between a draft phase being a feature and a draft phase being
a migration. Do it in v1.

---

## 14. Win conditions under the mind game

The owner's sixth addition. Today's only completion paths are base breach and
the territorial-progress timeout, and #189's coarse crusade read had **44 of 63
matches reach max ticks with six draws returned**. Should the win-condition set
widen?

### 14.1 What the machinery already expresses

`GenericActorModeCompletionKind` is `FaultEligibility | ModeObjective |
MaxTicks`, with a fixed terminal precedence (fault-eligibility beats
mode-objective beats max-ticks), and victory is *"a typed part of the mode
definition"* with two shipped variants:

- **breach completion plus territorial timeout ranking** (Frontline);
- **optional score-limit completion plus max-tick score ranking** (Deathmatch's
  kill limit).

So a **score-threshold victory already exists as a typed pattern** — Deathmatch
uses it. And for Frontline, "win at N advances" *is* the breach rule: breach is
three advances in one direction out of five ordered positions. **A lower
threshold is `positionCount`, a number, not a capability.** That is worth
saying plainly because it removes the most obvious candidate from the "new
mechanic" list.

Likewise the cheapest available pacing lever needs **no new capability at
all**: SDK 0.10.3 already added *"optional canonical Frontline capture-gain
schedules"*, so a late-match acceleration (gain rises, or the threshold falls,
after tick T) is expressible in the contract today and is pure data.

### 14.2 Is the wall a doctrine, numbers, or missing-win-condition problem?

The evidence says **numbers, on a doctrine-confounded read**, and #189 says so
itself:

> Pacing note, honest: 44/63 matches reach max-ticks and draws returned (6) —
> eight-body defenses under stale doctrine hold hard; **whether that is the
> doctrine or the numbers is wave-9's question**, and channel-ratchet-retune is
> now the most-live registered factor (home respawns lengthened every walk the
> 40-tick hold was calibrated against).

Three things follow:

1. **The read carries the campaign's heaviest caveat.** #189's crusade numbers
   were taken with the wave-8 cohort playing a game it was not authored for —
   *"stale doctrine on every axis"*. Stale doctrine systematically favours
   defense (the known play still works; the new offense has not been invented),
   so a max-ticks rate from a stale cohort is an **upper bound** on the real
   one, not an estimate.
2. **A registered numeric hypothesis is already on the record and untested.**
   The 40-tick ratchet hold was calibrated against forward rallies; `--pendulum
   hull` replaced every forward arrival with a home walk, lengthening every
   approach the hold was priced against. That is a specific, falsifiable,
   numbers-only explanation for exactly the symptom observed.
3. **Adding a win condition before testing it would repeat a mistake in
   reverse.** #187's central finding was that *"the economy cannot be fixed by
   tuning the economy"* — a structural problem needed a structural fix. The
   converse is equally true: a numbers problem must not be fixed with a
   mechanic. Adding an elimination rule to make matches end faster, when the
   ratchet hold is mistuned, would bury the tuning error under a new mechanic
   and make it permanent.

### 14.3 What the mind profile changes about it

Three effects, and they do not point the same way — which is the argument for
measuring rather than prescribing.

- **It supplies the missing read for free.** Stale doctrine is the read's
  heaviest caveat, and the mind profile obsoletes *all* per-life doctrine by
  construction (#190 cancelled wave 9 for exactly this reason). The first mind
  cohort is therefore the **first fresh read on the legion game**, and it
  answers the doctrine-vs-numbers question as a side effect of shipping. That
  is a strong argument for sequencing: the pacing verdict arrives with the port
  wave (P6) whether or not anyone asks for it.
- **It should make defense better.** Eight or nine coordinated bodies holding a
  point, with persistent knowledge of every approach and coherent screen
  assignment, is a much better defense than eight bodies each solving the
  agreement problem locally. On its face this makes the wall *more* likely.
- **It should also make offense better** — the escorted channel is the whole
  set-piece the mind exists to make authorable (§2.5a), and #188 measured
  escorts as the intended play that authors could barely execute. Coordinated
  attack may gain more than coordinated defense, because attack is the side
  that needed coordination.

Net direction: **unknown, and genuinely so.** That is precisely why the answer
is to measure it on the first fresh cohort rather than legislate now.

### 14.4 Recommendation

**Keep breach + timeout for v1. Register the alternatives with build-ready
specs. Make the first mind cohort attribute the wall before anything is
adopted.**

The pre-registered pacing diagnostic, to run on P6's read:

| Metric | Why |
|---|---|
| max-ticks rate, mind cohort vs the #189 stale-doctrine baseline | isolates doctrine from numbers |
| median decisive-match end tick | a falling median with a flat max-ticks rate means the tail, not the game, is the problem |
| ratchet-hold occupancy: ticks a hold is live / ticks the objective is contested | tests the #189 registered hypothesis directly |
| mean advances per match | distinguishes "nobody can push" from "pushes do not accumulate" |
| draw rate and territorial margin distribution at the wall | a tight margin distribution means the timeout ranking is working; a spike at zero means it is not |

The candidates, ranked by how much they cost and how live they are:

1. **Late-game capture acceleration** — *already expressible, zero new
   capability.* An optional gain/threshold phase schedule after tick T. This is
   the cheapest answer to the wall and should be the **first** thing tried if
   the diagnostic says "numbers". Register as `--capture-gain-phase late-surge`
   in the existing arm grammar.
2. **Lower breach threshold** — *a number* (`positionCount` 5 → 3, i.e. two
   advances to breach). Also zero new capability. Registers as a topology/map
   variant since it changes the objective region list.
3. **Elimination window** — *the one genuinely new victory variant, and the
   most live.* Specified in §14.5.
4. **Economy-threshold victory** — **do not register.** Making the bank or the
   tier board a win condition changes the economy from an amplifier of the
   front into a competing objective. #187's ruling was explicit that upgrades
   must not re-open the #184 triangle and that body count stays the
   fabricator's monopoly; an economic victory path would let a team win without
   contesting the objective at all, which is a different game. #189's *"scraps
   should decide the game"* is satisfied by the economy deciding *who wins the
   front*, which is what it now does.

### 14.5 The elimination window, specified

If the diagnostic says the wall survives a fresh cohort and the numeric levers,
this is the variant to build.

**Rule.** A scoring team that holds **zero live bodies for W consecutive
ticks** loses immediately. `W` is a pre-registered factor.

**Why a window and not an instant.** Under automatic respawns, zero live bodies
is a routine transient — a team wiped on tick D has bodies again at
`D + 1 + 18` for the prime and at each companion's rebuild delay. An instant
rule would make a lucky simultaneous wipe a coin-flip win. A window asks the
real question: *can you keep them dead?*

**Why it is genuinely live now, when it was not before.** Three of #189's own
changes make a sustained wipe a real state rather than a fiction:

- `--pendulum hull` removed forward rally, so **every** arrival is a home walk.
  A wiped team is out of the fight for the respawn delay *plus* the walk, not
  just the delay.
- `--roster legion` means a wipe is 8–9 bodies, which requires overwhelming
  force — it cannot happen by accident.
- The economy is *"allowed to decide"*: a team that won the lanes and bought
  the board has the tools to produce and sustain a wipe. That is the owner's
  stated intent ("enable overpowering the opponent") given a terminal
  expression.

And the mind makes sustaining it authorable: holding a spawn-denial state
across 8–9 bodies for W ticks is a coordination problem of exactly the kind the
mind exists to solve.

**What it costs to build.** One typed victory variant on the Frontline mode
definition, plus one counter in mode state — `zeroBodyTicksByTeam` — which
mirrors the existing `DecayTicksElapsed` pattern exactly (a consecutive-tick
counter that resets on any tick the condition breaks). The engine already
computes `ActiveHealthByTeam` and `EligibleTeamIds` every tick in
`ModeWorldView()`; the fact is authoritative and free. The counter is published
as a trailing tagged field on the Frontline mode state, absent on rulesets
without the arm.

**Interaction to pre-register:** an elimination window and the
fault-eligibility short-circuit must not race. Fault-eligibility already beats
mode-objective in terminal precedence, and a disqualified participant's slots
go permanently dormant — so a disqualified team trivially satisfies "zero live
bodies". The rule must therefore read **eligible teams only**, exactly as
early mode completion already *"compares eligible teams only"*.

**Registered identity:** `--victory wipe-W` composing with the existing arm
grammar; the composite token is a pre-registration like every other.

---

## 15. Map scale for bigger armies

The owner's seventh addition. `frontline-labs-03-legion` carries the classes
map's exact tiles — **23 × 15 = 345 tiles, of which 112 are walls, leaving 233
open floor tiles** — with home pads widened from six tiles to ten (a region
tagging change, not geometry). Does the mind game want a larger map generation?

### 15.1 Density, measured

| Configuration | Bodies on the board | Open tiles per body |
|---|---:|---:|
| The game everything was tuned on (prime + 2, both sides) | 6 | **38.8** |
| Legion mid-game (5 per side after tick 150) | 10 | 23.3 |
| **Legion endgame (8–9 per side)** | **17** | **13.7** |
| FFA-4 at legion scale (hypothetical) | 34 | **6.9** |

**The legion endgame is 2.8× denser than the game every mechanic was measured
on, and FFA-4 would be 5.6× denser.** Every number in the campaign — the
channel's escort geometry, the shell's counter-play, the salvo's zoning, the
16-tick vein walk, the 40-tick ratchet hold — was priced at 38.8 tiles per body.

### 15.2 The three geometric arguments

**(a) Choke widths versus the shell's counter-play rule.** The bulwark's
stance is explicitly priced as *"blanks poke, loses to flanks and multi-angle
bodies"* (#171). Flanking needs open tiles to flank through. The map's central
chamber is entered at `(8,7)` and `(14,7)` — the T4 `entry-initiative` probe's
own geometry — and row 7 is the only fully open row on the board. At 13.7 tiles
per body, a defender with four bodies can physically occupy or cover both
approaches; at 6.9 it is trivial. **The counter-play rule that prices the
game's most-crowned class degrades as a function of density**, and that is a
balance dependency on map size that nobody has measured.

**(b) The vision union is already near-saturated, so fog is already weak.**
Nine sensors at range 4–6 on a 233-tile board cover most of it. §3.4 argued that
persistent memory softens fog in the *time* dimension; density has already
softened it in the *space* dimension, and the two compound. **A larger map is
the cleanest single lever that restores fog** — and notably it restores it
without touching vision profiles, i.e. without a balance change to any class.
That is a strong point in favour of minting one.

**(c) The economy's geometry is load-bearing and constrains the shape.** The
veins sit at `(11,1)` and `(11,13)` — on the centre column — *because the tile
rows are palindromic about `x = 11`, which makes them exactly 16 facing-locked
ticks from both home pads*. #189 records that the mirror is free precisely
because of this. **Any larger generation must preserve row-palindromy about its
centre column**, or the economy's symmetry breaks and every vein number needs
re-deriving.

### 15.3 Payload and fuel at larger scale

Extending §4.6's model. `visibleTiles` is ~33% of a wire observation and 21.5%
of a stored replay, and it scales as *union size × observers*. On a ~660-open-
tile map (2.8× today, restoring the measured 38.8 tiles/body at 9 bodies), the
union roughly doubles rather than triples, because vision range caps each
sensor's contribution:

| | 233 open tiles | ~660 open tiles |
|---|---:|---:|
| Team-shared `S(9)` | ~10 KB | ~18 KB |
| **Per-life observation** `O(9)` | 14.1 KB (measured) | **~22 KB** (est.) |
| **Actor profile**: bytes/tick/participant | 127 KB | **~198 KB** |
| **Mind profile**: bytes/tick/participant | ~16 KB | **~25 KB (2.4% of cap)** |
| Replay document, 750 ticks, actor | ~107 MB | **~165 MB** |
| Replay document, 750 ticks, mind | ~36 MB | **~48 MB** |

**This is the load-bearing conclusion of the section: a larger map is
comfortable under the mind and expensive under per-life.** Under the actor
profile a 2.8× map pushes a legion match toward 165 MB of replay and 198 KB per
participant per tick; under the mind the same map costs 25 KB per tick and a
48 MB replay. And the projection *cost* moves the same way — §4.6's
`O(N² × mapArea)` → `O(N × mapArea)` is precisely the term that a bigger
`mapArea` multiplies.

**The mind is what makes a bigger map practical.** That is an argument for
sequencing them together, and against minting the map first.

Fuel is not a constraint: a route search over 660 tiles rather than 233 is
roughly 3× a term that was never the dominant one, comfortably inside
`250 M + 200 M × N`. Worth noting in the author packet, not worth a budget
change.

### 15.4 Recommendation

**Keep `frontline-labs-03-legion` for the v1 mind profile. Mint a larger
generation as a registered ARM, measured in or after the port wave — never as a
v1 default, and never by editing an existing map.**

The map-as-tuning-surface discipline is already established and #189 followed it
verbatim (*"No existing map is edited"*). Two reasons to hold the map constant
for v1 specifically:

1. **The §7.2 null pin requires it.** The pin proves the mind profile is a
   change of driver and not a change of game. Changing the map in the same step
   destroys that proof.
2. **The mind is itself a large factor.** Stacking a map change on an unmeasured
   controller change is the two-factor confound #189 called out when
   `--roster legion` superseded FIVE SLOTS' schedule.

**The candidate to register**, so the work is specified rather than deferred
into vagueness:

- **`frontline-labs-04-march`, ~35 × 23** (805 tiles, ~65% open ≈ 520 floor
  tiles), restoring ~30 tiles per body at the legion endgame — most of the way
  back to the measured 38.8 without a wholly unfamiliar board.
- Rows **palindromic about the centre column** (§15.2c), so the vein mirror
  stays free and the two vein sites keep equal walk distance from both pads.
- Objective positions stay **five ordered regions** so `TerritorialProgress`,
  the breach rule and every registered capture factor carry over unchanged.
- Home pads sized for the legion roster's seven companion anchors plus the
  prime, as `-03-legion` already does.
- Side lanes (rows 1 and 13 today) preserved as the economy's dedicated
  geography, widened proportionally — #186's *"the side lanes of the map need to
  matter"* is an owner directive, not an artifact of this map's size.
- Registered as an arm token (`--duel-map march`), producing its own map and
  topology fingerprints, never a new default.

### 15.5 What FFA-scale would need — and the thing worth saying now

**FFA is not a map problem; it is a mode problem, and stating that now stops a
map from being designed for it wrongly.**

The Frontline objective is *five ordered positions along an axis* with a signed
`TerritorialProgress` computed as displacement from the centre in a team's
advance direction. That is inherently two-sided: three or four teams have no
consistent "advance direction", and the signed channel has no meaning. FFA-4
Frontline is not expressible on an ordered-line objective, and no map makes it
so.

What FFA actually needs:

- **A mode whose objective is not an axis.** Deathmatch already exists, already
  runs FFA-4 in the proof fixtures, and is the natural first FFA product —
  `GAME-MODE-ARCHITECTURE.md`'s own worked example is *"FFA Deathmatch is
  Deathmatch mode plus an FFA match format"*. A radial or multi-point Frontline
  variant would be a **new typed mode**, not a Frontline map.
- **Rotational rather than reflective map symmetry**, N mirror-fair spawn
  anchor sets, N home pads and banking regions. Map generation 3 already
  supports *"team-neutral spawn pools assigned by the resolved format/topology"*
  and typed placement regions, so the generation is capable; the specific map is
  the work.
- **An FFA rating policy**, which `GAME-MODE-ARCHITECTURE.md` §11 already
  reserves as *"a separate later product decision"* and explicitly forbids
  implementing as looped pairwise Elo.

Nothing in this memo forecloses any of it. The mind's participant boundary
(§1.3) is exactly the right shape for FFA — one mind per participant, one
scoring team per participant, `Allies` empty — and it needs no further work.

---

## 16. Ratings and compositions

The owner's eighth addition: how the ladder treats compositions once they exist.

### 16.1 The recommendation, in four sentences

1. **Rating stays participant-scoped.** The artifact is the mind is the player;
   `BotRating(botId, ladderId, rating, policyState)` is unchanged.
2. **Composition is recorded contract metadata per match**, recoverable from the
   pinned playlist version and the replay's topology, with one optional
   denormalized column if reads want it queryable.
3. **Playlist versions gate profile and composition sets exactly as they gate
   rules today** — a mind playlist with an allowed composition set is a new
   `PlaylistVersion` row and nothing else.
4. **Balance reads gain composition-pair cells**, which is precisely why §9.5's
   registered set of five matters: 15 cells, not 6,561.

### 16.2 Why participant-scoped rating is right, not merely simple

- **It is what #190's boundary says.** The mind *is* the participant. A rating
  is a statement about a player; the player is the submitted artifact.
- **The App is already built for it.** `BotRating` is keyed `(bot ID, ladder
  ID)`; the rating boundary *"accepts all entrants and tied team standings
  atomically"*; `DuelEloV1` must reproduce K=32, floor=100, zero-sum transfers
  exactly. None of that cares how many bodies or chassis a participant drove.
- **The alternative fragments the population.** A composition-scoped ladder
  would rate a *strategy choice* rather than a *player*, and would divide an
  already-small population by five — fewer opponents per ladder, slower rating
  convergence, and a leaderboard nobody can read.
- **It preserves a rule that already shipped.** A bot is *permanently classed*
  today (`ServerCommands.cs` refuses a class change on an existing registration:
  *"is permanently classed as '…'"*). Compositions inherit that rule verbatim:
  **switching composition means a new bot row.** That is what keeps a rating
  honest — a rating earned as `warden` cannot be carried into `spearhead` — and
  it is already implemented, already understood by players, and already tested.

### 16.3 What the App layer needs — almost nothing

**Additively, three small things:**

| Change | Shape |
|---|---|
| `Bot.ClassId` → also `Bot.CompositionId` | One nullable column mirroring the existing `classId`, with the same permanence rule. `class` remains an accepted alias for a mono composition, so no migration of existing rows. |
| `SubmissionContractProfileProbe` third arm | Already noted in §1.2; `supportedContractProfiles` grows a value, its shape is unchanged. |
| A new `PlaylistVersion` row | Pins ruleset, format, map pool, admission, execution policy, **the contract profile**, and now the allowed composition set. |

That third row is the finding worth emphasizing: **the playlist version already
pins the contract profile.** The hosted Labs slice literally pins *"exact
contract profile `generic-actor-match-2`"*. So admitting the mind profile — and
constraining which compositions may enter — is a **data row**, not a code
change. The generic execution lane already *"validates its canonical
fingerprint and engine pin"* and gives *"every immutable playlist version a
distinct retrying queue capability"*, so old workers leave mind work pending
rather than mis-executing it. The compatibility machinery for exactly this was
built in Package H and has been waiting for a consumer.

**What stays untouched:** `DuelEloV1`'s exact reproduction, the legacy Duel
lane and its six-game mirroring, ranked-set locks, broadcast secrecy gates,
series settlement, achievements, notifications, and the generated API clients
(a nullable column on an existing response record regenerates cleanly through
`scripts/generate-api-clients.sh`; a new response *type* would not be needed).
Labs remains unranked, setless and behind `BOTARENA_FRONTLINE_LABS_ENABLED`, so
none of this creates a ranked product.

### 16.4 The balance-read consequence

Composition-pair cells are the reason §9.5's registered set is a hard
requirement rather than a convenience:

| Set | Unordered pairs incl. mirrors | Matches at 3 seeds × 1 arm |
|---|---:|---:|
| 3 mono classes (today) | 6 | 18 |
| **5 registered compositions (v1)** | **15** | **45** |
| Free composition, 3 chassis × 8 slots | 6,561 armies → ~21.5 M pairs | not a number |

Wave 8 ran 252 matches across 21 pairings × 3 seeds × 4 arms and that was a
large round; 15 composition cells at the same seed/arm depth is 180 matches —
inside the envelope. The three monos being byte-identical to today's class arms
(§9.5) means the campaign's existing cells stay directly comparable, so the
mixed cells are *additions* to the evidence rather than a replacement of it.

Two methodology notes to pre-register:

- **The wrapped per-life cohort is mono-class by construction** (§7.2), so the
  null pin and the controller A/B run on mono cells only. Composition is a
  strictly later factor. Do not let one read carry both.
- **`qualificationProfile` gains the composition**, since
  `BOT-QUALIFICATION-SUITE.md` already defines it as *"the semantic capability
  distribution on which a bot earned its cumulative T/C result"* and
  fingerprints it in balance evidence. A T4 earned commanding a mono striker is
  not evidence about commanding a `warden`. The suites themselves need no
  change (§6.2 keeps the duel-depth union profile), but the recorded profile
  must name the composition the artifact declares.

---

## 17. Build plan, revised for the full commission

Phases in dependency order. **S** ≈ one agent-session, **M** ≈ two–three,
**L** ≈ four or more with a review gate. §8's phases stand; the owner's
additions land as follows.

| Phase | Size | Content | Publishes |
|---|---|---|---|
| **P0** Decision record + reserved identifiers | S | DECISIONS #191. Profile constants, `GenericMind*` version block. **All field-ID allocations**: role tags (§12), allied intents (§11), per-slot chassis (§9), candidate chassis (§10). Nothing executes. | nothing |
| **P1** Engine: mind session + contract | L | Per-participant coordinator; union-once projection; decision-map grammar; fault semantics; **per-slot `ClassId` + composition-aware `TopologyProfileIdFor`** (§9.2, §9.7); `slotChassis` shape reserved and refused (§10.1) | nothing |
| **P2** SDK / Guest / Runtime.Wasm + codecs | L | `IGenericMindBot`, `MindContext`, `MindBody`, `MindSlot`, `MindStart`; **role tags** (§12); NBV2 mind codecs incl. the **inert `alliedIntents`/`intents` fields** (§11.3); one Store per participant; runtime configuration 2.0; `Ready` attestation; **`WrappedPerLifeMind`** (§7.1) | SDK/Guest bump |
| **P3** Replay + validators + TS + viewer | M | `MindTurns`; C# re-derivation + the three new refusals + **role-tag chronology check**; `replayWireV3.ts`/`replayV3Normalize.ts` mirror; **role-tag rendering, mind-grouped panel, mind-fault presentation, mind-scoped fog** (§5.3) | web build |
| **P4** CLI + qualification + scaffold + docs | M | `new --profile generic-mind`; `experiment … --profile mind`; **`--compositions`** + manifest (§9.6); `frontline-mind-qualification-{3,4,5}` with `body-handoff` + `escort-integrity` and the folded C-axis; `ArenaBasics` fixes; all doc surfaces | **CLI** (#93 gate: `publish-cli` before deploy) |
| **P5** The null pin | S | Rebuild eight lineages against the new Guest; full matrix on both profiles; outcome comparator | evidence |
| **P6** The port wave | L | Hand-port three lineages; ported-vs-wrapped A/B; **the pre-registered pacing diagnostic** (§14.4) — the first fresh read on the legion game | evidence + the pacing verdict |
| **P7** Mixed compositions | M | Per-slot chassis end to end; the five registered compositions; 15-cell composition read; `Bot.CompositionId` + a mind `PlaylistVersion` row (§16.3) | App row + evidence |
| **P8** Pacing / map arms, if P6 says so | M | In order: late-game capture acceleration (zero new capability), lower breach threshold (a number), **then** the elimination window (§14.5) if both fail. `frontline-labs-04-march` as a registered map arm (§15.4) | evidence |
| **P9** Reservations exercised | S | Only when a format with allied minds is admitted: enable `alliedIntents`. Only if FOUNDRY is taken: admit `chosen-at-activation`. Both are switch-and-numbers by then | — |

**Sequencing rules that must not be broken:**

- **P5 can stop the project.** Outcome-identical or the profile does not
  proceed.
- **P7 waits for P6.** Never stack composition on an unmeasured controller
  change.
- **P8 waits for P6's diagnostic**, and inside P8 the numbers levers are tried
  before the mechanic. A numbers problem must not be fixed with a mechanic
  (§14.2).
- **The map stays fixed through P6.** The null pin requires it, and §15.3 shows
  the map arm is *cheaper* after the mind ships anyway.
- **P0's field IDs are the only irreversible decision in the plan.** Everything
  else can be built late; a reused field ID cannot be taken back.
