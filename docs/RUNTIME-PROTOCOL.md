# Runtime protocols

Nilbots preserves the shipped duel runtime and two exact actor-contract
generations. Protocol 0.1 is the shipped duel path. Actor framing protocol 1.0
first carried the experimental Frontline-alpha contract and now also carries a
separately negotiated generic actor-match profile. Only the explicit local
Frontline command selects the alpha path; the generic path is under active
implementation and is not admitted by historical `play`, App/server queues, or
ladders.

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
