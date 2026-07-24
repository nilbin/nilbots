# Territorial-v8 native holdout and DX findings — 2026-07-24

Four isolated authors built Pincer, Comet, Augur, and Echo from the player
docs, public SDK, experiment brief, and CLI only. Each received the same
training artifacts and one bounded improvement opportunity. Final evidence
used canonical WASM with zero faults.

## Product result

Territorial v8 passes the activity and outcome-blind viewer study, but remains
on strict HOLD because one pre-registered diversity gate failed.

| native v8 gate | threshold | result |
| --- | ---: | ---: |
| draws | ≤10% | 1.9% |
| all four doctrines win | 4 | 4 |
| leading share of decided wins | ≤35% | **Pincer 42.5% — FAIL** |
| damage games | ≥75% | 100% |
| reciprocal damage | ≥40% | 78.7% |
| multiple damage ticks | ≥60% | 100% |
| active-world ticks | ≥75% | 100% |
| stalled games | ≤5% | 0% |
| repeated-frame loop games | ≤10% | 0% |
| median action entropy | ≥0.60 | 0.728 |
| non-Wait sole-score ticks | ≥50% | 97.7% |
| contest-to-sole / damage evictions | ≥36 / ≥24 | 87 / 51 |
| median end tick | ≤100 | 23 |

The 108-game records were Pincer 45-9-0, Echo 26-27-1, Comet 18-35-1, and
Augur 17-35-2. Every game ended with an Elimination reason; two simultaneous
eliminations were draws. Average end tick was 24.7 and p90 was 41. Damage
occurred on 4.26 ticks per game and 16.57 times per 100 simulation ticks.

The skill-shot layer was materially used: 615/856 shot programs curved, 307
curved shots hit, 234 hits landed after a bend, and 259 were ranged curved
hits. Ninety-six games contained a ranged curved hit. Seventy-one programmed
paths crossed a tile the target had vacated, across 47 games.

The previous native v7 quartet was rerun under its native rules on the same
fresh blocks as a product-generation reference, not a causal A/B. Its 108
games had 57.4% damage incidence, 38.9% reciprocal damage, 1.9% loops, 0.560
median entropy, and a 112 / 200 median / p90. The comparison supports the
watchability direction but does not excuse v8's failed diversity gate.

## Outcome-blind viewer study

Twelve header-only, map/pair-balanced v8 replays were selected with seed
`20260724` before aggregate outcomes were opened. Two fresh reviewers watched
the self-contained viewers at normal speed. Mean ratings were:

| dimension | mean / 5 |
| --- | ---: |
| legibility | 4.17 |
| tension | 3.92 |
| visible action/counter-action | 4.33 |
| freedom from repetition/downtime | 3.75 |
| earned ending | 4.75 |

All 12 samples scored at least 3 for action/counter-action; only one scored 2
or below for repetition. The blind gate passed. Reviewers consistently
understood sole scoring and contest decay from the pressure display. The main
presentation weakness was delayed-projectile causality: some hits needed the
event feed to identify the original shot. A few symmetric arena/bastion
approaches were slow, and sample 11 contained the clearest miss loop.

The representative sample is published owner-only at
`https://nilbots-gen8-highlights.sebastian-lind.chatgpt.site`. It is not a
curated highlight claim.

## Historical sentinels

The four new bots played unchanged Bastille and Helix in a separately reported
144-game screen: 4 draws, 142 Elimination reasons, 1 MaxTicks, median 23, p90
44, and zero faults. Against Bastille the new cohort went 58-13-1; against
Helix it went 43-26-3. These rows establish compatibility and show that the old
passive mirror is no longer self-sufficient. They are not native-product gates.

## Frozen artifacts

| bot | SHA-256 |
| --- | --- |
| Pincer | `0c0271655d25e6b91d520b2f0d55acdefaabd3e205646fff6b98a82b4c1e5abd` |
| Comet | `1b11ae2c53908c3f5d620f50173ad4927039ffa7f1abbe48b443dcce9e024627` |
| Augur | `46b41a9a2bb86dead995e29ae5c5c2053a25188a4c5cc3eebc04e0ff375f5510` |
| Echo | `a5520a6701db5b4d89b1644224530946ed2bc3563c6f1a47a4bd91d0518a2e16` |

All 108 native-v8 replay hashes matched an independent rerun.

## DX changes from the run

1. **One experiment brief.** Territorial control, cone/hearing semantics,
   projectile timing, programmed-shot limits, and the fast iteration path now
   live in `docs/EXPERIMENTAL-TERRITORIAL-V8.md`. Future isolated authors get
   this player-facing document instead of reconstructing rules from scattered
   comments.
2. **Set replay preservation.** `botarena set --out <dir>` now writes a unique
   game directory for each orientation/map/seed. It no longer forces harnesses
   to rely on implicit output naming.
3. **Command help is local.** `botarena <command> --help` now prints that
   command's options and an example instead of the global command list.
4. **SDK identity matches the toolchain.** The SDK project package/assembly
   version is aligned with `ToolchainInfo.SdkVersion` 0.8.0.
5. **Zone docs no longer lie across experiment arms.** `ZoneTiles` explains
   that shared-meter commitment may be successful Wait or territorial sole
   occupancy, depending on the advertised capability.
6. **Contest breaks are visible.** On territorial replays, the viewer labels a
   contested-to-sole transition as `CONTEST BROKEN · <bot> GAINS`.
7. **Evaluation output includes mean duration.** Median and p90 stay the
   guardrails; the script also emits average end tick for complete context.

## Next experiment

Do not tune health, damage, control, projectiles, maps, or the 35% gate.
Freeze Pincer's artifact and the v8 rules. Give losing doctrines a bounded,
equal docs/SDK/CLI-only counterplay iteration, then run fresh seeds. The exact
question is whether strategic adaptation lowers the leader below 35% without
losing v8's combat, activity, objective, and viewer results. The full frozen
protocol and unopened blocks are pre-registered in RULES-0.5-DESIGN §R.
