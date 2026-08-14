# Arc Relay Tapline depth pack

Date: 2026-08-14  
Status: implementation complete for owner review; experimental, not promoted  
Runtime for exhibits: trusted in-process Orders v2 mind  
Map: `arc-relay-tapline-01`

## Outcome

The Tapline depth pack now exists as canonically distinct experiments rather
than as implicit switches or one inseparable bundle. The six design ideas in
the implementation brief are available independently, with combat durability
split into its requested second numerical arm, and together in
`tapline-depth-pack`.

The implementation covers the rules contract, engine, replay projection and
round-trip, CLI summaries, Orders compiler/executor, candidate validation,
viewer, and sheet editor vocabulary. It does not change `Current`, hosted
admission, a playlist, a rating era, or the tracked WASM artifact. Tapline-3 is
still the product/control line.

This is an implementation result. The six combined games below are an exhibit,
not a battery, and do not establish balance, strategic depth, or human fun.

## Exact experiment identities

| Profile | Ruleset | Rules fingerprint | Isolated difference |
| --- | --- | --- | --- |
| `tapline-3` | `arc-relay-tapline-03` | `0ed35d080ffc90fa796f0c3c95b4931e81d1bbbfc8be41ce71125a7bd3399415` | unchanged control |
| `tapline-depth-score-124` | `arc-relay-tapline-depth-score-124-01` | `3d299b9c2d419bd38577ce8f36cd3b1dadc457504444eb3ea777ffb951b79985` | relative 1/2/4 Core value, nine births, 65-tick cadence |
| `tapline-depth-veteran-command` | `arc-relay-tapline-depth-veteran-command-01` | `aebbfb7544ba283e329e5642f832c41cdf7fcf4722b757952ac964e7362d71b0` | veteran facts and target selectors |
| `tapline-depth-respawn-wave` | `arc-relay-tapline-depth-respawn-wave-01` | `1c9e51ef1febd9df58b91bbae724b5bb82b2a37732ec1526407e0f425b3f2ee5` | 30-tick public waves, 15-tick minimum dead time |
| `tapline-depth-roster-wildcard` | `arc-relay-tapline-depth-roster-wildcard-01` | `556e65d7c0541b9ada3a4a20325ecf3f02006e37ec72cdfc31faac02e7efe92d` | at least seven distinct classes in eight slots |
| `tapline-depth-durable-deployments` | `arc-relay-tapline-depth-durable-deployments-01` | `aad8ac7dad3bc4ca905951edbf820eb712ab2ea07388b7b7a9be3847cf8b99c8` | durable/targetable constructs and 3-HP, 30-tick Hardlight |
| `tapline-depth-combat-scale-10` | `arc-relay-tapline-depth-combat-scale-10-01` | `18579e39cc6e1c954d726a0e10a1fe3d99c95ce8d6a7625d5a73791f49068c67` | 30/40/50 hull and complete tenfold magnitude conversion |
| `tapline-depth-durability-plus-10` | `arc-relay-tapline-depth-durability-plus-10-01` | `229042f0497219cc0fb3d8da7d54fcb1b54863837d1c27eb7baa107538292f44` | scale-10 plus 40/50/60 hull only |
| `tapline-depth-pack` | `arc-relay-tapline-depth-pack-02` | `e107e818bbb120c72f8c9bead72e9d995627f04dd3ad1e2b934520bd6c5b8f89` | all six design ideas, using pure scale-10 rather than durability-plus |

The abandoned `arc-relay-tapline-depth-pack-01` scratch run remains only in
temporary evidence and is not registered as the final combined identity.

## Implemented mechanics

### Core economy

- A bank resolves the Core's origin relative to the scoring team as 1, 2, or 4
  charge.
- One bank resolves every crossed three-charge threshold in order, including
  a two-Pulse bank, and stops cleanly at reactor destruction.
- Nine scheduled Well rounds at a 65-tick cadence make the slow nine-safe-bank
  route mechanically possible.
- Public facts include the awarded bank value, carried Core bank value,
  friendly Pulse count, enemy charge remainder, and the highest-value known
  loose-Core target.
- Viewer landmarks and carried-Core presentation explain the relative value
  and possible Pulse result before the bank.

### Veteran command

- `role-highest-level`, `most-experienced-ally`, friendly role targets, and
  hostile `veteran` targets are strict, deterministic Orders words.
- Ties resolve by the documented terrain-distance and stable-identity order;
  unavailable veteran targets fall through to authored target preference.
- Role-targeted Repair Beam and follow behavior support a fixed ace; emergent
  support stays dormant while every candidate is level 1 and releases after
  death/reset.
- Every selection and fallback remains visible in executor reasons.

### Respawn waves

- Both teams share waves at ticks 30, 60, 90, and so on.
- A destroyed body needs 15 complete dead ticks and joins the first eligible
  wave; edge cases immediately before/on/after the cutoff are pinned.
- Public state and Orders facts expose the next wave and eligible friendly and
  enemy counts. The viewer score bug carries the countdown and live/+wave read.

### Roster wildcard

- Candidate admission requires exactly eight slots, at least seven distinct
  classes, no class more than twice, and therefore at most one duplicated
  class.
- Compiler readability for historical max-two sheets remains intact; the
  experimental profile owns the stronger admission rule.
- Editor validation exposes the same rule rather than waiting for server
  rejection.

### Durable deployments

- Hardlight lasts 30 ticks and has 3 HP in the isolated arm or 30 HP under
  scale-10. It blocks movement and shots until destroyed, expired, or replaced.
- Recasting atomically replaces the same stable Mason slot's prior barrier,
  including across death/respawn.
- Ordinary and explicitly classified signature damage use tested construct
  policies. `deployment` is a legal visible hostile fight target.
- `deployment-active`, lifetime percent, and durability percent let an author
  preserve or deliberately relocate a construct without hidden prevention.
- Replay state exposes owner, kind, position, capacity, expiry, suppression,
  and removal reason.

### Combat scale

- Squishy/medium/heavy hull is 30/40/50 and ordinary damage is 10, retaining
  the baseline 3/4/5 hits to kill.
- Veteran damage is +5 per point, capped at +10, and is added after the rear
  multiplier. Front damage is 10/15/20; rear damage is 20/25/30 before Target
  Paint.
- Vitality adds 10 maximum HP to the current life without healing it; death
  resets it.
- Falling Star, Trip Node, Rail Line, Sentinel, Repair Beam, heal zones,
  Target Paint, and Hardlight use the complete rescaled magnitude table.
- `durability-plus-10` changes only hull to 40/50/60. It is intentionally not
  part of the combined profile.

## Health presentation

One proportional bar now replaces per-point and capped health pips for bodies
and damageable constructs in WebGL, Canvas2D, compact cards, and inspected
cards.

- Health and cooldown use the same fixed 0.52-world-unit outer width.
- Only fill fraction changes: 20/40 and 25/50 are both exactly half width.
- Health is green, amber, or red by remaining fraction; a narrow cyan/orange
  rim retains team identity.
- Health is visibly thicker than either cooldown track.
- Exact current/maximum values remain in inspection UI.
- Browser proof at 390x844 measured eight simultaneous compact bars at exactly
  52 CSS pixels wide despite maxima of 30, 40, and 50.

Mac ARM64 and Linux x64 exact Canvas frame baselines were deliberately
re-recorded for this visible change.

## Candidate sheets

Twelve candidate packages compile from `strategy-depth/`:

| Profile | Candidate sheets |
| --- | --- |
| score-124 | `tapline-two-heists-v1`, `tapline-turncoat-intercept-v1` |
| veteran-command | `tapline-crowned-rail-v1`, `tapline-emergent-standard-v1` |
| respawn-wave | `tapline-cutoff-execution-v1`, `tapline-pre-wave-evacuation-v1` |
| roster-wildcard | `tapline-twin-relay-circuit-v1` |
| durable-deployments | `tapline-double-mason-lockbox-v1`, `tapline-demolition-march-v1` |
| combat-scale-10 | `tapline-tempered-standard-v1` |
| durability-plus-10 | `tapline-iron-interval-v1` |
| combined | `tapline-depth-relay-circuit-v1` |

The combined sheet uses separate near and deep Relay jobs, a singleton Kestrel
ace, a three-body vanguard, role-targeted Patchbay support, persistent Mason
cover, score-aware Arc Toss, and a wave-aware reset. Those are authored Orders,
not engine exceptions.

## Combined exhibit

Public phone gallery:
https://sequence-looking-loop-ridge.trycloudflare.com

The standard cells are `w-9001 w-9002 e-9005 e-9009 w-9012 e-9004`, with
`tapline-depth-relay-circuit-v1` against `starter-tapline-v2` under
`tapline-depth-pack` and the trusted in-process runtime.

| Cell | Observed result |
| --- | --- |
| `w-9001` | reactor destroyed at tick 248 |
| `w-9002` | reactor destroyed at tick 248 |
| `e-9005` | reactor destroyed at tick 254 |
| `e-9009` | reactor destroyed at tick 243 |
| `w-9012` | reactor destroyed at tick 338 |
| `e-9004` | reactor destroyed at tick 243 |

All six passed the frozen felt-degeneracy eligibility bars. The replays visibly
contain 1/2/4 banks, damage values 10/15/20/25, vitality/damage/reach/vision
investments, role-targeted repair/follow, 30-HP Hardlight, public waves, and the
premium-Core scoring order. The combined sheet's conditional pre-wave reset did
not trigger in these six cells; the isolated wave candidate remains the direct
evidence for that authored behavior.

Cards show outcomes and the actual opponent. These records describe only the
six shown games and are not interpreted as a balance result.

For `w-9001`:

- rules fingerprint: `e107e818bbb120c72f8c9bead72e9d995627f04dd3ad1e2b934520bd6c5b8f89`
- match-contract fingerprint: `0988b2bae50bfdb2a022d68d83b4cc7c29f9fc800a90799262c5ceeab6e6d43a`
- map fingerprint: `058c97bf95bf7175a37952a4b3207199cad7e9c011cd1af50b4ed3a84781433a`
- replay hash: `112f9cb36204bd14189ea3234e3ee2f69dc18eafa137a207d2fcc2433401d0d0`

A production-build Chrome smoke loaded the public replay at 390x844, started
WebGL playback, exposed 30/40/50 exact health values, and showed the fixed-width
world and card bars.

## Verification

| Check | Result |
| --- | --- |
| `dotnet build BotArena.sln` | pass, 0 warnings / 0 errors |
| `dotnet test tests/BotArena.Engine.Tests --no-build` | 1,522 passed |
| `dotnet test tests/BotArena.Cli.Tests --no-build` | 224 passed |
| `cd web && npm test` | 550 passed on macOS; 550 passed in Linux x64 container |
| `cd web && npm run build` | pass |
| 12 candidate compile checks | pass |
| `git diff --check` | pass |

Focused coverage pins numerical breakpoints, rotations, multi-Pulse resolution,
wave boundaries, admission rules, deployment damage/replacement, Orders
selectors and fallbacks, capped/repeated upgrades, legacy defaults, replay
round-tripping, viewer normalization, fixed health-bar scale, and Tapline-3's
unchanged canonical fingerprint.

## Known risks and next owner decision

- The six-cell combined exhibit cannot say whether any arm is balanced, deeper,
  or fun for a person.
- Relative 4-value raids and two-Pulse banks may be too abrupt; faster supply
  may produce loose-Core clutter.
- Wave cutoffs can create a 30-tick downtime difference and may encourage
  repetitive grouped pushes.
- A protected vitality ace and a 30-tick barrier may be too durable, while
  veteran damage may also erase either too quickly.
- Seven-distinct composition may turn one duplicated utility class into a
  practical requirement.
- The candidate sheets demonstrate vocabulary and mechanics; they are not
  asserted to be optimal strategies.

The next decision is an owner watch: choose which isolated ideas deserve a
registered comparison, whether pure scale-10 or durability-plus has the better
combat feel, and whether the combined identity is worth tuning. Until that
ruling, keep `Current`, hosted playlists, ratings, and frozen WASM unchanged.
