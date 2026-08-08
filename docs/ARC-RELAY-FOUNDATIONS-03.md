# Foundations -03: verdicts and the re-based landscape

**2026-08-05. Goal: ROBUST PLAY FOUNDATIONS (-03) — owner-set, executed
autonomously.** Ruleset `arc-relay-forward-combat-03` minted beside `-02`
(DECISIONS #210); executor symmetrization and micro parity in the tactical
playbook mind (DECISIONS #211). Design memo for the follow-on depth
question: `DESIGN-ARC-RELAY-DEPTH-IDEAS.md`.

## Bar verdicts

| Bar | Verdict | Evidence |
|---|---|---|
| **Variance** — ≥6/8 distinct outcomes across 8 seeds, same-seed byte-identical | **PASS 8/8** | hook-control vs sentinel-zone, seeds 1001–1008: eight distinct (end tick, banked) outcomes, winner split 5–3; seed 1001 replayed byte-identical (commit 616183fe) |
| **Fairness** — mirror matches, no side >6/8 | **PASS at 8 seeds, with one documented residual** | hc mirror 4/8 west, sz mirror 6/8 east, stock baseline 6/8 west (commit d024dc09). Residual: with the hook channel live, the hook-heavy hc mirror shows 13/16 west at 16 seeds — real, mechanism unidentified after auditing every structural engine candidate (see below) |
| **Micro** — ≥10 hooks/game vs stock (was 0–2); zero avoidable mine deaths | **PASS** | 51 and 55 hook casts (stock: 49/56) via the opportunistic heading channel (54348626); 66 mines faced across two games vs mineline, zero minesmith-sourced deaths |
| **Discipline** — zero faults, tests green, -01/-02 byte-stable | **PASS** | 150/150 landscape games completed without fault; 1373 engine + 56 tactical primitive tests green; -01/-02 fingerprints pinned by `ArcRelayFoundations3Tests` |
| **Re-base** — full pool round-robin ≥5 seeds × both orientations | **DONE** | table below; the Meta Proof resumes from it |

**WASM note.** The parity spot-check found tactical-mind games are
deterministic within each runtime but not byte-identical across
in-process and WASM (same winner in the checked game). The repo's
byte-parity guarantee covers the builtin guest (WASM contract tests);
tactical evidence stays WASM-only per the standing house rule. This is
now measured rather than assumed.

## What changed and why

Single-game determinism had made every matchup a solved position: a
one-tile gate-rect change flipped winners, and "X counters Y" meant
winning exactly two fixed games. Three foundations landed together:

1. **Seed variance** — scheduled well births shift within a seed-derived
   ±6-tick window. Every seed is now a different game; evaluation is a
   distribution, not a replay.
2. **Side fairness** — engine: the order-dependent resolution slices
   (contested projectile consumption, advancement, same-target hook
   pulls) alternate by tick parity, and sentinel target ties break toward
   the shooter's own reactor (all gated on the -03 flag). Executor: every
   tie-break routed through the team's canonical frame — absolute
   lowest-Y/X and compass-enum preferences picked opposite relative tiles
   for the two sides.
3. **Micro parity** — hooks and rails fire opportunistically at
   ray-aligned hostiles (a dedicated arbitration channel, carriers
   first); pathing avoids visible armed mines. Forced movement still
   lands on mines, and unseen mines still trip — mines are terrain
   control now, not a lottery.

## The fairness residual, precisely

With hooks at stock parity, the hook-control mirror skews 13/16 west
(p≈0.01 under a fair coin). The opening exchanges carry it: east loses
first-150-tick deaths roughly 2–3× across seeds, and everything
compounds from there (west out-casts hooks 690–438 across the 16 games —
a survivor-count effect, not the cause). Audited and cleared: the
movement grid (simultaneous, contested tiles block all claimants),
deferred damage (deaths simultaneous), contact consumption, advancement
order, pull order (all parity-alternated), the cast-decision chain
(frame-keyed end to end). The sz and baseline mirrors sit inside the bar,
so the mechanism is specific to hook interactions. Open item for the
Meta Proof phase; margins there should be read with this in mind.

## The re-based landscape

150 games: 5 archetypes + parity baseline, seeds 5001–5005, both
orientations, `forward-combat-3`, in-process (analysis-grade). Read
per-matchup rows as wins out of 10.

| matchup | result |
|---|---|
| sentinel-zone vs baseline | **9–1** |
| sentinel-zone vs home-siege | **9–1** |
| sentinel-zone vs tempo-race | **9–1** |
| sentinel-zone vs hook-control | **6–4** |
| sentinel-zone vs breakwater | **5–5** |
| home-siege vs hook-control | **9–1** |
| home-siege vs tempo-race | **10–0** |
| home-siege vs baseline | **2–8** |
| home-siege vs breakwater | **2–8** |
| hook-control vs breakwater | **9–1** |
| hook-control vs baseline | **7–3** |
| hook-control vs tempo-race | **7–3** |
| breakwater vs baseline | **6–4** |
| breakwater vs tempo-race | **7–3** |
| tempo-race vs baseline | **6–4** |

| entrant | rate | band [35,65] |
|---|---|---|
| sentinel-zone | 76% | OUT (high) |
| hook-control | 56% | OK |
| breakwater | 54% | OK |
| home-siege | 48% | OK |
| baseline | 40% | exempt |
| tempo-race | 26% | OUT (low) |

**Reading.** A clean counter cycle survived the move to distributions —
**home-siege beats hook-control (9–1), hook-control beats breakwater
(9–1), breakwater beats home-siege (8–2)** — and it is a *different*
cycle than the single-game era produced, which is itself evidence the
old one was position-specific. Sentinel-zone is the problem child again
(76%, only breakwater holds it even at 5–5): its single-game "counter"
(hook-control's exact gate rect) did not survive variance, which is
exactly the brittleness the foundations were built to expose.
Tempo-race is under band and home-siege lost its baseline edge under
hooks-at-parity — both need re-tuning, now against distributions.

## What the Meta Proof inherits

- Bars restate over seed distributions (e.g. "beats baseline ≥7/10 both
  orientations pooled" rather than two fixed games).
- Immediate targets: a sentinel-zone counter that holds across seeds
  (breakwater's 5–5 is the thread to pull), tempo-race and home-siege
  re-tuning, and the hc-mirror residual noted above.
- Zero-fault status and the frozen -01/-02 lines carry forward
  untouched.
