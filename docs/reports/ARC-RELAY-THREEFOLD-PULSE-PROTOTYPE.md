# Threefold Pulse prototype — evaluation report

**2026-08-05. Recommendation: REVISE.** The mechanic's strategic-texture
and legibility gains are real and demonstrated; its primary stated
purpose — substantially more of each team doing direct objective work —
is **not differentiated from the -03 baseline** on the pre-registered
measure, and two structural gaps need design attention before adoption.
Owner brief: `docs/briefs/THREEFOLD-PULSE-PROTOTYPE-BRIEF.md` (with the
in-session clean-slate amendment). Pre-registered measure:
`docs/briefs/THREEFOLD-CONTRIBUTION-MEASURE.md`, frozen before any
evaluation game existed.

## Exact implemented rules (`arc-relay-threefold-01`)

Profile `threefold-pulse`, minted beside `-03` and never promoted; keeps
the -03 foundations (grammar 2, ±6-tick seed-jittered well births,
parity-alternating resolution). On top:

1. Every Core's origin is its Well (north / centre / south). Each
   reactor has three matching sockets.
2. Banking fills the matching socket only; the `reactor-charge` channel
   is the filled-socket count (0–3, unchanged range).
3. A Core whose origin socket is already filled **cannot be consumed**:
   the carrier keeps it, physical and contestable. No new event fires.
4. The third distinct origin triggers the existing Pulse; sockets reset.
   Sockets cannot be stolen (no sabotage was added).
5. **The one strictly-necessitated lifecycle change (brief clause 7):**
   a Well's outstanding gate clears on *pickup*, not on bank. Without
   this, any held duplicate froze its Well for the rest of the match
   (measured 443–477 blocked ticks of 600; six total banks; the east
   side starved outright). Loose Cores still block their Well; denial
   still denies the specific Core. Historical rulesets keep bank-cleared
   behavior byte-for-byte.
6. Wells must number exactly three under the flag (validated).

## Observation / replay / viewer changes

- `ArcRelayReactorState` publishes `FilledSocketWellIds` (canonical well
  order) — engine + SDK records with hand-written sequence equality; the
  runtime mapper carries it; the NBV2 observation codec adds it as
  tagged optional field 5 emitted only when non-empty; replay v3 writes
  `filledSocketWellIds` only under threefold; the web wire mirror and
  strict normalizer accept it conditionally. Prior rulesets' observation,
  replay, and rules bytes are untouched.
- Sheet grammar: new condition facts `own-socket-filled` /
  `enemy-socket-filled` (subject = absolute well id, like custody
  `sourceWells`), `own-filled-sockets` / `enemy-filled-sockets`, and
  `well-ticks-until-birth` (the jittered schedule is public). Compiler
  whitelist extended accordingly.
- Viewer (owner ruling): **the lane owns the sphere** — Cores render in
  origin hues (north `#5ea0ff`, centre `#7fe8d8`, south `#ffb45e`)
  loose, carried, and in flight, in both renderers; reactor charge pips
  light positionally in the same hues whenever sockets are published
  (identical to legacy rendering at zero charge). One presentation test
  updated to pin the ruling.
- Stock mind: needed-origin targeting, duplicate staging → replaced by
  the degeneracy repair below.

## Degeneracy-bar results (frozen v4 bars, never adjusted)

The bars caught a real structural conflict: **a held unbankable Core has
no legal disposal** — staging trips `homeCarrierNonProgress`, parking
trips `stuckCarrier` — so initial threefold mirrors were
cohort-ineligible on *both* teams. Repair (behavioral, per the brief):
the stock paths **around** loose duplicate-origin Cores exactly as it
avoids mines, guarding denial-valuable ones by presence instead of
possession. Post-repair: **23/24 team-slots eligible across 12 mirrors**
(one residual accidental-pickup trip, disclosed). All other detectors
(formation freeze, ping-pong, pickup-drop cycles, passivity, stuck
carrier) clean; no missing-origin deadlocks observed post-lifecycle-fix.

**Revision item 1:** rule 4 needs a disposal/stash affordance (e.g. a
voluntary drop or a reactor-side cache) for deliberate denial-holding to
be bar-legal. Today the only legal denial is guarding without pickup.

## Body-participation evidence (pre-registered measure)

Screening-grade (in-process), stock mirrors, eligible teams only; audit
script `scripts/arc-relay-threefold-contribution-audit.py` reads replay
facts only.

| arm | cycles | total median | ≥6 share | economic median |
|---|---|---|---|---|
| **Threefold** (12 mirrors, seeds 8101–8112) | 52 | **7 / 8** | 49/52 | **4** |
| **-03 control** (8 mirrors, seeds 2001–2008) | 35 | **7 / 8** | 30/35 | **4** |

The pre-registered target (median ≥6) **passes decisively under
Threefold — and the control matches it**. Total participation was
already high pre-Threefold (combat-proximity predicates fire broadly in
brawly stock play), and the economic core (carry/bank/pickup/denial) is
~4 bodies in both. Threefold-only signatures: DENIAL-HOLD fires
(structurally impossible under -03) and the distribution is slightly
right-shifted — but the honest headline is **no substantial
participation gain**. WASM authoritative spot-run (seed 8101): eligible,
cycle counts identical to the in-process twin (8/7/8/6/6).

## Strategy audit (clean-slate, one at a time, per amendment)

Strategy #1, `trifold-balanced-v1`, eight drafts against the competent
Threefold stock baseline (bar ≥7/10; 5 seeds × both orientations):

| draft | shape | result |
|---|---|---|
| 1 | balanced 2-2-2 lanes | 1/10 — west collapse, all deaths at the well line |
| 2 | fighting-spine comp | 0/10 — halved couriers, reverted |
| — | hook-control probe | 0/10 — **cannot complete a cycle** |
| 3 | socket-phase mass rotation | 0/10 but close (1–2p vs 3p) |
| 4 | + shadow couriers | 0/10 — thinned the mass, reverted |
| 5–6 | dry-lane skip → anticipating rotation (`well-ticks-until-birth`) | 0/10 — west 2-3p consistently |
| 7 | spread + two-socket endgame surge | 0/10 — 0-1p |
| 8 | arrival-tuning | inert |

**Key structural finding:** every pre-Threefold sheet is a two-lane
design and none can even Pulse under Threefold (hook-control never
completed a cycle). The mechanic forces genuinely new strategy shapes.

**The 3-3-2 question:** the strongest known strategy is the stock's
*distributed parallel coverage with socket-adaptive prioritization* —
it defeated every set-piece alternative authored (mass rotation,
overload-surge, hybrids). This is not literally a fixed 3-3-2 (the
stock adapts to socket state), but the evidence **leans toward the
brief's failure mode**: one allocation family dominates, because
distributed couriers always beat a formation to a public, staggered,
jittered schedule. Disclosed as a depth concern rather than a verdict:
only one sheet family was exhausted, and the sheet machinery's formation
bias (cohesion, task cadence) may cap set-piece play below its true
ceiling. Checklist audit: balanced ✓ (draft 1), overload-rotate ✓
(drafts 3–6), socket-state prioritization ✓ (stock + drafts), protect-
two-contest-one ✓ (draft 7, ineffective), denial ✓ (presence-guarding,
after the repair), interception / feint / overload-counter — not
reached; nothing found the grammar could not *express* (gap fixed en
route: phase transitions to the same target shared one streak counter;
plus the two new fact families above).

## Side / seed / class / composition checks

Mirrored orientations throughout; the known -03 side textures persist
under Threefold (draft 1 converted 9/9 as east and 2/9 as west in the
same matchup). No class or composition explains results alone: the
stock's dominance held across the parity composition and every tactical
comp tried. Seeds vary outcomes genuinely (variance foundation); all
multi-seed records are reported per side above and in the ledgers
(commits 067c4a2f → 210230d1).

## Canonical-hash proof and suites

`ArcRelayThreefoldTests` pins: the threefold rules fingerprint is
distinct; `-01`/`-02`/`-03` fingerprints re-derive byte-identically
beside it; `threefoldSockets` appears canonically only in the threefold
document. Suites at report time: engine **1377/1377 pass, 44 s**
(includes DocDrift and replay verification suites); web **404/404
pass** (~25 s); tactical-mind and stock-mind projects build clean; both
WASM artifacts rebuilt (tactical `42288a91…`, stock `bf3d147b…`).
In-process runs are labeled screening; WASM runs authoritative.

## Gallery

Outcome-visible, curated, current 3D presentation, served at the
standing review URL:
**https://trucks-therapist-bloomberg-elephant.trycloudflare.com**

1. **Authoritative WASM mirror** (seed 8101) — full cycles, lane-hued
   spheres and sockets; participation 8/7/8/6/6.
2. **Eligible mirror, denial by presence** (8103) — duplicates skirted,
   not carried; zero bars tripped.
3. **Eligible mirror, out-of-order completion** (8104) — the any-order
   rule visible on the pips.
4. **Strategy 1 draft 6 loss** (w-9004, 2–3p) — the socket-phase mass
   rotation and why distributed couriers beat it.
5. **Strategy 1 draft 1 win** (e-9001, 3–1) — the balanced sheet's
   perfect-custody east game.

## Recommendation: REVISE

Adopt-worthy: the socket structure's legibility (three hues, readable
counterplay), the extinction of two-lane solutions, denial as an
emergent role, and a mechanic with zero numeric knobs that changed
strategy shape completely. Not adopt-worthy yet: (1) the primary
participation target is met but **not differentiated from -03** — the
mechanic does not measurably spread objective work; (2) duplicates need
a disposal affordance to make denial bar-legal; (3) distributed-parallel
play looks dominant, and a revision should give set-piece or asymmetric
allocations a reason to exist (the depth memo's Ripening is the natural
single-mechanic follow-up candidate — value concentration is precisely
what mass play needs and distributed play fears). Rejecting outright
would discard real, cheap legibility and structure gains; adopting as-is
would enshrine a mechanic that misses its stated purpose.
