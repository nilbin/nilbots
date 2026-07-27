# Runtime protocols

Nilbots currently preserves two independent runtime contracts. Protocol 0.1
is the shipped duel path. Actor protocol 1.0 is the internal Frontline path;
the CLI, App, server admission, and ladders do not select it yet.

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

The 1 MiB host cap is sized for a 32×32 map with five allied sensors, union
provenance, projectiles, and events. Legacy-only guests keep their historical
128 KiB buffer and allocate the larger actor buffer only after negotiation.

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
parity.

## Versioning

Protocol, runtime configuration, actor object schemas, SDK, guest adapter,
controlled-build cache, tracked built-in artifact, and CLI package are
separate axes. Additive unknown fields may retain protocol 1.0. Reusing a
field ID, changing its meaning, or requiring a contract an old guest cannot
attest requires a new version and explicit eligibility handling.

The initial actor delivery uses SDK 0.9.0, guest adapter 0.9.0, actor
protocol/configuration 1.0, and CLI package 0.6.0. Legacy protocol and
configuration remain 0.1.
