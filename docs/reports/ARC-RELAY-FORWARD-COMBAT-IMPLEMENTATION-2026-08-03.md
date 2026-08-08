# Arc Relay forward-combat implementation — 2026-08-03

## Outcome

Arc Relay entrant playlist v4 is a new, versioned combat contract. Mobile
basic weapons expose exactly the facing heading and its two adjacent diagonal
headings. Standard, Swift, and Deliberate now make different, explicit
movement/facing commitments; signature targeting is unchanged and deployed
Sentinels remain omnidirectional.

The current hosted stock entrant is the same operation-capable source and WASM
artifact evaluated by the live-operation and dominance harnesses. Entrant
playlist v3 remains the immutable Counterflow/omnidirectional version, and v2
remains the earlier Home Gates version. This pass changes neither historical
rules/map identities nor historical replay bytes.

This is an implementation, deterministic reliability, and representative
dominance read. It is not a claim that humans find the game fun.

## Contract

| Surface | Current v4 rule |
| --- | --- |
| Basic fire | absolute submitted eight-way heading, constrained to facing ±1 sector; firing never silently rotates |
| Standard | forward, forward-diagonal, and lateral travel preserve facing; rear and rear-diagonal travel reverse facing |
| Deliberate | only the current cardinal forward heading is legal; turning remains a separate action |
| Swift ordinary move | eight-way travel projects deterministically onto cardinal facing |
| Swift strafe | separate `strafe-eight-way` action preserves facing; the stock mind selects it only to retain an immediate engagement |
| Signatures | unchanged authored target types and ranges |
| Sentinel | stationary, faceless, omnidirectional automated exception |

The engine canonical writer, rules validator, SDK reader, legality masks,
authoritative resolution, chronology reconstruction, replay-v3 serializer,
TypeScript wire type, and strict web normalizer carry the same fields and
closed enum values. Historical contracts omit every new optional field.

## Stock-mind behavior

The current first-party algorithm reads the legality mask rather than assuming
aim freedom. It prepares an off-cone shot, commits a route-facing turn before a
Deliberate move, and makes Swift's turn-versus-strafe choice from the immediate
post-move engagement. Route moves and route-facing turns cannot be immediately
undone by opportunistic off-cone aim preparation.

Carrier recovery is bounded independently of operation execution. A blocked
return lane first asks allied traffic to clear, may hand a Core strictly closer
to home, and after twelve carried ticks without a new best home distance lets
the baseline return path preempt operation positioning. Committed recovery
uses a deterministic strict-corner shortest route that treats visible spawn
reservations as durable obstacles. Transient body traffic may delay its first
step but cannot redirect the carrier into an equal-distance orbit. The strict
homeward distance test prevents handoff ping-pong.

## Registered repair chain

| Candidate | Outcome and retained reason |
| --- | --- |
| v1 | Product forward-aware baseline exposed widespread old-mind stalls; rejected before a balance read. |
| v2 | Operation-capable mind reached 158/160 eligible matches, but one sheet was 0–10 and Deliberate-heavy sides won 26.7%. |
| v3–v6 | Targeted, pre-registered return-lane repairs isolated facing preparation, traffic, projectile avoidance, and self-blocking cover. Each failed the same retained smoke until the actual handoff obstruction was identified. |
| v7 | Strictly homeward handoff cleared 160/160 eligibility, but Deliberate still spent 41.5% of actions rotating and high exposure won 25.0%. |
| v8 | Route/aim commitment cut Deliberate rotation to 34.1%, raised high-exposure wins to 31.7%, and gave every sheet a win. Two operation-positioned carriers still tripped the home-progress bar; the exposure delta missed by 3.0 points. |
| v9 | Added bounded carrier preemption, but two in-progress cells proved its Chebyshev progress measure disagreed with actual walk distance; the doomed sweep was stopped. |
| v10 | Aligned recovery debt with static strict-corner distance; exact smoke showed the old modulo window could still expire and reopen an away-from-home route. |
| v11 | Kept committed recovery active until progress; one smoke cleared, while the other exposed a permanent spawn reservation on the nominal shortest ray. |
| v12 | Permitted legal non-worsening local detours and cleared both older regressions, then a partial cohort exposed a 68-tick east/west orbit beside a visible reactor spawn reservation. The sweep was stopped. |
| v13 | Replaced greedy local recovery with one deterministic reservation-aware shortest route and froze two complete campaign seeds before outcomes. All three exact regressions were eligible; their worst home-progress debt was 20 ticks, below the 30-tick bar. |
| v14 | v13 completed 320/320 canonical and eligible with all sheets winning, but failed the frozen class/handling gates: Deliberate-heavy teams won 26.7% versus 55.9%, while Minesmith appeared only in five strong sheets and correlated at 80%. No stat was tuned. v14 retains route-turn commitment while allowing facing-locked aim preparation after movement, then repeats the identical sample and gates. |

No class statistic was tuned from these correlated sheet results. Every
registration, frozen cohort, plan hash, failed read, and final read is retained
under `balance/arc-relay-forward-combat-v*.json` and
`arena-bots/arc-relay/forward-combat-cohort-v*-2026-08-03/`.

## Final evidence

| Frozen input | SHA-256 / identity |
| --- | --- |
| Rules | `arc-relay-forward-combat-01` / `0acab8947506cf6224ac00029d8c3d62e9ec61cbda224e2a955db45b711b90a9` |
| Accepted map | `arc-relay-threefold-depth-counterflow-01` / `5ca7d1a1826791d736465d352c1558793846fc2e3df343d730f1df4c79f47e0c` |
| Final-candidate stock WASM | `195114c7bd12758dc5b55060381c48782fe4e370a26a7c79883d6eb921490a64` |
| v14 registration | `ef1b2d7204ddc656724a744cc931ec41ebb88b2d5e3b62fd77ae236a1341cc54` |
| v14 cohort | `92e887a732250d62c19c9d6ea441e8e933c5741bfab6380e1cb5b3d87e496dcd` |
| v14 320-cell plan | `cc4d014bb4999289f2194d6e7238d633f4d47d026f95131d46e05f766603d20f` |

The v13 exact-regression plan was frozen as
`9cee5012e9168b37a79a8c29af0abbcbacf56983e4e5d8342288ebe0a4696083`.
All three cells verified canonically and remained cohort-eligible; their worst
home-carrier non-progress run was 20 ticks and their worst same-tile carrier
run was 8 ticks.

The complete v13 diagnostic was intentionally retained even though its frozen
dominance gates rejected promotion (`dominance-read.json` SHA-256
`2178871a8390534c4e37edf625787b3fa63f0030074154241162dbda1f87525d`).
It verified 320/320 canonical and eligible WASM matches with zero faults and
zero draws; all 32 sheets won, and the leaders held 6.25% each of decided
wins. The rejection was substantive: Deliberate-heavy compositions won 26.7%
against 55.9% for the rest, and the five already-strong sheets that happened
to field Minesmith produced an 80% correlated class read. The latter is
disclosed as confounded rather than treated as authority to nerf a class.

The v14 targeted plan was frozen before its outcomes as
`5dc305d6cbab5ffbdfeeae616a2a493f3775f5a9c2f786c63a91b83fa912739a`.
It covered both participant assignments for six Deliberate-heavy comparisons
plus the retained carrier regressions. All 14 matches verified canonically and
remained cohort-eligible. In the ten high-Deliberate versus non-high sides,
the high side moved from 3 wins under v13 to 4 under v14; this was only a
smoke signal, not a balance read, so the unchanged 320-cell gate was rerun.

### v14 dominance read

The unchanged 320-cell native-sheet population ran entirely on the final WASM
artifact. All 320 canonical replays verified, all matches remained eligible,
no runtime fault or draw occurred, and all 32 sheets won at least twice. The
leader, `sensor-grid`, held 18 of 320 decided wins (5.625%). Every frozen hard
gate passed; the final read is
`06f63fdae757692782a4fc61d14190a257b443bd0bb0f910f697ee39638db31a`.

| Exposure / behavior | Final measurement | Frozen gate |
| --- | ---: | ---: |
| Deliberate-heavy win-rate delta | 18.814 points | at most 20 points |
| Standard-heavy win-rate delta | 2.909 points | at most 20 points |
| Swift-heavy win-rate delta | 8.000 points | at most 20 points |
| Deliberate rotation share | 36.072% | at most 40% |
| Swift strafe share of Swift movement | 4.465% | at least 2% |
| Swift turn-with-move share | 95.535% | at least 20% |
| Highest class-correlated win rate | Minesmith, 68.0% | at most 75% |
| Highest combat-share / turn-share ratio | Repulsor, 1.441 | at most 2.5 |

These are implementation-dominance gates over a shared stock mind and a
registered sheet population. They do not establish causal class balance.

### Ten live coordinated operations

The evaluation-grade operation corpus then ran ten authoritative WASM matches
against the exact same `195114…` artifact. All ten showed causal preparation,
one committed branch, mission success, bounded release, and surviving
participants returning to their ordinary baseline role tags. Signature-bound
proofs observed `tractor-hook`, `smoke-canister`, `hardlight-block`, `arc-toss`,
and `exchange` inside the successful activation. The compact receipt is
`5ae64cc8972a298d8ed657802617541db46063b2d3cc48a91a764f3cf4790647`.

Relay Catch's receiver is explicitly staged closer to home than its carrier,
which satisfies the stock mind's safe-toss condition. Decoy Switch's emergency
abort checks the actual home gate rather than treating ordinary enemy presence
anywhere on the broad home side as an overload. These are operation-sheet
repairs only; the stock artifact, rules, registered population, and v14
dominance evidence did not move.

### Determinism and hosted execution

The historical six-cell golden manifest reran in 38.048 seconds on WASM. All
six canonical hashes remained byte-identical:

- `1661522b6eb3af8f05834f74c6665c69618ca142c5bba4dee26c7b190edd2f0e`
- `b0433312f8f2188435b086bce139eabb9d5618411d12cde53b40584a4a9eafbb`
- `37d7f726b992b606745246a493e93f93d6a0608608f993fde2421645c4dfa27c`
- `28acb15cadb60ecdf2fd0988e794af0c44d69893e5d1374a580031b39d399966`
- `1680c38dea521c2b2951f63bc025c0b28f61836d39625b5b644368ab27987605`
- `cda2fdb628ef71e5a523cd196031c490d2dc4b2d696b8e4f0376af2be50e2b20`

Entrant playlist v4 pins the final artifact hash and its source hashes in the
first-party build receipt. Sheet entrants use the trusted in-process v4 stock
mind; custom minds remain sandboxed WASM. An actual final-artifact WASM match
and the trusted runtime produced the exact same canonical replay hash when
given identical participant provenance. A separate diagnostic CLI run used
truthful runtime-specific provenance; its initial frame, every tick, partial
flag, and terminal result were byte-identical, while the complete replay hashes
correctly differed because replay headers named different runtime kinds and
artifact identities.

### Validation

| Check | Result |
| --- | --- |
| Engine, including DocDrift | 1,365 passed |
| CLI | 127 passed |
| App | 194 passed; 83 PostgreSQL/external-integration tests skipped because no test database or external services were configured |
| SDK / Guest / Determinism / Runtime.Wasm | 84 / 36 / 17 / 67 passed |
| Forward-combat engine contract tests | 8 passed within Engine |
| WASM/trusted full-replay parity | 1 passed within App |
| Operation/sweep Python harnesses | 10 passed |
| Web | 386 passed |
| Web production build | passed; all 16 runtime GLBs verified against the approved Meshy audit |
| Historical golden manifest | 6/6 exact |
| Final v14 cohort | 320/320 verified and eligible; all hard gates passed |
| Ten-operation live proof | 10/10 passed |
| `git diff --check` | passed |

The SDK is version `0.10.12` and the CLI is version `0.9.33`. No mobile
feature work was performed. No DECISIONS number was minted.
