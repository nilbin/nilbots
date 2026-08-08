# Classes wave 1 — revision 3 (2026-07-29): the pendulum doctrine round

The phase-1 population: each lineage revised once, in isolation, for the
pendulum counterweight arms (DECISIONS #158/#160 — sticky-frontline,
forward-rally, contest-majority, enemy-sole-decay), measured on
`--movement facing-locked` (#159). Briefs excluded the skills arms
(those belong to phase 2, #165). Budget matched the r2 precedent: one
strategic revision, free mechanical repairs, own replays and own
rebuilt predecessor only. The §1.9 pre-flight gate ran green before
commissioning and the §2.6 friction watch ran throughout: **six of six
retained T4, zero friction kills** — the first wave with no shared-trap
casualty.

| Entrant | Class | `out/bot.wasm` sha256 | Headline vs rebuilt r2 |
| --- | --- | --- | --- |
| vector-edge | striker | `598ce1c1bc9d15f45a885bc0b4f6cc5619771459e7e0844bb91aa0617d3fa55c` | byte-identical on inert arms; 9–3 ratchet |
| still-water | striker | `fcf6b4a6bf38454bf995d3efb0cd896c71e34d7757e685eb487b1160388e0662` | 28–12–0 overall; sticky arms side-saturated (see below) |
| iron-root | bulwark | `00ede717dacf60eb8e778134cc12145a648c057b69fb570b99340c5bf22f7090` | tick-identical on inert arms; +14.6 ratchet-contest |
| march-wall | bulwark | `3a0d079b10908a639354f3674cf449b104b212f4d8b95a205d97af185d1021f9` | 16–0–0 (+140) ratchet; 15–1–0 (+154) contest |
| spark-line | fabricator | `5b38ee1cfd0f88f16ab58d3ab7620522652c0b9aa6f852f013a53908ff5b8a50` | honest null on class arm; +21.8/seed contest on base |
| ledger-fly | fabricator | `83db091374e7ca7b714b731546efaf8e1d27866c1d79638620236e19e1b11b8c` | 24–0–0 on both sticky arms; byte-identical elsewhere |

Every entrant proved doctrine gating the same way: decision streams
byte- or tick-identical to its rebuilt predecessor on contracts that
declare none of the pendulum policies. All sparring used predecessors
REBUILT from source — frozen r2 artifacts fault at tick 0 on sticky
arms (the `ratchetHoldTicks` canonical field; the #156 additive-pattern
consequence).

## Consensus findings (independent, converged)

- **The hold's owner must become observable.** Five of six authors
  independently derived the hold window from
  `ControlResumesAtTick − RedeployPauseTicks` (exact, published,
  documented nowhere) and independently failed on ownership for lives
  born mid-hold — which forward-rally manufactures constantly. The
  minimal fixes proposed: a nullable `holdingTeamId` /
  `holdRemainingTicks` on the mode observation, or `advancingTeamId`
  on the ModeChanged payload. Scheduled for the batched phase-2 SDK
  bump (see the Codex handover brief).
- **`--print-candidate-contract` prints identity, not policy values**
  (3 votes) — authors ran throwaway matches to read `gameMode.capture`
  out of replay headers.
- **Two silent identity traps**: a `.wasm` spec drops the declared
  class (resolves the base contract, no warning; 3 votes, two authors
  measured whole matrices on the wrong game before catching it), and
  `--swap` exchanges artifacts but not participant IDs (accounting keyed
  on participant ID inverts).
- **In-process vs WASM behavioral divergence** (spark-line): identical
  bots deadlocked in-process while producing 62 advances per side under
  WASM. Existing doctrine already says frozen outcomes are WASM-only;
  this elevates it to "never A/B in-process" and files a parity
  investigation.
- **contest-majority × zero-weight forms inverts anchor value**
  (iron-root, found by ablation at −21 territory): under weight-scaled
  control a second body is capture pressure, under binary control it is
  free suppression. Needs a class-addendum sentence before phase 2.
- **Suspected engine-side side bias on ratchet arms** (still-water): an
  identical-bot striker mirror on ratchet swept 5–0 for team 1,
  breaching at tick 167 on every seed. Suspect: forward-rally placement
  order is canonical row/column — an absolute order on a mirrored map,
  the same defect class OrderedDirections fixed bot-side. Under
  investigation before the phase-1 pre-registration; both-sides
  accounting cancels it for entrant comparisons either way.
- **`balanceEvidenceEligible` is JSON-only** (march-wall misread its own
  r2) — print it beside the tier. The ~17× qualify speedup between
  passes is the WASM module cache warming, not a suite change; say so
  in output.

Doctrine convergence worth recording: four lineages independently
arrived at the same core repricing — *stop paying capture-tempo for
ground that cannot move the front* — from four different class
perspectives (economy, duelist, fortress, swarm). The arms appear to
have one deep strategic truth with class-specific expressions, which is
what a good structural mechanic should look like.
