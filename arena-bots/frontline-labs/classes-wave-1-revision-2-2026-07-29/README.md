# Classes wave 1 — revision 2 (2026-07-29)

The budgeted loss-forensics pass over the frozen wave-1 population. Each
entrant received an isolated brief: its own wave-1 factorial replays only,
one strategic revision, free mechanical/contract repairs, and the new
movement-coupling capability documented in
`docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`. Authors never saw another
entrant's source, standings, or the aggregate balance report. Predecessor
sources stay untouched in `classes-wave-1-2026-07-29/`.

**Outcome: six of six retained or reached cumulative T4**
(`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`,
exit 0, `balanceEvidenceEligible: true`), including both redemptions
(spark-line from T2, still-water from qualification-blocked). The
population can now clear the T4 voting floor for the first time.

| Entrant | Class | Doctrine (revision 2) | `out/bot.wasm` sha256 |
| --- | --- | --- | --- |
| vector-edge | striker | Pressure duelist — measured dodge ledger replaces the assumed dodge rate | `36cadf4bac048b1f6566b65961bfd4528f07f9bb6367c11d91327d5e66e01493` |
| still-water | striker | Patient interceptor — retained; contract repairs and coupled-arm adaptation | `b0fe1f36708df1c62a117dfe57f8506c6d59abea01770e2c78618d2a6712e289` |
| march-wall | bulwark | THE LANE IS THE WALL — anchors only at won chokes with relief priced in | `c0e24671a241ce77ff81df12550d304d93eacd17825ec1cbd87384d0b9ae51ac` |
| iron-root | bulwark | TENURED ROOT — roots only with relief physically in place; return is a rotation, not a reflex | `793c4f2e3406c5ea29efdc5b8f4f1ff6830449be4042c7bc52baa589bca4841c` |
| spark-line | fabricator | Tempo engine — striker column repaired (sensor problem, not duel problem) | `8bb386542d4ef3b203e2885fb643e6cf29cb4f3b4241ea98987050b1e8985290` |
| ledger-fly | fabricator | Attrition banker — fabrication priced against declared enemy slot capacity | `81b7a91704cccdea864f84494bf85690084791c17bfeddfbc5f47942779a986c` |

Headline validations against each lineage's own frozen v1 (details and
caveats in each entrant's `DX.md`): iron-root 9–18–0 → 14–9–4 with 9–0 vs
frozen v1 across all three movement arms; vector-edge 31W/13L/4D over 48
matches; ledger-fly 8–0 with breach at t181 on its repaired cell;
march-wall turned nine cross-class breach losses into no-breach or
breach-for; spark-line's 0-12-0 striker column traced to vision range,
not doctrine.

## Cross-cutting findings

- **The mask-as-search-space trap.** Three agents independently found that
  seeding route search from the movement legality mask freezes a bot under
  `facing-locked` (the mask offers only the current facing). The shipped
  starter laid this trap; fixed in the template after the wave
  (`TryAdvanceToActiveObjective` now plans on map geometry and emits the
  unlocking rotation — commit `2dfb909`).
- **Turrets were wasted, not punished** (iron-root). Fortification losses
  traced to reflexive same-tick returns burning the irreversible mobilize,
  not to opponents breaking the fortress.
- **Absolute direction preferences are a systematic side bias** on
  mirror-symmetric maps. All revisions adopted the mirror-fair
  `OrderedDirections` (advance-first, randomized perpendicular tie-break).
- **Allied-bolt pass-through must be read, not assumed** (vector-edge):
  modeling own projectiles as obstacles produced three-tick stalls on the
  one lane that matters.
- **Deterministic bots can collapse a seed sweep to one sample**
  (vector-edge): wave-1 cells produced byte-identical replays across all
  three seeds. Bots that consume `context.Random` (e.g. via
  `OrderedDirections`) restore seed variance; cohort reports should
  disclose distinct-replay-hash counts per cell.

## Disclosures

- **ledger-fly** (DX.md): its empty-gun footwork rung is classified as a
  mechanical repair, but "a reviewer who counts the footwork rung as a
  second strategic revision would not be wrong." Recorded here so the
  budget asymmetry, if one is later judged to exist, is pre-disclosed.
- **vector-edge** (DX.md): a forensics glob matched the opponent's name
  and printed one aggregate outcome row for each of two script runs over
  36 non-assigned replays before being hard-filtered; everything was
  re-run and no conclusion depends on those rows. No per-tick behaviour,
  source, or standings was read.

## Known limitations for the next factorial

- **Coupled-arm doctrine is self-play-validated only.** Qualification runs
  the duel-depth union profile with no facing coupling, and `--movement`
  does not combine with the qualification path, so no entrant has faced a
  foreign doctrine under a coupled arm yet. The classes × movement × maps
  factorial is the first such measurement — treat coupled-arm cells as
  first-contact evidence.
- **Frozen wave-1 v1 artifacts cannot run the coupled arms.** The v1
  canonical contract reader validates an exact property set, so the added
  `facingCoupling` field is fatal to them ("exited before its life
  ended"). Coupled-arm baselines must come from these v2 artifacts.
