# DX notes — Still Water

Written before seeing any opponent source, standings, or aggregate cohort
results. Everything below comes from this entrant's own authoring session, its
own self-play, and its own qualification report.

## Identity

| Field | Value |
| --- | --- |
| Entrant | `still-water` |
| Authoring lineage | `still-water-v1` |
| Class | `striker` (declared in `botarena.json`) |
| Role | `verdict-doctrine` |
| Doctrine | patient interceptor |
| Target tier | cumulative T4 (`frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`) |
| Budget | one authoring pass; mechanical/contract repairs free; no open-ended strategic iteration |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `79ad08b6c4cc7c9494c9cd87bafbe5f2b9ca25ec97a1d380cd1f7cc46501df6a` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `676cb185b37ea82758b19ba110d4e1366cb0037d465e8777b2959c188dde77a4` |
| Rule card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `42e12c66f3adc8628dfb505f9f403d8fd2ec3a150da140ebfd9e644bb6789a9a` |

## Frozen artifacts

| Item | Value |
| --- | --- |
| `out/bot.wasm` sha256 | `d4bb23f17ce27cc4ee037fc3608450bd95822924843ac143f22b8fe519b3358a` |
| `out/bot.wasm` size | 3,190,304 bytes |
| `evidence/t4/qualification.json` sha256 | `3b776f38656be71c2087a4181a75a3958024c8d0daff9a05ee99c0dd1d9304cc` |
| Deterministic source-tree hash | `6d8f26c4d50c5228d3e7c8ba32f27b0934856f630e1129018a34f783b8c1e23a` (sha256 of the sorted per-file sha256 listing of all `.cs` + `.csproj` + `botarena.json`, excluding `bin/` and `obj/`) |
| Toolchain | controlled `botarena build`, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK `0.10.4`, WASI p1 core module, macOS host via the platform-matched Docker builder |

Per-file source hashes at freeze:

```
513fc5d6f70403af07e97702a3c65ce8eb1f1a38b37ed8f2eeecc79e970da32f  ActionBook.cs
1e5bfce688d985b4533cfec918e0f05243c8aa327350fe52cd3873e0213f5fd9  Doctrine.cs
df05fd11c3f1efa2dc032eebc9a11f65478aee5dc61e3447d2115935dedb12a6  Field.cs
ee7c05bafef4a7a9cdbcc4e04808070de2cf5cf4ed02cc799120b90ef07db9ea  ForkPlanner.cs
72f6f1030ab516ea034c0da07d8e4e7903d75f8036a0984ff4fecb6aea2afdcb  Quarry.cs
20754674dcc7e1677a9c3cfb6295b94c1f3aa74a2485611c424b13b33eff866c  StillWater.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  StillWater.csproj
d8d3506bd61d97051a758f6c5b44e2b23e1d508f55335e9f595b875b373ef6d2  ThreatField.cs
bc4e5dd51f3eb957982536925da6640a314b5e4631a870463ed38e1624f92d3c  botarena.json
```

## Qualification outcome

`experiment frontline-labs qualify --suite frontline-qualification-5` exited
**3** — a clean capability failure. **Tier awarded: T1.**

| Level | Component | Result |
| --- | --- | --- |
| T4 | suppression-choke | PASS |
| T4 | entry-initiative | PASS |
| T4 | prediction-chamber | PASS |
| T4 | front-rotation | PASS |
| T4 | map-holdout (thin-fronts) | PASS |
| T3 | wall-terminated-bend | PASS |
| T3 | strict-corner | **FAIL** |
| T3 | cadence-parity | PASS |
| T3 | cooldown-window | PASS |
| T3 | local-form-safety | PASS |
| T2 | contract-matrix | PASS |
| T2 | automatic-life-cycle | PASS |
| T2 | objective-path | PASS |
| T2 | direct-fire | PASS |
| T2 | straight-evade | **FAIL** |
| T2 | manual-fabrication | PASS |

**Tier held: T1.** All five T4 doctrinal components pass; the cumulative
prerequisite chain fails on exactly two probes, one at T3 and one at T2, so no
higher cumulative tier is awarded. This is the honest shape of the result and
it is recorded rather than papered over: the entrant demonstrates the T4
positional behaviours but does not clear the fundamentals chain beneath them.

Zero runtime faults and zero rejected actions in every probe run and every
self-play match. `contractValid` and `probeControllerValid` true throughout.

### What the two failures are

- **`strict-corner` (T3).** The probe places a visible body where the only
  geometric bend solution is refused by the strict diagonal-corner rule. Making
  coverage wall-and-corner-exact and adding an angle-seeking posture took this
  from 3 wasted curves / 0 hits to 2 curves / 2 hits with zero unsafe
  commitments, but the probe still fails; the remaining criterion appears to
  couple the legal intercept to holding the objective, and from every objective
  tile in that scenario no legal trajectory to the target exists. Not resolved
  within budget.
- **`straight-evade` (T2).** One damage taken. The bot spawns in a two-tile
  corridor whose only lateral exits are three steps away, and the correct
  evasion is a *backward* walk toward that opening. That is in direct tension
  with `entry-initiative`/`map-holdout`, which fail unless the bot refuses to
  give a choke tile back to a repeating bolt. Two attempts to satisfy both — a
  danger-ranked escape, then a refuge-seeking `Evade` posture — each flipped the
  choke probes back to FAIL. The commitment rule was kept and this probe left
  failing, because three T4 components depend on it.

## Build and qualification timings (macOS, Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` (editing loop) | ~0.5 s |
| `botarena build --no-cache` (cold cache, first ever) | 7.5 s |
| `botarena build --no-cache` (subsequent) | 6–9 s |
| In-process 500-tick match | ~2.6 s including in-process build |
| `qualify --suite frontline-qualification-5` (full cumulative chain, WASM) | ~10 s wall, ~86 s CPU across 8 cores |

The WASM loop was far better than the documentation prepared me for; the doc
warns about slow cold Docker builds, and the actual cold build was 7.5 s. That
is a pleasant surprise worth keeping in the docs.

## Repairs made (mechanical / probe-driven, per packet allowance)

1. **Explicit Fabricate was missing entirely.** The striker class has no
   `fabricate` action, so I removed the scaffold's fabrication helper. Suite 3's
   `manual-fabrication` probe runs the *duel-depth union* profile, which does
   have it, and the bot scored `acceptedFabricationCount: 0`. Added a
   contract-driven fabrication step reading the unit-target constraint from the
   mask. This is exactly the packet's "handle both explicit Fabricate and
   declared automatic activation … when the assigned qualification profile
   requires their union" — and it is a trap that class-armed authoring walks
   straight into, because the class contract genuinely does not contain the verb.
2. **Move oscillation in a choke.** The bot stepped forward, retreated from the
   incoming bolt, stepped forward again, forever. Added a commitment penalty for
   moves that increase distance-to-goal while seizing or contesting, plus a
   one-tick anti-dither term. This alone flipped `entry-initiative` and
   `map-holdout` from FAIL to PASS.
3. **43% of moves blocked against a bulwark.** The legality mask reports a move
   as available and the joint step then refuses it; re-attempting every tick
   burned roughly one move in two. Added a three-tick memory of refused
   destinations. Block rate fell to ~5% and the match outcome went from a
   max-ticks win to a base breach.
4. **Speculative fire.** The bot spent bolts on trajectories no prediction
   supported. Firing now requires the peak swept tile to be a *named* prediction.
5. **Coverage that ignored walls.** The closed-form bend test said a target was
   coverable when the engine's own path preview truncated the bend at a wall or
   a strict corner. Coverage now confirms through `ShotPaths.Preview`.

## Documentation gaps

- **`ShotPaths.Preview` bend indexing is not stated anywhere.** Whether
  `BendAfterTiles = k` means the bend happens *on* tile `k` or *after* it
  changes every reachability formula by one tile. I had to derive it by reading
  the `Preview` body and then confirm it against a `committedPath` in a replay.
  One worked example in the XML docs — "facing East, bend right after 3, the
  bolt occupies (+1,0)(+2,0)(+3,0)(+4,+1)(+5,+2)" — would have saved that.
- **Impact timing is derivable but never written down.** `LaunchTiles`,
  `TilesPerAdvance`, `TicksPerAdvance` and `AdvancesOnLaunchTick` are all in the
  contract, but the composed rule — travel distance `d` lands on tick
  `T + ceil((d - launch) / perAdvance)`, and the target has had that many *plus
  one* decisions — is the single most important number for a predictive bot and
  has to be reconstructed from a replay trace. It belongs in the projectile
  doc-comment.
- **The exact-diagonal blind spot is invisible.** With initial aim pinned to
  zero and one bend, a striker literally cannot hit a target on a perfect
  diagonal, at any range. That is a large, permanent hole in a class's threat
  map and nothing in the class addendum mentions it.
- **The suppression/concession tension is real and undocumented.** T4's choke
  components reward refusing to yield a tile; T2's evade component penalises
  taking a hit. Both are legitimate, but nothing warns an author that a single
  movement rule has to satisfy both, or how the suite expects them reconciled.
- **`--print-candidate-contract` ignores declared classes.** With two
  class-declaring projects it still printed the base `frontline-labs-1`
  identity; the class arm only resolved once a real match ran. The flag is
  documented as emitting "the exact resolved identity for a spec", so it reads
  as authoritative and quietly is not.
- **Qualification analyzers are opaque.** Probes report rich metrics but no
  criterion. `curvedProjectileHitCount: 0` tells you that you missed; it does
  not tell you whether the probe wanted a hit, wanted zero wasted commitments,
  or wanted both plus objective residence. Four of my seven qualification cycles
  were spent guessing which. A single `"failedCriterion"` string per case would
  have collapsed that to one.

## Hardcoding temptations resisted

Every one of these was tempting and every one is read from the contract:

- The 23×15 map and its pillar layout — read as `TileRows`.
- Objective tiles at `(10,7)…(12,8)` and the five ordered positions — read via
  the mode/map binding and the region catalogue.
- "East is forward for team 0" — derived from the objective centroids and the
  team advance delta, with a spawn-based fallback.
- Unlock ticks 120 and 260, respawn 18, rebuild 30 — never referenced; companion
  handling is driven entirely by whether the mask offers a fabrication action.
- Capture threshold 15, decay every 2, redeploy pause 5 — read from the capture
  definition, including `GainPhaseAtTick` so a phased-gain ruleset still works.
- Bend window 1–4, range 8, cooldown 2 — read from the attack profile; the
  standoff band and the fork-reach constant are *computed* from that window, not
  typed in.
- Enemy class identity — deliberately never matched by name prefix. The opposing
  form set is the catalogue minus our own slots' reachable forms, closed over
  declared transition routes; a mirror correctly yields "as dangerous as we are".
- Participant IDs, team IDs, unit IDs and their density — resolved from
  topology; the lateral station bias uses this slot's *rank* in the sorted unit
  list, not its raw id.

## Confusing terminology

- **"Slot-0 bot wins"** in the batch summary is genuinely ambiguous under
  `--swap`. `Total (2 seeds, W = slot-0 bot wins): 0W 2L` was, in a swapped run,
  my bot winning both. I resorted to reading participant names out of the replay
  JSON to be sure who won. Reporting by bot name would remove the trap.
- **"Available"** on a legality entry means individually available, not
  performable — correctly documented, but the gap is much larger in practice
  than the wording suggests (43% of my moves at one point).
- **"Tier awarded"** vs **"tier held"**: the report emits `tierAwarded: T1` when
  the deepest prerequisite fails, even though five T4 components passed. That is
  correct and cumulative, but reading a report that says T1 next to five T4
  PASSes takes a moment to parse.

## Strategy passes

One authoring pass, then contract/mechanical repairs only, as briefed. The
doctrine's shape — standoff band, station, predicted bends, late commit — was
fixed before the first match ran and did not change. Adjustments after that were
either probe-driven repairs (listed above) or corrections where the
implementation contradicted the brief: the retreat line originally walked to the
map edge rather than "conceding ground to keep favorable geometry", and the
station degenerated to the back wall when the front reached our own last
position.

## Isolation incident (disclosed)

The session scratchpad is shared across concurrently running entrants. An output
directory name I chose (`mirror1`) collided with another agent's run, and before
noticing I read aggregate statistics — shot counts, kills, front movement — from
one `fabricator-vs-fabricator` replay that was not mine. No source, no standings,
no doctrine, and nothing from any striker entrant was seen, and nothing from that
file influenced this bot: the reading happened before any doctrine change and the
numbers were from a different class pair entirely. I moved to a uniquely named
private directory (`still-water-priv-7c2e`) immediately and used only my own
scaffolds as sparring partners thereafter. Flagging it because competitive
independence is the experiment's evidence, and a shared scratchpad with
guessable directory names is a real hazard for the next wave.

## Self-play evidence (own runs only, WASM-parity artifact)

Against fresh `--profile generic-actor` scaffolds I created myself, seed 7,
in-process:

| Opponent class | Side | Result |
| --- | --- | --- |
| striker (starter) | team 0 | base breach, +30 |
| striker (starter) | team 1 | base breach, +30 |
| bulwark (starter) | team 1 | base breach, +30 |
| fabricator (starter) | team 1 | base breach, +30 |
| self (mirror) | — | max-ticks, ±30 |

Zero runtime faults in all runs. 84–98% of Still Water's shots use the private
one-bend program, which is the doctrine's core verb actually being spent.
