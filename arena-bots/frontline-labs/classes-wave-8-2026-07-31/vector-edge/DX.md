# DX — VectorEdge revision 8 (wave 8, full-cohort channel + economy round)

**Lineage:** vector-edge-v1 · **Class:** striker · **Doctrine:** pressure-duelist ·
**Role:** verdict-doctrine · **Target tier:** T4 on `frontline-qualification-5`

**Budget as commissioned:** ONE doctrine pass integrating the capture channel and
the SCRAP economy. Mechanical and contract repairs free.

**Cells read:** the artifact is read in all four —
`swell` (neither arm), `siege` (channel only), `forge` (economy only) and
`bastion` (both) — so every rule is gated on a contract field that is
inert-absent without its arm, and the no-arm cell is measured for identity
rather than assumed.

---

## Isolation statement

Everything this revision was written from, with the SHA-256 of the exact bytes
read. Nothing else was opened.

| what | sha256 |
| --- | --- |
| `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` | `e7e1f023ca696faf5d57103eba14b72744bc6fead8e44cfe87929efe8987409c` |
| `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` | `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| `docs/FRONTLINE-LABS-RULES.md` | `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| `src/BotArena.Sdk/GenericActorContext.cs` (types + XML docs) | `b954d2bed023d0ae0acbb4f6ed13763988515d8ada8b0df9eb7b2eb21e7cb498` |
| `src/BotArena.Sdk/GenericActorRulesContract.cs` (types + XML docs) | `e99b48632042469f17e5c7dc752bdf81b33adf621d9ec70a96449baf2893b126` |
| `src/BotArena.Sdk/GenericActorActionLegality.cs` | `cd0ea1aea3ba440186d3d1a2ed8f0f77442e70efcc977defa8ac385c1f3eb06b` |
| `src/BotArena.Sdk/GenericActorActionArgument.cs` | `193a590ad30c7f7a829e125256f5a97a2f6e16587f3ab829c3cdd19f0ed2edfc` |
| `templates/botarena-generic-actor/ArenaBasics.cs` (scaffold; the team-safe `OrderedDirections`) | `dfebec45d84c1e7e7a0a71362a3113bfb0bb94e5e61e6124769485618ba3f4b8` |
| own frozen wave-7 tree `arena-bots/frontline-labs/classes-wave-7-strikers-2026-07-30/vector-edge/` — per-file digests in that tree's `sha256s.txt` | (copied OUT to scratch before building) |
| sandbox CLI `sandbox/cli-publish/nilbots` (0.9.27, SDK 0.10.10, game rules 0.5) | `dc31f848488b25794fb28e51cea6ac4805a7a5becfcb4f7d5d21192b8fe3e578` |

Opponent artifacts — **binaries only**. No sibling entrant's source, README, DX,
or replay-not-mine was opened, and no listing of a sibling tree beyond
`*/out/bot.wasm` was taken. `still-water` and `arc-light` are sibling strikers
and were played against exactly as artifacts.

| artifact (`sandbox/w8-baseline-0.10.10/<name>/out/bot.wasm`) | sha256 |
| --- | --- |
| `arc-light` | `7a3a57f14ffe25db47747f44c0535d4dea4db67b251413feed1fd914115e8a1a` |
| `gate-stone` | `8feb533b3b08fce9fa7fcdf2948ae53f4b536f17e71691aaf55776fa83e0b16a` |
| `iron-root` | `060ecfa0e8462f8e7cc47c3e9dd4878ed5233e1435be42fcfe32aa625a228591` |
| `ledger-fly` | `f4c7e2497ba31d580fe944d2a70d5a59164b46d5ffc9c1b49fe012e34fd6f2ce` |
| `march-wall` | `033be0a1c3b8eb3edb701f81b7db5d355e384f76a67d19d03ba2fd7364ff9528` |
| `spark-line` | `fe9da90c54bfcadfa21a645a750c284de612a6b397b63af38106554110103566` |
| `still-water` | `e710280b5a4c45f6e8bc364c76ca64d74c9bedbaa57cafa3540543c60a22dd7e` |
| `vector-edge` — own wave 7, and byte-identical to this author's own rebuild of the frozen wave-7 source on 0.9.27 | `d939889f927ef8690607bc05ab789bede9159f0f5ceb1fb9a4e2fda78b1c14c7` |

**Disclosures.**

1. **No accidental exposure occurred.** Private scratch was
   `sandbox/vector-edge-w8-scratch-5e2b91d7/`, a uniquely named directory created
   for this round. Nothing was written outside it and the output directory. The
   one shared path touched was `sandbox/w8-baseline-0.10.10/`, read only as
   `*/out/bot.wasm`, which the commission explicitly permits. Two things are
   disclosed for completeness rather than because they carry information: a
   directory listing of `arena-bots/frontline-labs/` and of
   `sandbox/` returned sibling entrant directory NAMES (no contents were
   opened), and a `ps` listing taken to diagnose machine load showed one other
   agent's sweep command line, including a path containing the string
   `still-water`. No file of that entrant was opened and no result of theirs was
   read.
2. **Contract facts came from the machine, not from prose.** Every declared
   number this revision turns on was read out of a replay-v3
   `header.contract.rules` from a match this author ran:
   `capture.threshold 8`, `stationaryGainMultiplierCap 2`,
   `opposingErosionMultiplier 4`,
   `claimInterrupt {damage-to-controller-on-objective-reverts-work, 1,
   controlling-team-bodies-on-active-objective-region, whole-run}`;
   `scrapEconomy` veins `(11,1)/(11,13)`, first 120, interval 80, last 360,
   amount 6, wreck 1, assay 1, carry 6, lifetime 80, `maxSimultaneousPiles 16`,
   `bankRegionIds [team-0-home-pad, team-1-home-pad]`,
   `upgradeScope prime-slot-lives-only`, `maxTotalTiers 3`,
   `purchaseMode invest-action`, tracks `edge/plate/optic` each
   `perTierMagnitude 1`, `maxTier 2`, `tierCosts [10,10]`; `invest` action code
   **106**, kind `mode-investment`, parameter kind `upgrade-track`. The shipped
   bot reads every one of them off `StartLife.Contract` or the per-tick mask at
   run time and names none of them.
3. `docs/FRONTLINE-LABS-RULES.md` is on the permitted list and WAS read this
   round (the rule card's map, region and action sections).

---

## Platform friction

Ordered by what it cost this round.

**1. An engine invariant fails on the shipped cell.** On `bastion`
(`--capture channel --economy scrap`) with `--volley salvo`, some matches abort
before writing a replay with

```
error: A retained projectile must preserve its exact resolved committed path. (Parameter 'projectiles')
```

It is deterministic and reproduces exactly:

```bash
sandbox/cli-publish/nilbots experiment frontline-labs \
  --bot <revision-8>/out/bot.wasm \
  --opponent sandbox/w8-baseline-0.10.10/arc-light/out/bot.wasm \
  --classes striker-vs-striker --movement facing-locked --pendulum keel \
  --skills kit --bend universal --aim offset --stance-ground open \
  --cooldown ticking --volley salvo --capture channel --economy scrap \
  --seed 53 --runtime wasm --out /tmp/repro
```

Dropping **any one** of the three factors makes the same seed complete:
`--capture channel` alone completes (breach at 219), `--economy scrap` alone
completes (breach at 352), and `--volley cast` instead of `salvo` completes
(breach at 85). The likely mechanism is the `edge` track: its declared effect is
`mobile-attack-travel-tiles-delta`, a tier settles after every bolt has flown,
and a projectile already in flight then has to be reconciled against a profile
whose travel has changed — which is exactly what "a retained projectile must
preserve its exact resolved committed path" refuses. A volley bolt carries a
different attack profile from the mobile bolt, which is consistent with the
salvo dependence. **A bot cannot cause this**: every action this artifact
submits comes from the tick's legality mask, and an illegal one produces an
ordinary `Blocked` rather than an engine `ArgumentException`.

The cost to the evidence is concrete and is disclosed rather than smoothed: 16
of 64 `bastion` matches abort for this revision, all of them in the two
striker-mirror pairings, so **`siege` is used as the primary channel evidence**
and every `bastion` figure below is a paired comparison over the matches that
completed. The exit code is **0** on an abort and the failure text goes to
stdout, so a sweep script that checks only the return code silently records
nothing and reports a smaller `n`; that is a second, separate bug and it is why
the harness here greps for `error:` as well.

**2. `--print-candidate-contract` still prints the identity, not the contract.**
This is wave 7's number-one friction, unchanged, and this round it cost more
than last round because the arms are bigger. Fifteen declared numbers decide
this revision — the stationary cap, the erosion multiple, the four interrupt
fields, the vein schedule and addresses, the assay and carry amounts, the pile
lifetime, the bank region IDs, the upgrade scope, the total-tier cap, the
purchase mode, and every track's effect/magnitude/max/prices — and not one of
them is reachable from any CLI flag. Getting them meant running a throwaway
match and reading `replay.json → header.contract.rules`. The CLI's own banner
now prints a friendly one-line summary of the economy ("veins at (11,1)/(11,13)
on 120/200/280/360; wrecks drop 1; carry, bank at home, invest in
edge/plate/optic"), which is genuinely useful and also proof that the resolved
values are right there at hand — they simply are not offered in machine form.
Either rename the flag `--print-candidate-identity` or add
`--print-resolved-contract`.

**3. A vendored scaffold helper changed meaning under a lineage that had
already copied it.** `ArenaBasics.OrderedDirections` now draws its lateral
tie-break from `context.TeamRandom` and its own doc comment explains why the
per-life stream "silently diverged across the team — that is exactly the trap
the wave-6 sweeps hit". This lineage copied that helper in wave 4 and trimmed
it, exactly as the packet instructs, so the fix arrived in the template and not
in the bot. Nothing in the addendum or the CLI says "if you vendored
`ArenaBasics`, re-diff it". A `nilbots new --diff-scaffold` — or simply a line
in the classes addendum naming the helpers whose behaviour moved — would close
it. (For the record the change measured **zero** here; the finding is the
silence, not the loss.)

**4. `--skills` cannot be ablated inside the shipped cell.** Asking for
`--skills none` with the rest of the candidate game returns

```
The candidate ID 'frontline-labs-1-striker-vs-striker-veer-aim-tick-channel-scrap-facing-locked'
needs 77 of the 64 canonical characters.
```

The error is excellent — it names the budget, the overflow and the exact ways
out. But the consequence is that the one ablation an author most wants when a
new arm interacts with a skill ("is this the channel or is this the fan?") is
unavailable in the cell the artifact is actually read in, because the composite
token only exists for the full kit. A registered short token for
`veer + aim + tick + channel + scrap` would fix it.

**5. The legality mask makes the store trivially safe, and it should be
advertised harder.** `UpgradeTrackConstraint` offers a track only when the bank
covers its next tier and no cap forbids it, so a purchase routine is
twenty lines with no arithmetic and no possible `Blocked`. This is the single
best-designed piece of the new surface and the addendum says so in one
sentence ("read the mask, don't price the ladder"). Having now written the
routine: that sentence saved an hour, and it deserves to be the *first* line of
the `invest` section rather than the last.

**6. `nilbots build <dir>` still writes into `<dir>`.** No `--out`. So the
commission's "copy the frozen tree OUT to scratch before building" is doing real
work, and the required final act — rebuild `--no-cache` from the frozen tree —
is a write to the very artifact it verifies. Survivable only because the build
is reproducible.

**7. Timings, for anyone planning an ablation budget.** A cold WASM build is
~10 s idle and up to ~60 s at load average 100+. A 64-match WASM sweep is ~18 s
idle and 2–5 minutes at load average 100–200, which is what a shared box looks
like when several authors sweep at once; the wall-clock budget for a
leave-one-out grid is therefore a function of who else is running, not of the
grid. T4 qualification including the cumulative T3 prerequisite is ~8 s of CPU
and writes 214 MB of replays and viewers.

**What worked well.** `--seeds a,b,c`, the per-seed line and the
`Total (N seeds, …)` footer remain exactly the right shape. The contract's
inert-omission discipline is the thing that makes a four-cell artifact possible
at all: `stationaryGainMultiplierCap`, `opposingErosionMultiplier`,
`claimInterrupt` and the whole `scrapEconomy` block are simply *absent* without
their arm, so "does this mechanic exist" is a null check rather than a policy
string match, and the no-arm cell provably plays the previous revision. The
`ScrapPile.ExpiresAtTick` / `ObservedRouteCooldown.ReadyAtTick` /
`HoldEndsAtTick` clock grammar being identical across three unrelated features
is worth more than it looks: one helper reads all three.

---

## Budget ledger

| item | budget | spent |
| --- | --- | --- |
| doctrine pass — channel + economy integration | 1 | 1 |
| mechanical / contract repair | free | 3 |
| source files added | — | `Channel.cs`, `Salvage.cs` |
| source files changed | — | `Advance.cs`, `VectorEdge.cs`, `ShotSolver.cs`, `Field.cs`, `Doctrine.cs`, `ArenaBasics.cs`, `README.md` |
| source files byte-identical to revision 7 | — | `Cast.cs`, `Skills.cs`, `Traffic.cs`, `DodgeLedger.cs`, `Arms.cs`, `Ballistics.cs`, `VectorEdge.csproj`, `botarena.json` |

Free repairs taken, all three of which are contract fields that were simply not
being read:

1. **`StackHelps` was a substring match with an expiry date.** Revision 7 asked
   whether `controlPolicy` contains `net-positive-objective-weight-difference`
   and concluded "surplus weight buys nothing" for every other policy. That is
   true of the policy it was written against and false of the channel, whose
   cap is a *number* saying exactly how much surplus buys. The answer now comes
   from `stationaryGainMultiplierCap`, so the second body joins the point.
2. **A rotation is not a tile change, and the doctrine had no way to say so.**
   `March` now carries `ChangesTile`, set by whichever half of a locked route
   the tick actually emits. Without it, "do not move while channelling" also
   deletes the rotation that arms a seat — see rule C1.
3. **The vendored `ArenaBasics.OrderedDirections` drew a SHARED tie-break from
   the PER-LIFE stream.** The scaffold's own copy has moved to
   `context.TeamRandom` and says why; this lineage's trimmed copy had not. Rule
   T1.

**Deliberately out of budget.** `iron-root`, `gate-stone` and `spark-line`
remain losses in every arm; all three predate the channel, all three are losses
in revision 7's own numbers on this cohort, and a channel-plus-economy pass is
not the commission that fixes them.

---

## Seeds, and what N seeds is worth — stated before any result

**Seeds are inert for this bot on these arms, more completely than in any
previous wave, and every table below has to be read through that fact.** Both
sides are frozen deterministic artifacts; the seed reaches this bot through
exactly one path — the lateral tie-break in the mirror-fair direction order —
and under `facing-locked` on a map with an advance direction that tie almost
never binds.

Measured on the shipped build, `siege`, both disjoint seed sets:

| arm / set | cells | matches | distinct replay hashes | distinct (result, progress, end-tick) outcomes |
| --- | --- | --- | --- | --- |
| siege-A | 8 | 64 | 64 | **8** — one per cell |
| siege-B | 8 | 64 | 64 | **8** — one per cell |

Every cell resolves identically on all eight seeds, and the two disjoint seed
sets produce the same per-cell outcome, so `siege-A` and `siege-B` return the
same 40-24-0. **128 matches carry 8 observations.** Across the whole round the
shipped build played 345 matches with 345 distinct replay hashes and **55
distinct outcomes**, and most of those 55 are the same cell counted once per
arm.

The consequence is stated rather than worked around: a 16-point swing in `W−L`
on a 64-match sweep means **one cell flipped**, not sixty-four trials
disagreeing. The per-cell rows are the instrument; territorial progress is the
finer one; the second seed set is a replication of the harness, not of the
result. Seeds are reported because the packet asks for them and because a
per-seed hash proves the runs happened — not because eight of them are eight
observations.

---

## The engine bug this revision found, isolated to one line of doctrine

The abort in friction item 1 was narrowed to a single decision by the ordinary
leave-one-out machinery, and the isolation is exact — same match, same seed,
same arm, two builds differing in one `const bool`:

| build | economy behaviour | `--economy scrap`, striker mirror vs own wave-7 self, seed 3 |
| --- | --- | --- |
| shipped source with `InvestOnAFreeTick = true`, buying **`edge`** | 1 tier of `mobile-attack-travel-tiles-delta` | **aborts** — `A retained projectile must preserve its exact resolved committed path` |
| identical source with `InvestOnAFreeTick = false` | never invests | completes — max-ticks at 499 |
| an earlier build of the same source that bought **`optic`** instead | 2 tiers of `vision-range-delta` | completes — the cell resolves 0-8-0 |

So the trigger is not "the economy" and not "a purchase": it is **a tier whose
declared effect is `mobile-attack-travel-tiles-delta` settling while one of that
team's projectiles is still in flight.** The tier applies to the profile the
bolt was launched from, the bolt's committed path was resolved against the old
travel budget, and the engine's own retained-projectile invariant then refuses
the reconciliation. A vision tier changes no projectile and is harmless.

**This is disclosed rather than exploited, and the shipped doctrine's
interaction with it is stated plainly.** The final ladder ordering — sight
before reach, because a tile of range this chassis cannot see into is not a
tile — is argued from the contract (`travel 8` against `vision.range 6`) and was
settled before the crash was traced. Its side effect is that the shipped
artifact buys `optic` first and in practice never reaches the third tier, so
`bastion` now completes **64 of 64** where the previous ordering aborted 16.
That is a real consequence of a rule chosen for a real reason, and it is not a
reason to keep the rule if the rule is wrong; it is reported here so the reader
can discount it.

---

## Results — revision 7 against revision 8, in every cell the artifact is read in

Every row is a PAIRED comparison: the same opponent artifact, the same class
pair, the same arm, the same seed, revision 7's frozen source rebuilt on this
CLI against the shipped revision 8. Two disjoint seed sets (A =
3, 11, 19, 23, 37, 47, 53, 67; B = 13, 29, 41, 59, 73, 97, 109, 131), sixteen
matches per cell.

### `siege` — the channel alone (`--capture channel`)

| cell | rev 7 | prog | **rev 8** | prog | interrupts | interrupt dmg | casts |
| --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror vs **own wave-7 self** | 0-0-16 | +0.0 | **16-0-0** | **+18.0** | 5.00 | 6.00 | 13.00 |
| striker mirror vs still-water | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 0.00 | 0.00 | 3.00 |
| striker mirror vs arc-light | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 2.00 | 2.00 | 8.00 |
| bulwark-vs-striker vs iron-root | 0-16-0 | −16.0 | 0-16-0 | −16.0 | 0.00 | 0.00 | 0.00 |
| bulwark-vs-striker vs march-wall | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 5.00 | 7.00 | 3.00 |
| bulwark-vs-striker vs gate-stone | 0-16-0 | −16.0 | 0-16-0 | −16.0 | 0.00 | 0.00 | 0.00 |
| fabricator-vs-striker (wane) vs spark-line | 0-16-0 | −16.0 | 0-16-0 | −16.0 | 0.00 | 0.00 | 1.00 |
| fabricator-vs-striker (wane) vs ledger-fly | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 8.00 | 12.00 | 7.00 |
| **all 128** | **64-48-16** | **+2.00** | **80-48-0** | **+4.25** | 2.50 | 3.38 | 4.38 |

### `bastion` — the shipped game (`--capture channel --economy scrap`)

| cell | rev 7 | prog | **rev 8** | prog | interrupts | invests | scrap banked | tracks |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror vs **own wave-7 self** | 0-0-16 | +0.0 | **16-0-0** | **+8.0** | 5.00 | 2.00 | 22.0 | optic |
| striker mirror vs still-water | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 0.00 | 0.00 | 3.0 | — |
| striker mirror vs arc-light | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 2.00 | 0.00 | 7.0 | — |
| bulwark-vs-striker vs iron-root | 0-16-0 | −16.0 | 0-16-0 | −16.0 | 0.00 | 0.00 | 0.0 | — |
| bulwark-vs-striker vs march-wall | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 5.00 | 0.00 | 1.0 | — |
| bulwark-vs-striker vs gate-stone | 0-16-0 | −16.0 | 0-16-0 | −16.0 | 0.00 | 0.00 | 0.0 | — |
| fabricator-vs-striker (wane) vs spark-line | 0-16-0 | −16.0 | 0-16-0 | −16.0 | 0.00 | 0.00 | 2.0 | — |
| fabricator-vs-striker (wane) vs ledger-fly | 16-0-0 | +16.0 | 16-0-0 | +16.0 | 8.00 | 0.00 | 7.0 | — |
| **all 128** | **64-48-16** | **+2.00** | **80-48-0** | **+3.00** | 2.38 | 0.25 | 5.2 | |

### `forge` — the economy alone (`--economy scrap`)

| cell | rev 7 | prog | **rev 8** | prog | invests | scrap banked | tracks | n |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror vs **own wave-7 self** | 0-0-16 | +0.0 | **16-0-0** | **+30.0** | 1.00 | 16.0 | optic | 16 |
| striker mirror vs still-water | 16-0-0 | +30.0 | 16-0-0 | +30.0 | 0.44 | 12.4 | optic | 16 |
| striker mirror vs arc-light | 16-0-0 | +30.0 | 16-0-0 | +30.0 | 1.00 | 17.0 | optic | 16 |
| bulwark-vs-striker vs iron-root | 0-16-0 | −30.0 | 0-16-0 | −30.0 | 0.00 | 4.0 | — | 16 |
| bulwark-vs-striker vs march-wall | 9-7-0 | −6.1 | 9-7-0 | −5.9 | 0.00 | 7.3 | — | 16 |
| bulwark-vs-striker vs gate-stone | 15-0-0 | +27.0 | 15-0-0 | +27.0 | 0.00 | 16.0 | — | 15 |
| fabricator-vs-striker (wane) vs spark-line | 0-14-0 | −30.0 | 0-14-0 | −30.0 | 0.00 | 5.0 | — | 14 |
| fabricator-vs-striker (wane) vs ledger-fly | 9-3-0 | +15.2 | 9-3-0 | +18.5 | 0.00 | 8.2 | — | 12 |
| **all 121 completed** | **65-40-16** | **+4.55** | **81-40-0** | **+8.87** | | | | |

Seven of the 128 `forge` matches abort on the engine bug in friction item 1;
the table is paired over the 121 that completed for both builds. The aborts
happen where the ladder reaches a THIRD tier and the third tier is `edge`.

### `swell` — neither arm

**Identical to revision 7 in all 128 matches**: 70-42-16, +4.38, same per-cell,
same cast counts, same kills, to the decimal. This is the graceful-degradation
claim measured rather than asserted, and it is the reason the artifact can be
read in four cells: `ChannelRules.Read` and `Salvage.Read` both return null,
and every rule keyed off them disappears.

### What actually changed, said plainly

**One cell moved, it moved in every armed cell, and it moved for a different
reason in each.** Against its own wave-7 self the revision converts a 500-tick
stalemate into a base breach: **0-0-16 becomes 16-0-0 on `siege`, on `forge`
and on `bastion`, on both disjoint seed sets.** Every other cell is identical in
result on every arm. That is the honest size of this pass — and the interesting
part is that the two arms break the same stalemate by different routes, which
the leave-one-out grids confirm: on `siege` the deciding rule is **C1**, on
`forge` it is **E2**, and neither exists on the other's arm.

It is also exactly what the channel's own arithmetic predicts. At equal bodies
on the point, claim weight minus denial weight is zero and nobody gains, so a
mirror between two doctrines that both walk onto the objective is a draw by
construction; the side that stops shuffling is the only one that can be ahead.
The economy reaches the same place from the other direction — a stalemate is
long, a long match banks wreckage, and a tier of sight on a chassis that
already shoots further than it sees is the thing that ends it.

**Three cells are losses and all three predate this pass**: `iron-root` and
`gate-stone` in the bulwark cell and `spark-line` in the fabricator cell are
losses in revision 7's own numbers on this cohort, in every arm, and this
commission does not touch them.

---

## Per-rule measured attribution

**Method, as the commission requires: leave-one-out from the working whole,
never build-up.** Every row is a build identical to the shipped source except
one `public const bool` flipped to `false` — nothing else changed. Two grids,
because a rule can only be measured where its arm exists:

- **Grid S** — `siege`, 8 pairings x 8 seeds = 64 matches. The channel is
  present and the economy is not.
- **Grid B** — `bastion`, the same 64 matches with both arms.

| rule | what it is |
| --- | --- |
| **C1** `StillnessIsCapture` | a tile change on the point costs the tick's gain, so the objective shuffle is refused — but only the STEP, because a rotation changes no tile and still arms the seat |
| **C2** `InterruptIsGround` | a contact on a body of the controlling team standing on the objective is priced in progress, `min(1, reverted / threshold)`, inside the shot solver's priority |
| **C3** `ScreenTheChannel` | past the declared stationary cap the surplus body takes a screen seat off the point, on the line between a visible enemy and an allied body holding it |
| **C4** `ChannelArithmetic` | the cap and the erosion multiple are read, so "does stacking help?" is answered by a number instead of a policy substring |
| **E2** `InvestOnAFreeTick` | `invest` is cast on a tick the gun was reloading, on the track the declared effects say this chassis needs |
| **E3** `HuntTheCarrier` | a visible enemy's published `carriedScrap` raises its priority in proportion to a tier's declared price |
| **T1** `TeamOrderedTieBreaks` | the one shared direction order is drawn from `context.TeamRandom`, not the per-life stream |

### Grid S — `siege`, 64 matches

| build | W-L-D | Δ(W−L) | territorial progress | Δ | objective moves/match |
| --- | --- | --- | --- | --- | --- |
| **shipped whole** | **40-24-0** | — | **+4.25** | — | **2.38** |
| − C1 (shuffle on the point permitted again) | 32-32-0 | **−16** | +0.62 | **−3.62** | 3.25 |
| − C4 (stacking answer from the policy string) | 32-24-8 | **−8** | +2.00 | **−2.25** | 2.38 |
| − C2 (interrupt not priced as ground) | 40-24-0 | **±0** | +4.25 | **±0.00** | 2.38 |
| − C3 (no escort seat) | 40-24-0 | **±0** | +4.25 | **±0.00** | 2.38 |
| − E2 / − E3 / − T1 | 40-24-0 | **±0** | +4.25 | **±0.00** | 2.38 |

The last row is the inertness claim, measured: with no economy declared,
`Salvage.Read` returns null and removing either economy rule changes nothing on
any metric of any of the 64 matches. T1 is also exactly zero here.

### Grid B — `bastion`, 64 matches, every build on the shipped source

| build | W-L-D | Δ(W−L) | territorial progress | Δ | invests/match | objective moves/match |
| --- | --- | --- | --- | --- | --- | --- |
| **shipped whole** | **40-24-0** | — | **+3.00** | — | **0.25** | **2.00** |
| − E2 (the bank is never spent) | 32-32-0 | **−16** | +1.62 | **−1.38** | 0.00 | 2.00 |
| − C1 (shuffle on the point permitted again) | 32-24-8 | **−8** | +2.00 | **−1.00** | 0.25 | 3.12 |
| − C4 (stacking answer from the policy string) | 40-24-0 | **±0** | +4.00 | **+1.00** | 0.38 | 3.12 |
| − C2 (interrupt not priced as ground) | 40-24-0 | **±0** | +3.00 | **±0.00** | 0.25 | 2.00 |
| − C3 (no escort seat) | 40-24-0 | **±0** | +3.00 | **±0.00** | 0.25 | 2.00 |
| − E3 (a courier is an ordinary body) | 40-24-0 | **±0** | +3.00 | **±0.00** | 0.25 | 2.00 |
| − T1 (per-life tie-break stream), 60 paired | 38-22-0 | **±0** | +3.20 | **±0.00** | 0.27 | 2.03 |

Both grids agree on the sign of every rule except one, and the exception is
reported rather than averaged away: **C4 is worth +8 wins and +2.25 progress on
`siege` and costs 1.00 progress on `bastion` at unchanged W−L.** The mechanism
is visible in the last column — reading the cap turns the second body onto the
point instead of into the supporting ring, which is +1.12 objective moves a
match, and on the arm where a long stalemate also banks scrap those extra moves
are ticks the channel does not pay for. It ships because the contract says two
stationary bodies take the point twice as fast and because W−L, the coarser but
less arm-specific instrument, is unmoved; the disagreement is the honest
uncertainty in this pass and is named here rather than in a footnote.

### The three rules that measure exactly zero, and why two of them ship anyway

**C2 — the interrupt priority — is inert on this cohort and is reported as
inert.** Removing it changed nothing on any metric of any of the 128 matches in
either grid. The cause is structural rather than lucky: the solver's priority
already adds **+0.9** for a body standing on the objective, the interrupt term
adds at most `1.6 x reverted/8`, and in every duel this doctrine actually
fights there is one enemy in the solution — so a term that moves an argmax with
one candidate moves nothing. It ships because it is the only thing in the file
that distinguishes "an enemy on the point" from "an enemy on the point whose
team is eight ticks from an advance", and because both operands are contract
data: a ruleset with `revertPerDamagePoint: 2` or a smaller threshold makes it
live without a line of code. **Its attribution is zero and is reported as
zero.**

**C3 — the escort — is inert for a reason that is a fact about the cohort, not
about the rule.** The screen seat is only taken once the point already carries
the declared cap's worth of STILL allied weight, which needs three bodies alive
and two of them holding. On this map, with companions at 120 and 260 and a
threshold of 8, the matches that reach that state are the ones already decided.
It ships because the precondition is read from the contract rather than
assumed, so a cell with more slots or a later breach gets it for free. Zero,
reported as zero.

**T1 — the team-drawn tie-break — is inert and ships as a correctness fix.**
The lateral tie-break it feeds almost never binds under `facing-locked` on a
map with an advance direction, which is also why seeds are inert (see above).
It ships because the alternative is a lineage that has silently diverged its own
bodies' shared derivations since wave 6 and would keep doing so on the first arm
where the tie does bind.

### The rule that was built, measured, and is NOT here

Reported leave-one-**in**, because it is not part of the whole.

| build | grid | W-L-D | Δ(W−L) | progress | Δ |
| --- | --- | --- | --- | --- | --- |
| **shipped whole** | B (`bastion`) | **40-24-0** | — | **+3.00** | — |
| + E1, route a spare body onto a pile within four tiles | B | 32-32-0 | **−8** | +1.38 | **−1.62** |
| **shipped whole** | `forge`, mirror cell, seed set A | **8-0-0** | — | **+30.0** | — |
| + E1 | `forge`, mirror cell, seed set A | 0-8-0 | **−8** | −7.0 | **−37.0** |

"The assay is picked up, not fetched" is a real distinction and four tiles is
on the wrong side of it. The rule is a correct reading of the economy — a pile
is worth a banked point with no transport, and every teammate derived the same
ordered pile list so two bodies never raced for one — and on the arm where it
fires most it turns an eight-seed breach win into an eight-seed loss, because
four tiles off the route is a body the front is missing for eight ticks and the
front is the only thing that scores. What survives is the half that costs
nothing at all: a wreck under a body already standing there is still banked,
because the engine pays the assay at the tile.

---

## Qualification

```
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4
```

Exit code **0** — **T4 awarded**, `balanceEvidenceEligible: true`. Run from the
FROZEN tree, so the participant name and every replay in `evidence/` belong to
the shipped artifact.

| field | value |
| --- | --- |
| suite | `frontline-qualification-5` v1 |
| profile | `frontline-duel-depth-union-t4-v1` |
| qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| artifact hash under test | `c56ab6ba16cbbfda7e11c428fa0d6b560d422cee5bf24afc6a57b2fe8fea47ce` |
| seed | 104729 · runtime `wasm` |
| probes | `suppression-choke` PASS · `entry-initiative` PASS · `prediction-chamber` PASS · `front-rotation` PASS · `map-holdout` PASS |
| prerequisite | `frontline-qualification-4` / `frontline-duel-depth-union-t3-v1` PASS, T3, report sha256 `98c7c9ffa1319f26560f9f6f464cab0d03f4d89cd56f3693f7a8423f3e1be13d` |
| runtime faults | 0 across every probe |
| report | `evidence/t4/qualification.json`, sha256 `041ed1a1713203869f07e0b89359b3d2bca53b371b85d63c67901a88c8e5bdbd` |
| duration | ~8 s of CPU, 214 MB of replays and viewers |

The qualification profile declares **no channel, no economy and no volley
route**, so `ChannelRules.Read`, `Salvage.Read` and `Skills.VolleyFrom` all
return null and every rule this revision added is inert there by construction.
That is the intended property: a class-armed, arm-armed doctrine has to stay
contract-driven to pass a contract that has none of its arms.

---

## Reproduction

| item | value |
| --- | --- |
| toolchain | `nilbots` 0.9.27 · SDK 0.10.10 · runtime protocol 0.1 / actor 1.0 · game rules 0.5 |
| compiler | NativeAOT-LLVM 10.0.0-rc.1.26306.1 (platform-matched Docker builder) |
| shipped artifact sha256 | `c56ab6ba16cbbfda7e11c428fa0d6b560d422cee5bf24afc6a57b2fe8fea47ce` |
| `--no-cache` rebuild FROM the frozen tree | `c56ab6ba16cbbfda7e11c428fa0d6b560d422cee5bf24afc6a57b2fe8fea47ce` |
| verdict | **reproduces exactly** — byte-identical, cache key `2862aa50c028830ac354c96d8e2e6322f0b0e58ba2e78b0eb5aeae8b86171b1b` |
| per-file source digests | `sha256s.txt` in this directory |
| revision 7 baseline, rebuilt on this CLI from its own frozen source | `d939889f927ef8690607bc05ab789bede9159f0f5ceb1fb9a4e2fda78b1c14c7` — byte-identical to the pre-built `sandbox/w8-baseline-0.10.10/vector-edge/out/bot.wasm`, so both sides of every before/after row were compiled by the same toolchain |

---

## Coverage note on the attribution grids

Two grids were run, and their coverage is not symmetric, so it is stated:

- **Grid S (`siege`)** carries the complete leave-one-out for all seven shipped
  rules, on the final shipped source, 64 matches per build.
- **Grid B (`bastion`)** carries the complete final-source leave-one-out for all
  seven shipped rules, 64 matches per build, every variant rebuilt from the
  shipped source after the E1 removal. The `noT1` row is paired over the 60
  matches both builds completed; the other six are 64 of 64.
- The rules were measured on the arms where they exist. An economy rule on
  `siege` and a channel rule on `forge` are inert by construction, and both were
  measured at exactly zero rather than assumed.

**Machine conditions.** Every sweep in this report ran on a box shared with
other authors' sweeps at load averages between 50 and 200 on 18 cores. That
changes wall-clock only: the matches are deterministic and the replay hashes are
reproducible, which is why the tables are hash-backed rather than timing-backed.
