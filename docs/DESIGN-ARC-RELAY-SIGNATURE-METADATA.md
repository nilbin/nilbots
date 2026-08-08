# Design note: signature metadata in the rules contract

Status: proposal, deferred to the next rules-version boundary. Motivated by
friction ledger #11 and the 2026-08-05 dead-kit episode (four classes whose
signatures no tactical executor could cast, silently, for the archetype
generation's whole lifetime).

## Problem

Executors dispatch signatures by hardcoded id: which category a signature
belongs to (damage / control / support), which argument kind it takes
(heading, position, unit, direction, parameterless), and what guard makes a
cast sensible (adjacency for a burst, range for a deploy). Every new class
therefore requires editing every executor, and a missed edit fails silently —
the class plays gun-only. The tactical mind now guards this with a
match-start coverage assertion (unknown signature → loud failure), but the
assertion only converts silent breakage into loud breakage; it does not
remove the per-executor table.

## Proposal

`ArcRelaySignature` in the rules contract gains three derived-metadata
fields, authored beside each signature definition in the ruleset:

- `category`: `damage | control | support | movement | custody` — the
  designed role, the same taxonomy the tactical executor already uses.
- `argumentKind`: already derivable from `SignatureParameters(...)` in
  `ArcRelayH0Definition`; promoting it into the contract makes the derivation
  consumer-visible instead of engine-internal.
- `engagementRange`: the distance inside which a combat cast is sensible
  (today hand-encoded as guards: kinetic-burst 1, null-field 3, trip-node 4,
  sentinel-seed 6, smoke-canister 6).

Executors then dispatch generically: enumerate contract signatures, order by
category, cast via the argument-kind-appropriate helper under the range
guard. A new class ships with its metadata and plays correctly in every
generic executor with zero executor edits. The coverage assertion inverts
into a contract-completeness check (every signature must carry metadata),
enforced at ruleset authoring time instead of match time.

## Why not now

The public rules manifest serializes into the canonical rules fingerprint.
Adding fields changes every rules fingerprint, which invalidates every
frozen match contract, binding, and golden — a rules-version-bump-class
change (see the versioning invariants in CLAUDE.md). Excluding the metadata
from canonical serialization was considered and rejected: aliases and
presentation stay outside component hashes precisely because they do not
affect play, but this metadata is consumed by minds during play, so hiding
it from the fingerprint would let two contracts that play differently hash
identically.

## Migration path

1. At the next deliberate rules version (any gameplay change that already
   bumps fingerprints), add the three fields to `ArcRelaySignatureDefinition`
   and project them through `PublicRulesManifestFactory`.
2. Add the ruleset-side completeness check (every signature carries
   metadata; every category non-empty taxonomy member).
3. Port the tactical executor's `SignaturePlays` table to read the contract,
   keeping the hand table only as an override hook for executor-specific
   behavior (e.g. the Switchback escape-swap guard, which is judgment, not
   metadata).
4. Retire `RoleHandledSignatures`/`UnwiredSignatures` in favor of the
   `movement`/`custody` categories.

## Interim rule (in force now)

Until then, the tactical executor's three-way coverage
(`SignaturePlays` + `RoleHandledSignatures` + `UnwiredSignatures`) plus the
match-start assertion is the contract: adding a class means adding exactly
one entry, and forgetting fails the first screening game with a message
naming the signature.
