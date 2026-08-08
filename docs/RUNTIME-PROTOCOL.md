# Runtime protocols

Nilbots preserves the shipped duel runtime and three exact actor-contract
generations. Protocol 0.1 is the shipped duel path. Actor framing protocol 1.0
first carried the experimental Frontline-alpha contract and now also carries two
separately negotiated generic profiles: the per-life `generic-actor-match-2` and
the participant-scoped `generic-mind-match-1`. The two generic profiles coexist
BESIDE each other rather than in sequence — they play the same game and differ
only in who drives it. Only the explicit local Frontline command selects the
alpha path; the generic paths are under active implementation and are not
admitted by historical `play`, App/server queues, or ladders.

## Duel protocol 0.1

Protocol 0.1 is the historical line-oriented UTF-8 contract over:

```text
botarena::next_observation(pointer, capacity) -> length
botarena::post_decision(pointer, length)
```

`Runtime.Wasm/WasmProtocol.cs` and `Guest/GuestProtocol.cs` are its host and
guest twins. Existing artifacts, official rules 0.1–0.5, replay v1, and the
legacy runtime/configuration version strings remain unchanged.

## Actor protocol 1.0

Actor protocol 1.0 uses the same two imports with framed binary messages:

```text
Hello -> HelloAck -> MatchStart -> Ready
      -> Observation -> Decision
      -> Observation -> Decision ...
      -> MatchEnd
```

`Hello` may require one exact actor-contract profile. An absent profile selects
the frozen Frontline-alpha actor contract. The current generic generation
requires `generic-actor-match-2`. `HelloAck` and `Ready` attest that same exact
selection; a guest cannot downgrade, retry with another generation, or infer a
generation from later payload bytes. Unknown or unavailable profiles produce a
typed terminal `Unsupported("actor-contract-profile", ...)` reply before
`MatchStart`.

`Fault` is a guest-to-host terminal reply for negotiation, contract, bot, or
codec failure. `Unsupported` is a typed guest-to-host reply naming a capability
the artifact cannot implement. Every released host request accepts exactly one
correlated guest reply; an unsolicited, duplicate, stale, or wrong-kind reply
fails the life. A guest attests its compiled runtime, MatchStart, observation,
and decision schema versions in `Ready`; it never echoes versions supplied by
the host. A protocol-0.1 artifact answers the binary `Hello` as legacy data and
is classified explicitly as executable-but-Frontline-ineligible.

Every frame has a fixed 12-byte header:

```text
0..3   ASCII "NBV2"
4      protocol major (1)
5      message type
6..7   reserved flags (must be zero)
8..11  little-endian signed payload length
12..   payload
```

Payload objects are tagged fields:

```text
uint16 field ID
int32  little-endian byte length
bytes  field value
```

Collections contain an `int32` count followed by length-delimited items.
Unknown field IDs are skipped. Duplicate fields, missing required fields,
undefined enum values, invalid UTF-8, negative or inconsistent lengths,
trailing collection bytes, excessive nesting, and over-limit frames fail
closed. An absent nullable field means `null`; a present zero-count collection
means supported-but-empty.

Protocol 1.0 limits are:

- host-to-guest frame: 1 MiB;
- guest-to-host frame: 64 KiB;
- collection count: 4,096;
- nesting depth: 64;
- semantic action and form IDs: canonical lowercase kebab case, at most
  64 UTF-8 bytes;
- the bot selector and opaque match/runtime handles: at most 256 UTF-8 bytes;
- debug and fault text: 4 KiB UTF-8.

The 1 MiB host cap is sized for a 32×32 map with multiple allied sensors,
union provenance, projectiles, and events. Legacy-only guests keep their
historical 128 KiB buffer and allocate the larger actor buffer only after
negotiation.

### Generic actor-match profile 2

The generic profile is an all-or-nothing tuple: runtime contract 2,
MatchStart 2, observation 2, decision 2, and resolved match contract 2. It
does not widen or reinterpret Frontline-alpha's schema-1 objects.

MatchStart carries the exact canonical rules-schema-3/map-format-3/match-
contract-schema-2 JSON plus its independently recomputed fingerprints,
topology, life identity, deterministic seed, and lifecycle origin. It also
carries a second, TEAM-scoped seed as a trailing tagged field: one value
shared by every life on a scoring team, derived host-side in its own domain
so neither team can derive the other's. The guest turns it into
`GenericActorContext.TeamRandom`, whose stream is re-derived from (team seed,
tick) at the start of each tick rather than advanced across ticks — which is
what makes teammates agree on the Nth draw of a tick no matter when a life was
born or what it drew before, and therefore makes a randomized team plan common
knowledge without a channel. Being a trailing tagged field, an artifact
compiled before it existed still negotiates and still runs; it simply never
sees the stream. Admission
bounds the canonical contract before any guest receives it:

- at most `1 MiB - 1 KiB` of canonical UTF-8;
- at most 65,536 JSON values;
- at most 4,096 direct items or properties in one container.

Per-tick observations use variable, canonically ordered entity collections;
exact `teamId + unitId + lifeId` identity; participant and lifecycle state;
nullable capability collections whose `null` and empty meanings differ;
generic score channels; a tagged mode state; typed events and lineage; and
per-action legality masks. Class identity is explicit on teams, participants,
self, allies, and visible enemies — absent for a classless contract, and on the
canonical contract emitted only when a ruleset declares classes, so a
class-free ruleset keeps byte-identical bytes (#156). A live territory-ratchet
hold publishes its owner and the tick it lifts. Visible projectile observations
include their advance cadence and damage per hit, while an already-visible tile
publishes the permanent automatic-return claim or the due-tick fabrication or
replication claim that makes it unavailable.
Decisions use stable action ID/code pairs and a bounded tagged argument union.
Signed 64-bit projectile and score values keep their exact integer meaning
across the wire.

The SDK parses static canonical contracts without `System.Text.Json`, keeping
controlled NativeAOT guests below the existing 16 MiB ceiling. Full semantic
cross-catalog validation remains the trusted Engine admission boundary; the
guest independently checks syntax, profile identity, fingerprints, bounds,
and view consistency.

### Generic mind-match profile 1

The participant-scoped generation (DECISIONS #190/#191,
`docs/DESIGN-MIND-ARCHITECTURE-2026-07-31.md`). Framing protocol 1.0 is carried
unchanged: same 12-byte NBV2 header, same tagged fields, same correlated
request/reply rule, same frame caps, same `Fault`/`Unsupported` semantics. New
message types are a profile matter, not a framing matter.

The exact tuple:

| Capability | `generic-actor-match-2` | `generic-mind-match-1` |
|---|---:|---:|
| Framing protocol | 1.0 | **1.0 carried** |
| Resolved match contract schema | 2 | **2 carried** |
| Runtime configuration | 1.0 | **2.0 minted** |
| Runtime contract version | 2 | 1 (fresh namespace) |
| MatchStart schema | 2 | 1 (fresh namespace) |
| Observation schema | 2 | 1 (fresh namespace) |
| Decision schema | 2 | 1 (fresh namespace) |

The match-contract schema is **carried, not minted**, and that is the
load-bearing decision: the rules, map, forms, actions, transitions, lifecycle,
mode and economy are identical, so a ruleset keeps its exact rules and map
fingerprints on either profile. Only the aggregate match fingerprint moves,
because the capability tuple rides inside it. That is what makes a cross-profile
comparison a statement about the DRIVER rather than about the game.

Three message types replace three, and everything else is reused verbatim:

```text
MindStart       (host->guest)  replaces MatchStart
MindObservation (host->guest)  replaces Observation
MindDecisions   (guest->host)  replaces Decision
Ready / Fault / Unsupported / MatchEnd   reused unchanged
```

`MindObservation` carries the team-shared union ONCE — allies, enemies, visible
tiles, projectiles, events, sounds, scoreboard, mode, participants — plus one
`bodies[]` entry per own live body and the participant's complete `slots[]`
table. Every nested record is the existing per-life type encoded by the existing
codec, which is what keeps the two observations comparable field by field. The
slot table is published EVERY tick rather than only at start, so a slot's due
tick, readiness and pending fabrication are always current.

`MindDecisions` is a decision MAP plus one tick-scoped diagnostic string. Its
grammar is deliberately more forgiving than the per-life one, because the
strictness that was right for N runtimes mapped onto N keys is hostile to one
runtime holding a plan:

- every own live body is pre-filled `Wait`, and the mind overwrites what it
  wants moved — forgetting a body costs that body one tick, visibly, in the
  replay, not the match;
- a command naming a body the participant does not own, or one that is not live
  this tick, is `Rejected` — recorded, non-fatal, and forgivable on purpose,
  because a mind's memory outlives its bodies;
- two commands for the same body, a malformed action, or a malformed argument
  is `Faulted` and increments the participant counter, exactly as today.

`Ready` attests the mind runtime-contract, MindStart, observation and decision
schemas compiled into the artifact, never echoing host-supplied versions. A
`generic-actor-match-2`-only artifact answering a mind `Hello` is classified
exactly as a protocol-0.1 artifact is: executable, but mind-profile-ineligible.
The one exception is the wrap adapter — an artifact whose GUEST is new enough
attests **both** profiles even though its author only wrote `IGenericActorBot`,
because `GuestHost.RunDetected` selects programming models by static type
analysis and installs `WrappedPerLifeMind` for a per-life type. One sub-brain
per live body, constructed on that body's first tick and discarded when it is no
longer live, seeded from that body's own published per-life random seed: per-life
memory semantics reproduced exactly. The migration is therefore a rebuild, not
an edit.

## Actor life and sandbox ownership

One submitted artifact factory owns one Wasmtime Engine and one compiled
Module. Every active `(teamId, unitId, lifeId)` owns an independent Store,
Instance, linear memory, globals, deterministic clock/random shims, guest
thread, and bot object. A form change keeps that life. Destruction disposes it;
respawn or refabrication creates fresh private memory.

Actor runtime configuration 1.0 pins:

- 64 MiB linear memory;
- 16,384 table elements;
- one instance, table, and memory per Store;
- 200 million fuel per tick and 5 billion startup fuel by default;
- a 30-second wall-clock backstop;
- deterministic `clock_time_get` and `random_get`;
- immediate `NOSYS` for `poll_oneoff`;
- no WebAssembly start section; bots export `_start`.

Epoch interruption is armed before `_start`, on every released message, and
for `MatchEnd`, so startup, ticks, and shutdown all retain a termination path.

## Mind life and sandbox ownership

Under `generic-mind-match-1` the ownership boundary moves from the life to the
participant:

> One submitted artifact factory owns one Wasmtime Engine and one compiled
> Module. **Every submitted participant owns exactly one Store, Instance,
> linear memory, globals, deterministic shims, guest thread, and mind object,
> for the whole match.** Bodies are data inside that instance. A body's
> destruction disposes nothing; a participant's disqualification or the match's
> end disposes the Store.

Mind runtime configuration **2.0** changes exactly two numbers from
configuration 1.0 and keeps every other pin:

- **linear memory 64 MiB -> 128 MiB.** The mind is the only instance and holds
  match-long belief state for the whole army. Even doubled, per-participant
  memory falls about 4.5x at a nine-body roster, because it replaced nine
  64 MiB instances with one.
- **fuel per tick -> `250,000,000 + 200,000,000 x liveOwnBodies`.** The per-body
  term is exactly the per-life budget, so per-body compute is unchanged and a
  cross-profile comparison cannot be confounded by a compute difference. The
  base term funds the once-per-tick shared work that has no per-body home —
  digesting the union, updating beliefs, assigning roles — and is available at
  zero bodies, which is what makes the "ticks even with nothing alive"
  invariant affordable. `liveOwnBodies` is authoritative tick-start state, so
  the budget is a pure function of replayable state and is recorded per mind
  turn.

Startup fuel stays 5 billion, paid once per participant per match instead of
once per life. Table elements, instance/table/memory counts, deterministic
shims, `poll_oneoff`, the absent start section and the `_start` export are all
unchanged, and so are the frame caps. The per-tick budget is refilled **only**
when the released message is a `MindObservation`: `Hello`, `MindStart` and
`MatchEnd` draw from the one-time startup pool, exactly as their per-life
counterparts do, and the budget never accumulates across ticks.

A mind fault is participant-scoped, which is the existing policy applied to a
coarser unit rather than a new one. It costs every own body its decision that
tick (each gets a synthetic `Wait`), and recovery discards the Store and
create-and-starts once before the next tick — which means **the mind's entire
match-long memory is gone**. That is kept rather than papered over: snapshotting
128 MiB across a trap is not cheap, not deterministic in general, and would
reward writing fragile minds. Under the shipped Labs contract the allowance is
zero, so the first fault also disqualifies the participant and dormants every
slot it owns — exactly as a single per-life fault already did.

The shared tagged codec lives in `BotArena.Sdk`; Guest and Runtime.Wasm use the
same implementation so host/guest field definitions cannot drift. Engine/SDK
object graphs remain deliberately separate and are checked for contract
parity. One controlled artifact may implement legacy duel, Frontline-alpha,
and generic actor bot interfaces; generated capability detection exposes every
implemented interface without constructing a throwaway bot.

## Versioning

Protocol, runtime configuration, actor object schemas, SDK, guest adapter,
controlled-build cache, tracked built-in artifact, and CLI package are
separate axes. Additive unknown fields may retain protocol 1.0. Reusing a
field ID, changing its meaning, or requiring a contract an old guest cannot
attest requires a new version and explicit eligibility handling.

The Frontline-alpha delivery remains the historical SDK/Guest 0.9.0,
actor-protocol/configuration 1.0 checkpoint. Generic profile 2 was introduced
by SDK/Guest 0.10.2, extended in 0.10.3 with optional canonical Frontline
capture-gain schedules, and extended in 0.10.4 with declared delayed first-life
activation and its distinct life-origin reason. Controlled-build pipeline 4
remains unchanged, and CLI 0.9.5 carries that SDK. Actor framing and sandbox
configuration remain 1.0 because these additive static-contract branches do
not change frame or resource semantics. Legacy duel
protocol/configuration remain exactly 0.1.

SDK/Guest 0.10.5 (CLI 0.9.14) grows the per-tick observation instead of the
static contract: the Frontline mode state publishes the live territory-ratchet
hold's owner and expiry, and one visible projectile publishes its firing
profile's advance cadence and damage per hit. **The generic observation schema
version stays 2**, and that is the rule rather than an exemption — these are
additive trailing tagged fields, an older guest ignores an unknown field ID and
still attests the same profile, and the versioned capability block is inside
the fingerprinted match contract, so bumping it would relabel every immutable
generic ruleset including the hosted `frontline-labs-1`. A version moves when a
field ID is reused, a meaning changes, or a guest is asked to attest a contract
it cannot. Replay-v3 documents are a separate matter: the two mode keys and the
two projectile keys are mandatory in the document, so a replay written before
0.10.5 and one written after are not interchangeable.

SDK/Guest 0.10.6 (CLI 0.9.15) adds typed class identity and spawn-reservation
observability under exactly that rule, and **the profile stays
`generic-actor-match-2`**. The observation side is trailing tagged additions:
`classId` on self, allies, visible enemies, and participant status, and a
`spawnReservation` on an already-visible tile. The contract side is the #156
additive-canonical pattern rather than a new generation — the canonical writer
emits `classId` on a team or participant **only when the ruleset declares
classes**, and both mirrors reject an explicit null as a second encoding of the
same contract, so every class-free ruleset keeps byte-identical topology and
match fingerprints and the pinned `frontline-labs-1` fingerprint is untouched.
A ruleset that DOES declare classes is a new content-identified ruleset whose
fingerprint is new anyway, and an artifact built before 0.10.6 faults on it at
tick 0 exactly as `ratchetHoldTicks` already made it fault — the accepted #156
consequence, not a new one. Minting a `generic-actor-match-3` was considered
and rejected: the capability tuple rides inside the fingerprinted match
contract, so bumping it relabels every immutable generic ruleset, invalidates
the whole frozen phase-1 artifact population on contracts it can still play,
and would force a second registered hosted generation for identical mechanics.
Replay-v3 documents again grow mandatory keys — `classId` on the four observed
actor shapes and `spawnReservation` on a visible tile, both nullable and always
present — so the engine-authored fixtures were regenerated.

SDK/Guest 0.10.11 (CLI 0.9.28) mints the **mind profile**,
`generic-mind-match-1`, beside `generic-actor-match-2` rather than after it, and
the versioning rule is what decides that shape rather than an extension. `Self`
becomes `Bodies[]`; `Allies` changes meaning from "my team's other bodies" to
"allied bodies I do not control"; the decision changes from one action to a map.
Reusing field IDs, changing a meaning, and asking a guest to attest a contract
it cannot are all three of the conditions above, so no trailing-tagged-field
trick reaches it. Equally it must not REPLACE the per-life generation: the
hosted `frontline-labs` v1 playlist and its pinned fingerprints, the measured
lineages, and every frozen cohort's evidence all depend on those bytes staying
exact, and the same argument that rejected `generic-actor-match-3` for a smaller
change applies here with full force.

The resolved match-contract schema is **carried at 2** for exactly that reason:
a mind plays the same game. The mind observation and the actor observation
therefore encode the same facts the same way, using the same nested codecs, and
a cross-profile comparison is checkable field by field.

**Every artifact built from SDK/Guest 0.10.11 attests both profiles**, natively
if its type implements `IGenericMindBot` and through the guest's wrap adapter
otherwise, so a MIXED match — a native mind against a per-life artifact — is an
ordinary thing to run. The host cannot tell the two apart, which is the
migration working as designed. Profile is a MATCH-level choice, never a
per-entrant one: one match resolves one contract, and two contracts in one match
is not a thing a fingerprinted match can be.
