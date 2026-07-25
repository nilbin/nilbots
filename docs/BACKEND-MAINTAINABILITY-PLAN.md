# Backend maintainability and invariant ownership

Status: incremental. Phases 1–3 were implemented as the first vertical slice
in July 2026; phases 4–7 remain planned. This is a modular-monolith maintenance
plan, not a rewrite or a new product phase.

## Why this exists

The backend has good foundations: the simulation is pure, the controlled build
path is shared, durable work is explicit, artifacts use an object-store
boundary, and important historical data is snapshotted. The next maintenance
risk is not missing architecture. It is business rules accumulating in large
HTTP endpoint and worker files, then being repeated at every entry point.

Cosmetic entitlement checks exposed the pattern, but the scope is broader:
bot admission, match creation, broadcast secrecy, ranked finalization,
progression, privacy, retries, and time all need one obvious owner.

The goal is a pragmatic modular monolith in which:

- endpoints and workers translate transport or queue messages;
- application use cases own authorization, workflow, and transaction
  boundaries;
- small domain policies and validated values own reusable business rules;
- EF Core and `IObjectStore` remain the persistence boundaries;
- every critical invariant has one named owner and focused tests.

## Existing strengths to preserve

- `BotArena.Engine` stays a pure deterministic simulation library.
- `BotArena.Toolchain` stays the single canonical build path.
- `CompilerSubmissionService` is the useful precedent: orchestration has a
  named home without inventing a separate service deployment.
- `IObjectStore` keeps durable records independent of machine-local paths.
- Bot versions, participant appearance, map presentation, and replay metadata
  are snapshotted for historical truth.
- The background-job table, entitlement grant ledger, deployment health
  checks, and explicit application roles remain appropriate for the first-VPS
  architecture.

## Current pressure points

1. `BotsEndpoints` and `JobWorker` coordinate too many unrelated rules.
2. Appearance validation is required by create, update, submit, and match
   admission, making drift easy.
3. Ranked and unranked match creation repeat eligibility and snapshot logic.
4. Broadcast secrecy relies on each response projection remembering which
   fields are safe.
5. Ranked-set finalization and progression are safe partly by deployment
   convention: only one match worker runs.
6. PostgreSQL-dependent tests can be skipped, so CI can miss constraint,
   migration, transaction, and concurrency failures.
7. Direct `DateTime.UtcNow` calls make time-sensitive behavior harder to test
   consistently.
8. Anonymous response shapes make API and CLI contracts harder to pin.

## Target shape

The normal call shape should be:

```text
endpoint or job handler
    -> application use case
        -> domain policy / validated value
        -> EF Core / object store / engine
```

An application use case is a concrete operation such as
`UpdateBotAppearance`, `AdmitBotVersion`, `CreateRankedMatchSet`, or
`FinalizeRankedMatchSet`. It may use EF Core directly. It does not require a
generic repository, command bus, mediator, or separate assembly.

Extract an abstraction when at least one of these is true:

1. a business rule is repeated by multiple entry points;
2. it protects money, ranking, authorization, immutable history, privacy,
   determinism, or data integrity;
3. tests need to control time, identity, concurrency, or an external provider;
4. multiple implementations are real rather than hypothetical.

## Explicit anti-goals

- No full DDD rewrite and no mandatory new `Domain` project.
- No generic repository or unit-of-work wrapper over EF Core.
- No MediatR-style command/query ceremony for every request.
- No global in-process event bus for ordinary method calls.
- No broker, microservice split, or distributed transaction.
- No attempt to deduplicate the deliberately separate SDK and Engine types.
- No storage inheritance hierarchy beyond the useful `IObjectStore`
  interface.
- No entitlement check during replay rendering. Historical playback remains
  independent of current ownership.

## Invariant register

Each critical invariant gets one named owner, a typed result, a database
constraint or transaction where applicable, and tests at the policy and entry
boundaries.

| Invariant | Intended owner | Enforcement |
| --- | --- | --- |
| Same inputs produce the same replay and hash | `BotArena.Engine` and deterministic runtime adapters | Golden, determinism, and WASM contract tests |
| Submitted source uses the canonical controlled build and admission path | `CompilerSubmissionService` plus a bot-version admission policy | Build-cache identity, smoke test, artifact hash, application tests |
| A bot may equip only a valid, owned appearance | `BotAppearance` value plus `BotAppearancePolicy` | Catalog validation, entitlement query, create/update/submit/match-admission tests |
| A match participant uses an owned bot and active eligible version | Match-admission use case | Ownership/visibility rules, active-version check, integration tests |
| Match participant data is an immutable snapshot | `MatchParticipantSnapshotFactory` | Creation-only writes and replay round-trip tests |
| Unbroadcast outcomes and ratings remain secret | Named broadcast-safe query/projection layer | Contract tests before, during, and after the broadcast window |
| A ranked set finalizes exactly once | `FinalizeRankedMatchSet` use case | Transactional row lock or compare-and-set plus concurrency tests |
| One completed ranked set advances progression once | Ranked progression handler | Durable source ID and unique grant constraint |
| Cosmetic grants/revocations are idempotent and auditable | Entitlement ledger service | Unique keys, source provenance, integration tests |
| Public payloads never expose email or internal identifiers accidentally | Named public DTOs | Reflection privacy test and endpoint contract tests |
| Stored artifacts/replays match their addressed hash | `IObjectStore` callers and verification service | Hash-on-write/read verification and backend conformance tests |
| Only the migration role mutates production schema | Startup/role policy | Configuration validation and deployment smoke tests |

When an invariant changes, update this table or its named owner, add the
appropriate tests, and record durable product behavior in `DECISIONS.md`.

## Cross-cutting foundations

### Time

Inject .NET `TimeProvider` into application services and capture
`GetUtcNow()` once per operation. Persist UTC instants. The engine continues
to own deterministic simulation time; application time must never enter the
simulation.

### Actor and authorization

Resolve authentication at the transport boundary into an explicit actor
context containing account ID, system-account status, and relevant role
claims. Ownership and authorization decisions still happen inside the use
case so CLI, HTTP, workers, and future transports cannot bypass them.

### Validated values

Introduce small values only where they remove repeated parsing or validation:

- bot/projectile/theme presentation IDs;
- normalized accent color;
- `BotAppearance` as one validated selection;
- stable source IDs for idempotent grants.

Keep these values persistence-friendly. Avoid a hierarchy of anaemic wrappers.

### Errors and contracts

Application use cases return typed outcomes with stable error codes. One HTTP
mapper produces `ProblemDetails` with a trace ID; the CLI consumes stable
codes instead of scraping prose. Public endpoints use named DTO records rather
than anonymous objects, especially around matches, bots, accounts, and
entitlements.

### Transactions, retries, and idempotency

Every mutating use case documents:

- its transaction boundary;
- which rows are locked or compared;
- whether the operation is retryable;
- its idempotency key or why one is not required;
- what happens if the worker dies after each durable write.

If payments arrive later, provider webhooks first enter a durable inbox keyed
by provider event ID. Payment state then grants entitlements through the same
ledger; commerce never writes a bot appearance directly.

### Observability

Use structured logs, `ActivitySource`, and `Meter` at application boundaries.
Include operation/trace ID, account ID where appropriate, bot/match/set/job ID,
rules version, duration, and typed outcome; never log source archives, tokens,
or private observations.

Track at least:

- queue depth, claim latency, attempts, and terminal failures by job kind;
- compile duration, cache hit rate, admission result, and artifact bytes;
- match duration, broadcast lag, and replay persistence failures;
- ranked finalization conflicts/retries;
- entitlement grant attempts, duplicates, and failures by source kind.

Audit meaningful security/product writes—appearance changes, submissions,
grants/revocations, competition administration, and future commerce—not
ordinary reads.

### Configuration

Bind role-specific options into typed options and call `ValidateOnStart`.
Invalid role combinations, storage configuration, compiler isolation, public
origin, certificates, and worker concurrency should fail before accepting
traffic or claiming work.

### PostgreSQL fidelity

CI must run a real PostgreSQL service for application integration tests. Tests
cover migrations from an empty database, constraints, indexes that encode
invariants, transaction behavior, concurrency, and data backfills. A local
missing database may skip an opt-in developer suite, but the required CI job
must fail—not skip—when PostgreSQL is unavailable.

## Delivery phases

### Phase 0 — record boundaries and baseline

- Add this plan and invariant register.
- Measure current test counts, build duration, and self-contained web size.
- Mark transport-only versus business-rule regions in the largest endpoint
  and worker files before moving code.
- Do not combine extraction with behavior changes.

### Phase 1 — make PostgreSQL integration tests mandatory in CI

Implementation status: complete. The application suite uses an isolated
database per test and CI sets `BOTARENA_POSTGRES_REQUIRED=true`, making a
missing PostgreSQL service a failure rather than a skip.

- Add a reusable application test host and isolated database fixture.
- Run the latest migrations against an empty PostgreSQL database.
- Make the CI PostgreSQL suite fail if its database is missing.
- Pin current migration, uniqueness, public-payload privacy, broadcast
  secrecy, and representative backfill behavior.
- Add at least one two-connection concurrency test before changing worker
  finalization.

This comes first because later refactors need tests that exercise the database
semantics carrying the invariants.

### Phase 2 — add the shared application primitives

Implementation status: complete for the bot-lifecycle pilot. Expansion to
other operations remains incremental by design.

- Register `TimeProvider.System`; migrate one operation at a time.
- Introduce the explicit actor context.
- Add typed application error codes and one `ProblemDetails` mapper.
- Add validated `AppearanceId`, `AccentColor`, and `BotAppearance` values.
- Add a short transaction/idempotency header to each extracted mutating use
  case.
- Establish `ActivitySource`/`Meter` names and correlation fields.

Do not mass-convert every endpoint. Introduce each primitive through the pilot
slice and expand only when it removes real duplication.

### Phase 3 — bot lifecycle and appearance pilot

Implementation status: complete. `BotAppearancePolicy` is the named invariant
owner for create, update, submit, and defense-in-depth match admission.

- Extract bot creation and appearance updates into application use cases.
- Make create, update, and submit share one appearance policy.
- Compose the existing compiler/submission service rather than duplicating
  admission logic.
- Recheck appearance entitlement during match admission as defense in depth;
  replay rendering still never checks it.
- Add policy unit tests plus HTTP, submit, and PostgreSQL integration tests.
- Decide and document revocation behavior—reset an equipped item or block
  future use—before introducing the first revocable entitlement source.

This is the pilot because the rule is already repeated and the behavior is
easy to verify without touching simulation.

### Phase 4 — match admission, snapshots, and broadcast reads

- Extract common bot/version eligibility from ranked and unranked creation.
- Create one participant snapshot factory for bot identity, version, artifact,
  appearance, owner display data, and rules axes.
- Have ranked and unranked workflows compose the shared policy/factory while
  retaining their distinct matchmaking rules.
- Replace manual response shaping with named public and broadcast-safe
  projections.
- Add contract tests proving every outcome-bearing field remains absent until
  `BroadcastComplete(now)`.

### Phase 5 — worker decomposition and safe finalization

- Split job claiming/dispatch from compile, single-match, ranked-set, replay
  persistence, and finalization handlers.
- Put ranked finalization behind one transactional use case using a row lock
  or explicit compare-and-set.
- Make rating updates, set completion, and progression ordering explicit.
- Add forced-failure tests at durable-write boundaries and two-worker
  finalization tests.
- Add job/finalization metrics and structured outcomes.
- Increase match-worker concurrency only after those tests prove exact-once
  behavior.

### Phase 6 — progression, competitions, and future commerce

- Let the source domain own achievement truth: builds know successful builds,
  matches know completed challenges/ranked sets, and competitions know season
  results.
- Translate qualifying events into idempotent entitlement-ledger grants.
- Define revocation precedence when several sources grant the same item.
- Preserve source provenance and an audit trail.
- Add the durable provider-event inbox only when payment integration starts.
- Do not build a generic achievement rules engine before several materially
  different achievements require one.

### Phase 7 — opportunistic structural hygiene

- Split feature endpoint files after business logic has moved, not merely to
  reduce line count.
- Move role/service registration out of `Program.cs` when a feature has a
  stable registration boundary.
- Add one conformance suite shared by every `IObjectStore` implementation.
- Consolidate seed/backfill helpers when repetition appears.
- Replace remaining high-value anonymous contracts and validate typed options
  at startup.

This phase runs alongside feature work and should not become a blocking
cleanup project.

## Execution order

Run phases 1 through 6 in order; do phase 7 opportunistically. Each phase
should land as small behavior-preserving commits. A phase may stop after the
first useful vertical slice rather than converting the entire application.

The first implementation branch should contain phases 1–3 only. They establish
the test safety net and prove the pattern on bot appearance before match and
worker code are moved.

## Definition of done for an extracted invariant

- One named owner and documented callers.
- Typed success/failure outcomes with stable codes where externally visible.
- Explicit transaction, retry, and idempotency behavior.
- Database constraints or locks where in-memory checks are insufficient.
- Focused policy tests, real PostgreSQL integration tests, and entry-point
  contract tests.
- Logs/metrics useful for diagnosing failures without exposing private data.
- Privacy and broadcast classification for every new public field.
- Relevant architecture and product decisions updated.

## Success measures

- No critical PostgreSQL test is skipped in CI.
- Bot appearance writes contain no duplicated ownership/catalog checks.
- Broadcast-safe field classification is centralized and contract-tested.
- Ranked finalization remains correct with two workers.
- Every retryable mutation states and tests its idempotency behavior.
- CLI/API JSON and error contracts use stable named shapes/codes.
- Application time can be controlled in tests.
- Operations can be traced across endpoint/job, database work, and object
  persistence by correlated IDs.
- New feature work composes existing policies instead of copying checks.
