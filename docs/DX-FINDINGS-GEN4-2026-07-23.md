# DX findings — agent-arena gen-4 bracket (hill v3), 2026-07-23

Two-challenger bracket (Bastille, hill-fortress; Talon, zone-denial) plus
the trial's Castellan as a third doctrine, under `BOTARENA_RULES=hill`
(0.4-exp-hill3), one improvement iteration. Tournament outcome and balance
data live in GAME-DESIGN + DECISIONS #52; trial-phase findings in
DX-FINDINGS-GEN4-TRIAL. New findings from the bracket phase:

## Fixed in the 0.4 ship batch
All six items below were resolved the same day (mirror `-mirror` dir
suffix + slot-tagged verdict lines; runtime-scoped-hash + coordinate +
`--full` doc lines; `--seeds` error tip; zone counters documented as an
intended mechanic in the official v0.4 rules docs). Kept for the record:

1. **[bug, med] Mirror sets overwrite their own replays.** Same-name
   matchups (`set --bot . --opponent out/bot.wasm`) write both orientations
   of a map/seed pair to the same `out/talon-vs-talon-<map>-s<seed>/`
   directory — g2 clobbers g1, contradicting the README's "parallel runs
   never overwrite each other". Both agents hit it; workaround `--out` per
   game. Fix: make the default replay dir slot-aware when the two
   participant slugs collide.
2. **[doc, med] Replay hashes are runtime-scoped and the docs don't say
   so.** In-process and WASM runs of the same match produce identical
   timelines but different hashes (runtime kind + artifact hash are part of
   replay identity — correct by design). Both agents burned time trying to
   use the hash to verify in-process ≡ WASM. One doc sentence: compare
   summaries across runtimes, hashes only within one.
3. **[doc, low] Coordinate system is undocumented** — North = (0,−1), y
   grows southward; visible only in SDK source or by probing. One line in
   the template README fixes it.
4. **[doc, low] `--summary` tick-sampling deserves a warning label**: quiet
   stretches are compressed (movement can look like teleportation in
   4-tick limit cycles); `--full` exists and should be mentioned next to
   the loss-forensics advice (cost one agent two wrong diagnoses).
5. **[polish, low] Same-name set lines read as contradictions**
   (`g1 slot0 LOSS (Talon wins)` in a mirror) and the `--seeds` count error
   could mention that `--maps <subset>` legalizes shorter seed lists.
6. **[design note] Public zone counters are a sensor channel.** All three
   doctrines independently exploited `MyZoneTicks`/`EnemyZoneTicks` deltas
   to infer unseen enemy positions (frozen counter while standing on zone
   proves the enemy is on zone too — decisive on arena-01's mutually
   invisible split pads). Intended? Probably yes (public scoreboard), but
   it should be a documented mechanic, not folklore.

## Worked as advertised (regression-positive)
The rules pin, `Repro:` seed echo, Fuel line, array-newest-first
build-status doc, zone-aware summaries (`*` marks + zone totals), and the
register→submit→parity API flow were all used heavily by both agents with
zero friction reports — every one of them was a prior generation's finding.
