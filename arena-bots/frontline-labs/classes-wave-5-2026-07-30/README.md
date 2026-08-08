# Classes wave 5 (2026-07-30): the deck-game population

The first cohort authored FOR the open game: token `deck` (`sail-open`
without a fabricator; a mirror without stances resolves `crew`) = keel +
the skill kit + universal one-bend + the tuned four-slot/22-rebuild
fabricator (`wane`) + the restored ±45° launch offsets (`aim`) + open
transform placement with the unlimited ratio-floored turret cycle
(`open`), all facing-locked. Authored on CLI 0.9.21; the wave was killed
and relaunched once pre-freeze when the owner folded open ground and the
turret cycle in (#176) — no work lost. **Eight of eight T4 on the first
qualification attempt, zero friction kills.** Two benign isolation
disclosures (shared-directory NAME listings; no content read), both in
the respective DX.md.

| Entrant | Class | `out/bot.wasm` sha256 | Headline |
| --- | --- | --- | --- |
| vector-edge | striker | `9912013a033fead7cda342362e3137e18fab71d22727270a05c5675e35115415` | INVERTED bulwark-vs-striker 0-20 → 20-0 by breach: sight-band standoff + diagonal apertures |
| still-water | striker | `dd2b878bd7595418230d805350332a729a118d7b7694db6777a274b9cda48547` | 124 reachable tiles/facing vs wave-4's 52, brute-force verified; re-priced casts on the point 95× |
| arc-light | striker | `da7c4907846d0ddf6d453d1a28c37434a803a99ca93f7efe0b3dd931c26f5553` | fixed its broken leg: fabricator cell 3-13 → 15-1; every cast fired, zero bodies lost in stance |
| iron-root | bulwark | `9f5a7ae3cccc8d5188e7bf6636b974e3f3d1e3f0229c71eb5dafb7a4478f633b` | "against poke raise the arc, against numbers root the gun"; route-placement worth +85.6/cell |
| march-wall | bulwark | `d4e5e7899aff020fe4a0b7aabb491490efbb231b3fee459e64f2a72237311408` | 16-0 (+189); shell held the contested point 3,064 ticks; break budget fired 240× |
| gate-stone | bulwark | `bf975c47588b00984c79fd6be27c7c87deacc2463dc59d2480c25d53e606608d` | turret as a lease on the point itself; shell body-ticks 37% → 2.2% with more kills |
| spark-line | fabricator | `e725e2e5dadb75cbd034f040e27f574a483dd5e169036bd95c17f3f749ac169d` | PoseScore (whole-envelope pricing): class mirror 24/24 breaches; refuses the turret bargain 13,941× |
| ledger-fly | fabricator | `12165ad4ba9f157ff121e76f1632dbcbcf826ac19156da51e7ed1c789b4c1306` | tick-denominated exchange rate on the 22/30 clocks; honest +1 net with side-bias disclosure |

Every wave-4 predecessor was rebuilt from source for sparring; several
authors note the frozen wave-4 artifacts did NOT fault on the new
contracts (the pre-flight gate's warning was conservative) — rebuilds
were used regardless.

## Converged findings

1. **Placement legality lives on the ROUTE; the map keeps its tags.**
   Four authors independently, the wave's largest single effect
   (+85.6/cell, iron-root): under `open` every route's
   `forbiddenTileTags` is empty while the map still publishes
   `transition-placement-forbidden` on 112 tiles. A doctrine that asks
   the map silently declines a third of the legal board. The rule card
   needs one clause: *tile tags are map data and carry no legality; only
   a route's own lists decide placement.*
2. **The aim arm's content is ray coverage, not a payload option.** One
   facing owns three of the eight rays (52 → 124 reachable tiles;
   +62.6/cell; worth 4 games to a facing-locked chassis) — and bends
   FALL as diagonals rise, because an aim-only diagonal reaches bearings
   that used to cost a bend. A diagonal bearing is launchable from two
   facings (it is the shared aperture boundary) — the tie-break that
   inverted a matchup. No document states the geometry; each author
   re-derived it.
3. **The skills became doctrine.** The shell held contested points
   (3,064 on-point ticks, march-wall) and the break budget finally
   fires in real play (240 breaks); swarm-decline discipline is worth
   +12 wins alone. The volley's value is judgment: arc-light casts 10
   and wins with all of them; vector-edge casts 0 with contract-derived
   gates and wins too — both are doctrine, neither is non-adoption.
   Freed ground did NOT redeem wave-4 cast doctrine (−5.9 until
   re-priced): the tiles were never the whole story.
4. **The turret cycle's value is leaving, not committing** (iron-root
   measured the reverse reading at −47): a rental needs no collateral
   but the weight-zero bargain still prices every tick anchored.
   gate-stone leases the point's own tile; spark-line refuses the
   bargain 13,941 of 13,941 times on weight arithmetic alone. Both are
   the intended judgment call.
5. **Engine discovery (gate-stone): a gunless stance FREEZES the gun's
   cooldown** — cooldown belongs to the attack profile, so time stops
   passing for the gun while shelled. Falsifies wave-4's "idle ticks
   are free" shell pricing; design decision pending (should stance
   ticks advance the mobile gun's clock?).
6. **Determinism makes seeds decorative in self-play**: byte-identical
   outcomes across all 20 seeds (vector-edge), and a paired statistic
   is blind to variance changes (spark-line: two 0.0-scoring variants,
   one inert, one turning grinds into t192 breaches). Sweep tooling
   should print distinct-outcome counts and completion-reason mixes.
7. Fixed during/after the wave (#177): experiment viewers opt-in
   (--viewer/--open), verified replay writes (a full disk had produced
   replays that parsed and lied, +212 territorial), source cap 256 KB →
   2 MB. Remaining tooling asks: `--print-candidate-contract --full`
   (unanimous, third wave), a `nilbots` symlink beside `botarena`,
   inert-flag echo lines, observed-enemy route availability, the
   ratio-floor "skims as a heal" doc line, ObservedSound.Bearing.

## Coarse balance read

See `docs/DECISIONS.md` #178 — run at mains level only (fast-iteration
mode #174), wave-5 vs wave-5, mirrored accounting, distinct-outcome
counts disclosed.
