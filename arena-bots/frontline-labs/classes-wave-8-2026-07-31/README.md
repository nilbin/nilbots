# Classes wave 8 (2026-07-31): the channel game round

The "proper round for all" the owner commissioned before going AFK:
every lineage re-authored on the NEW game — the capture channel
(#187, `siege`), the SCRAP economy (#187, `forge`/`bastion`), and
TeamRandom (#185) — on CLI 0.9.27 / SDK 0.10.10. **Eight of eight T4
on first attempts, every freeze byte-reproducing.** Two engine defects
were found by authors mid-wave, fixed, republished, and every affected
measurement re-run (see Findings 2).

| Entrant | class | `out/bot.wasm` sha256 | headline vs predecessor |
| --- | --- | --- | --- |
| vector-edge | striker | `c56ab6ba16cbbfda…` | every armed cell +16W (wave-7-self mirror stalemate → breach); swell 128/128 identical |
| still-water | striker | `37cf30e5d6f1ebde…` | first bulwark wins in the lineage (+16.00 both cells); honest fabricator regression |
| arc-light | striker | `0252d32c2ad2270c…` | interrupts 2.8 → 11.2/match vs iron-root; territory up on 4 legs, records move on none |
| iron-root | bulwark | `ba7b5adb4a505353…` | bastion mirror 16-0-0 +14.81 (was 0-0-16); REFUTED the turret-denial prediction with numbers |
| march-wall | bulwark | `1b3cff136ce3336e…` | siege 16-0-0 +120 vs wave-6 self; found the second engine abort with a minimal repro |
| gate-stone | bulwark | `b1da63b710872d38…` | channel ledger measured worse and shipped DISABLED with its number; turret-relief pricing +100 |
| spark-line | fabricator | `55ca1c3b339724e9…` | fixed the commissioned failure (t52 breach gone), flipped march-wall +86; DX states "did not net-improve" |
| ledger-fly | fabricator | `b4de0047ef9870e6…` | 23W-1L-0D vs wave-6 self across all four cells |

## The 2×2 balance read (the wave's purpose)

21 class pairings × 3 seeds × 4 arms = 252 matches, zero aborts, every
cell's replay count verified (Finding 2 explains why counting replays
is mandatory). Payoff toward the first-named class:

| pair | swell (neither) | siege (channel) | forge (scrap) | bastion (both) |
| --- | --- | --- | --- | --- |
| bulwark-vs-striker | +0.667 | +0.556 | +0.630 | **+0.778** |
| bulwark-vs-fabricator | +0.444 | +0.500 | +0.667 | **+0.667** |
| fabricator-vs-striker | −0.222 | −0.222 | −0.222 | −0.111 |

Attribution (arm minus swell; interaction = bastion − siege − forge +
swell): the channel alone moves bvs **−0.111** (the #187 striker-role
prediction's direction, at a fraction of its magnitude — the
prediction was made against wave-7 bulwark doctrine, and the wave-8
bulwarks spent their free repair budget adapting to salvo strikers);
scrap alone moves bvf **+0.223** (the economy favors the class that
survives to collect); the bvs interaction is **+0.259** toward the
bulwark (channel + economy together suit the bulwark best: its shell
channels, its turret denies, its prime screens, and its idle ticks
fund the store).

**Verdict: the #184 triangle did not survive two waves of bulwark
catch-up — the bulwark is now the top class, and the full game
(bastion) is out of band on both its legs (bvs +0.778, bvf +0.667)
while fabricator-vs-striker stays in band everywhere.** The #187
floor tripwire (any pair below +0.15) fired in the OPPOSITE direction
from the prediction: the bulwark was never squeezed — it was crowned.
Note the asymmetric-freshness caveat cuts differently this wave:
every lineage is equally fresh, so this is the first symmetric read
of the campaign. The next balance levers on the record: economy
pricing/scope (the scrap arm is a bulwark amplifier as priced),
channel-speed, and the pre-registered class-numbers factors.

## Converged findings

1. **The stillness doctrine converged from every direction** (eight
   authors, eight phrasings): "a step off the point is a thrown-away
   tick", "stillness is a purchase", "freeze when the claim is yours,
   kite when it is theirs", "the tick you spend standing still is the
   most expensive tick this doctrine has ever had to price". The
   channel game is legible and its escort pattern is the intended play
   — measured, not asserted (screens absorb for free; every author's
   interrupt/escort rules carried their attribution tables).
2. **Two engine aborts, found by contract-driven bots, fixed
   mid-wave**: (a) the edge tier's retained-projectile re-trace used
   the raw profile (fixed: infer launch-time extra from the bolt's
   conserved reach); (b) guard returns carried the deflector's edge
   tier against the validator's fresh-budget rule (fixed: parries fly
   the raw budget — the tier buys the mobile gun, not the parry).
   Both were reachable ONLY by bots that read their legality masks —
   the population is now the engine's best fuzzer. Operational lesson
   made law: some abort paths exit 0 with the error on stdout, so
   harnesses must count replay files, never trust return codes.
3. **The owner's turret prediction was refuted twice, independently**:
   a body's unconditional denial weight beats the turret's conditional
   revert (iron-root: rooting for denial measured −14.5/cell), and the
   turret's real worth is priced on the mobile body it would otherwise
   be (gate-stone's turret-relief rule, +100). The turret's channel
   role is real but different than designed: it fires interrupts and
   casts invest from safety.
4. **TeamRandom's first doctrine verdict is null-to-negative**: three
   authors measured the scaffold tie-break at zero, one at −2.15
   (shipped off), one at +4.7 (team-shared direction order, the one
   positive). The capability is sound and its docs praised; no
   doctrine has yet found where coordinated unpredictability pays.
   Honest first-wave result, not a failure.
5. **The economy is played assay-first**: most of the cohort banks at
   the tile and refuses the carry (courier interceptions across the
   read: ~zero); invests run 0-2/match with optic favored and edge
   rarely reached. The 48-scrap pot funds 1-2 tiers in practice, not
   3. The invest action is live and legal-mask-driven (zero blocked
   casts except the documented same-tick race), but the deep-carry
   game the memo designed is mostly unbought — vein pricing and carry
   incentives are tuning levers with headroom.
6. **Platform asks, ranked by unanimity**: publish `movedThisTick` (or
   the claim/denial pair) — the channel's central fact is derivable
   only from life-scoped memory a newborn lacks (8/8 authors);
   `ArenaBasics.Capture` mis-reads the channel policy as binary
   control (every author hand-wrote the same 20-line replacement);
   abort paths that exit 0; `carriedScrap` absent from `activeLives`
   in replay analysis; the invest argument-domain convention differs
   from `transform`'s; enemies do not publish `routeCooldowns` though
   the salvo section implies they do.
