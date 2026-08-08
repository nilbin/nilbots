# DX — VectorEdge revision 7 (wave 7, striker salvo-integration round)

**Lineage:** vector-edge-v1 · **Class:** striker · **Doctrine:** pressure-duelist ·
**Role:** verdict-doctrine · **Target tier:** T4 on `frontline-qualification-5`

**Budget as commissioned:** ONE doctrine pass — fan integration: when to enter
the stance, whom to fan, and how the 8-tick entry clock plus the free gun
afterwards reshape duel tempo. Mechanical and contract repairs free.

**Cell:** `frontline-labs-1-striker-vs-striker-swell-facing-locked` and its
bulwark / fabricator siblings —
`--movement facing-locked --pendulum keel --skills kit --bend universal
--aim offset --stance-ground open --cooldown ticking --volley salvo`,
plus `--five-slots wane` where a fabricator is in the pair.

---

## Isolation statement

Everything this revision was written from, with the SHA-256 of the exact bytes
read. Nothing else was opened.

| what | sha256 |
| --- | --- |
| `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` | `251f2425f68fbfae953eb654f46dbc5635ae77b454b099ab6a76522d3d27fbf7` |
| `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` | `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| `docs/FRONTLINE-LABS-RULES.md` — permitted, **not opened** this round | `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| `src/BotArena.Sdk/GenericActorContext.cs` (types + XML docs) | `1dc21e48404908cacfaf1e9d3ce894f84857ef94dbb4519a5a86a300a22ea14e` |
| `src/BotArena.Sdk/GenericActorRulesContract.cs` (types + XML docs) | `5ed88367f33ad1e8968b46534122cdcaec0969b712b3542b0b443ba583f70537` |
| own wave-6 frozen tree `arena-bots/frontline-labs/classes-wave-6-2026-07-30/vector-edge/` — per-file digests in that tree's `sha256s.txt`; its artifact | `3ca784538f34d157de83de2003feb6c471f360627ec0e4902ed8016cfa9075e6` |
| sandbox CLI `sandbox/cli-publish/nilbots` (0.9.25, SDK 0.10.8, game rules 0.5) | `dc31f848488b25794fb28e51cea6ac4805a7a5becfcb4f7d5d21192b8fe3e578` |

Opponent artifacts — **binaries only**. No sibling entrant's source, README, DX,
or replay-not-mine was opened, and no listing of a sibling tree beyond
`*/out/bot.wasm` was taken. `still-water` and `arc-light` are sibling strikers
and were played against exactly as artifacts.

| artifact | sha256 |
| --- | --- |
| `sandbox/w6-rebuilt-0.10.7/iron-root/out/bot.wasm` | `836aef45f3718bd8031e3e67fb323d60fdf19ad478a5bd97a8bdc85643a6a586` |
| `sandbox/w6-rebuilt-0.10.7/march-wall/out/bot.wasm` | `0dc27eefd7c249042aec421c842de642bcec757412faa53aad51709c73723a95` |
| `sandbox/w6-rebuilt-0.10.7/gate-stone/out/bot.wasm` | `118d2bf68ccf570c77d9d1bb45b1ca711ae89d9027ae5e8b3d8369db453e4de8` |
| `sandbox/w6-rebuilt-0.10.7/spark-line/out/bot.wasm` | `aa51577daf01eb975bbc8289885934b2741cfb42a7f3ccddb7fd31c46bf99a31` |
| `sandbox/w6-rebuilt-0.10.7/ledger-fly/out/bot.wasm` | `4f8d64a477f0da61492e7e5773f8264e56ce562266f8f46cdeefb4a5695d76cf` |
| `sandbox/w6-rebuilt-0.10.7/still-water/out/bot.wasm` | `7b97d522dfc8a6d45789ce246cfe1d9205656d8c83782a3e29932abfbf5bc37f` |
| `sandbox/w6-rebuilt-0.10.7/arc-light/out/bot.wasm` | `61257ceb56b8828ea3846aa6c38bcdf96a66ea0c5c2c9b05c2274bf05a00b8bb` |
| `sandbox/w6-rebuilt-0.10.7/vector-edge/out/bot.wasm` — own wave 6, rebuilt on 0.10.7 | `b148b76b4ad1835cbb72105583cdec27ac5e79367da11b2a7756c9721496520c` |

**Disclosures.**

1. `docs/FRONTLINE-LABS-RULES.md` is on the permitted list and was **not read**
   this round. Its digest is recorded anyway so a later audit can distinguish
   "not read" from "read a different version". This revision's rules knowledge is
   revision 6's plus the resolved contract read at run time.
2. **No accidental exposure occurred.** Private scratch was
   `sandbox/vector-edge-w7-scratch-3f9a2c81/`, a uniquely named directory created
   for this round. Nothing was written outside it and the output directory. The
   one shared path touched was `sandbox/w6-rebuilt-0.10.7/`, read only as
   `*/out/bot.wasm`, which the commission explicitly permits. Two things are
   disclosed for completeness rather than because they carry information: a
   directory listing of `arena-bots/frontline-labs/classes-wave-6-2026-07-30/`
   returned sibling entrant directory NAMES (no contents were opened), and one
   throwaway copy of this revision's own artifact was written to `/tmp` during
   the reproduction check and deleted immediately after.
3. **Contract facts came from the machine, not from prose.** The declared numbers
   this revision turns on were read out of a replay-v3 `header.contract.rules`
   from a match this author ran: `striker-volley` `damagePerHit 2` /
   `cooldownTicks 1` / `volley.projectileCount 3`; `striker-bolt` `damagePerHit 1`
   / `cooldownTicks 2`; `volley-striker-{prime,child}` `windup.durationTicks 1`
   and `cooldownTicks 8`; `unstance-striker-*` `windup.durationTicks 1` with
   `automaticReturn {attacks-issued-since-entering-source-form, 1}`;
   `tickResolution.cooldownClock advances-with-time`. The shipped bot reads every
   one of them off `StartLife.Contract` at run time and names none of them.

---

## Budget ledger

| item | budget | spent |
| --- | --- | --- |
| doctrine pass — fan integration | 1 | 1 |
| mechanical / contract repair | free | 3 |
| source files changed | — | `Cast.cs`, `Skills.cs`, `ShotSolver.cs` |
| source files byte-identical to revision 6 | — | `VectorEdge.cs`, `Doctrine.cs`, `Field.cs`, `Advance.cs`, `Traffic.cs`, `DodgeLedger.cs`, `Arms.cs`, `Ballistics.cs`, `ArenaBasics.cs`, `VectorEdge.csproj`, `botarena.json` |
| builds compiled | — | 20 (1 baseline, 1 candidate, 6 first-round ablations, 8 factorial cells, 1 shipped, 5 shipped-whole ablations; several coincide) |
| matches run | — | 1 216 labs matches (19 sweeps × 64) + T4 suite |
| wall time | — | ≈2 h, dominated by WASM sweeps under load |

Free repairs taken, all three of which are contract fields that were simply not
being read:

1. `StanceRoute` now carries the entry route's own `TransitionId`, its declared
   route `CooldownTicks`, and its gun's `DamagePerHit`.
2. The window of ordinary fire a cast gives up was computed from the **stance**
   gun's cadence. That was the right number by coincidence on the arm revision 6
   measured, and wrong the moment the stance gun's cadence moved. It now counts
   the shots this body's **own** cadence would have fired inside the actual pin,
   starting from the cooldown it is standing on.
3. The blanket `health <= HardestHit` refusal — see "the repair the arm created
   by accident" in README. This one is scored below as rule R5 because removing
   it is a doctrine change, not only a repair.

**Deliberately out of budget.** This lineage's standing losses to `iron-root`
and (before this pass) `gate-stone` in the bulwark cell were wave-6 problems and
are not fan problems. `iron-root` is still a loss. Spending a fan-integration
pass on it would have been a different commission.

---

## Per-rule measured attribution

**Method, as the commission requires: leave-one-out from the working whole,
never build-up.** Every row is a build identical to the shipped source except
one `private const bool` in `Cast.cs` flipped to `false` — nothing else changed —
run over the same 8 pairings × 8 seeds = 64 matches. The shipped build has every
switch `true`.

| rule | what it is |
| --- | --- |
| **R1** `BodiesGateIsContractScoped` | revision 5's "fewer than two bodies under the rays ⇒ never cast" keeps its shape and reads its condition (`fanDamage <= gunDamage`) off the contract instead of outliving it |
| **R2** `ReadEntryClock` | read `self.routeCooldowns` for the entry route's transition ID; never request a held route |
| **R3** `CreditKillThresholds` | credit coverage of a body this fan REMOVES and the mobile gun could not, and of a body it OPENS into the mobile gun's one-contact band (the latter only where the contract leaves that gun in hand) |
| **R4** `MarchIsPriced` | a tick with a step worth taking is a margin on the cast rather than a refusal |
| **R5** `PinDerivedSafety` | exposure is the actual pin length against actually-tracked threats, replacing the blanket "one worst-case contact could kill me" refusal |

| build | W-L-D | Δ(W−L) | territorial progress | Δ | casts/match | fan damage/match |
| --- | --- | --- | --- | --- | --- | --- |
| **shipped whole** | **50-14-0** | — | **+16.77** | — | **10.89** | **11.1** |
| − R1 (absolute two-bodies gate) | 18-46-0 | **−64** | −9.47 | **−26.23** | 4.05 | 4.1 |
| − R2 (no entry-clock read) | 50-14-0 | **±0** | +16.77 | **±0.00** | 10.89 | 11.1 |
| − R3 (no kill-threshold credit) | 34-22-8 | **−24** | +6.17 | **−10.59** | 4.03 | 4.4 |
| − R4 (marching tick refused) | 42-22-0 | **−16** | +8.19 | **−8.58** | 8.94 | 9.2 |
| − R5 (blanket worst-case refusal) | 22-42-0 | **−56** | −4.16 | **−20.92** | 5.38 | 5.2 |

**R2 measures exactly zero, and ships anyway with the reason stated.** Removing
the entry-clock read changed **nothing on any metric of any of the 64 matches** —
identical results, progress, cast counts, damage, kills, stance deaths, blocked
requests. The cause is a platform fact, not luck: while a route cooldown is live
the per-tick legality mask already reports `transform` as unavailable with an
empty `allowedFormIds` (measured: 155 held ticks, available on 0; 263 open ticks,
available on 263). It ships because the mask's agreement is a convenience rather
than a contract, because the clock is slot-scoped and survives this body's death
so a fresh life has no history to infer it from, and because the published tick
is the only thing a future revision could *plan* around. **Its attribution is
zero and is reported as zero.**

### The two rules that were built, measured, and are not here

Reported the other way round — leave-one-**in** — because they are not part of
the whole. Same 64 matches, same method.

| build | W-L-D | Δ(W−L) | territorial progress | Δ | casts/match |
| --- | --- | --- | --- | --- | --- |
| **shipped whole** | **50-14-0** | — | **+16.77** | — | **10.89** |
| + scale the fan's score by the damage ratio | 42-22-0 | −16 | +9.39 | −7.38 | 10.92 |
| + spend a paid entry rather than drop the stance | 41-21-2 | −16 | +13.78 | −2.98 | 10.45 |

Both were derived from the contract and both are wrong; the reasoning and the
reason each fails is in README. The first is the more interesting failure: "a
landed volley hits twice as hard" is a true statement about damage and a false
statement about a positional score, and the doctrine only gets paid for it at
the kill thresholds.

### How the three-factor search was actually run

The first working whole contained six rules, three of which measured
net-negative in it. Reporting leave-one-out from a whole that is not the shipped
whole would have been dishonest, so the three contested rules (damage
multiplier, spend-the-paid-entry, march-as-price) were run as a full 2³
factorial with the other three fixed on, 64 matches per cell, and the winning
combination became the whole that the table above ablates. Main effects over
that factorial: damage multiplier −4 mean W−L and −4.7 progress; spend-the-paid-
entry −12 mean W−L and −3.9 progress; march-as-price +3 mean W−L but sign-flipped
by the damage multiplier, which is why the factorial was necessary rather than
three more single ablations.

---

## Fan usage, before and after

| | revision 6 | revision 7 | revision 7, disjoint seeds |
| --- | --- | --- | --- |
| stance entries per match (accepted `transform` into a volley stance) | 0.61 | **10.89** | 10.97 |
| entries refused by the route clock (Blocked `transform`) | 0.00 | **0.00** | 0.00 |
| fan damage per match | 1.36 | **11.08** | 11.22 |
| fan damage as a share of all damage dealt | 2.5% | **24.4%** | 25.0% |
| kills by a fan bolt per match | 0.70 | **5.16** | 5.30 |
| fan kills as a share of all kills | 4.2% | **39.6%** | 40.6% |
| bodies lost while standing in a stance | 0.16 | 1.61 | 1.78 |
| entries per match, striker mirror vs own wave-6 self | **0.00** | **15.00** | 15.00 |
| entries per match, striker mirror vs arc-light | **0.00** | 9.38 | 9.25 |

The last two rows are the commission's complaint, measured: on the salvo arm
revision 6 never cast the fan at all in two of the three striker-mirror
pairings. The stance-death row is the honest cost — a body immobile for three
ticks dies more often, and the doctrine takes that trade knowingly.

---

## Results

Full per-cell table, revision 6 against revision 7, eight seeds per pairing.

| cell | rev 6 W-L-D | prog | casts | fan dmg | fan kills | rev 7 W-L-D | prog | casts | fan dmg | fan kills |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror vs wave-6 self | 0-0-8 | +0.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +38.0 | 15.00 | 15.0 | 8.00 |
| striker mirror vs still-water | 8-0-0 | +23.5 | 1.62 | 5.2 | 1.62 | 8-0-0 | +30.0 | 4.38 | 8.6 | 4.38 |
| striker mirror vs arc-light | 8-0-0 | +30.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +30.0 | 9.38 | 11.0 | 5.38 |
| bulwark-vs-striker vs iron-root | 1-7-0 | -21.4 | 0.12 | 0.4 | 0.25 | 2-6-0 | -21.0 | 8.00 | 5.0 | 1.50 |
| bulwark-vs-striker vs march-wall | 0-8-0 | -8.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +9.0 | 13.00 | 12.0 | 4.00 |
| bulwark-vs-striker vs gate-stone | 0-8-0 | -30.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +26.0 | 17.00 | 16.0 | 9.00 |
| fabricator-vs-striker (wane) vs spark-line | 0-8-0 | -17.0 | 2.00 | 4.0 | 3.00 | 8-0-0 | +30.0 | 2.00 | 4.0 | 2.00 |
| fabricator-vs-striker (wane) vs ledger-fly | 5-3-0 | +3.6 | 1.12 | 1.2 | 0.75 | 0-8-0 | -7.9 | 18.38 | 17.0 | 7.00 |
| **all 64** | **22-34-8** | **-2.41** | **0.61** | **1.4** | **0.70** | **50-14-0** | **+16.77** | **10.89** | **11.1** | **5.16** |

Disjoint second seed set (13, 29, 41, 59, 73, 97, 109, 131):

| cell | rev 6 W-L-D | prog | casts | fan dmg | fan kills | rev 7 W-L-D | prog | casts | fan dmg | fan kills |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| striker mirror vs wave-6 self | 0-0-8 | +0.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +38.0 | 15.00 | 15.0 | 8.00 |
| striker mirror vs still-water | 8-0-0 | +21.6 | 1.50 | 5.0 | 1.50 | 8-0-0 | +30.0 | 5.88 | 11.1 | 5.88 |
| striker mirror vs arc-light | 8-0-0 | +30.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +30.0 | 9.25 | 10.8 | 5.25 |
| bulwark-vs-striker vs iron-root | 2-6-0 | -14.2 | 0.25 | 0.8 | 0.50 | 0-8-0 | -29.1 | 7.12 | 3.9 | 1.25 |
| bulwark-vs-striker vs march-wall | 0-8-0 | -8.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +9.0 | 13.00 | 12.0 | 4.00 |
| bulwark-vs-striker vs gate-stone | 0-8-0 | -30.0 | 0.00 | 0.0 | 0.00 | 8-0-0 | +26.0 | 17.00 | 16.0 | 9.00 |
| fabricator-vs-striker (wane) vs spark-line | 0-8-0 | -17.0 | 2.00 | 4.0 | 3.00 | 8-0-0 | +30.0 | 2.00 | 4.0 | 2.00 |
| fabricator-vs-striker (wane) vs ledger-fly | 6-2-0 | +11.2 | 1.25 | 1.5 | 0.50 | 0-8-0 | -7.5 | 18.50 | 17.0 | 7.00 |
| **all 64** | **24-32-8** | **-0.80** | **0.62** | **1.4** | **0.69** | **48-16-0** | **+15.80** | **10.97** | **11.2** | **5.30** |

---

## Seeds, and what N seeds is worth

**Seeds are nearly inert for deterministic bots, and this is disclosed rather
than papered over.** Both sides are frozen artifacts; the seed reaches this bot
only through `context.Random`, which it uses for mirror-fair direction
tie-breaks. Across the shipped build's 64 matches there are **64 distinct replay
hashes but 15 distinct (result, territorial progress, end tick) outcomes**;
five of the eight cells resolve identically on all eight seeds.

| cell | seeds | distinct replay hashes | distinct outcomes (shipped) | distinct outcomes (revision 6) |
| --- | --- | --- | --- | --- |
| striker mirror vs wave-6 self | 8 | 8 | 1 | 1 |
| striker mirror vs still-water | 8 | 8 | 2 | 4 |
| striker mirror vs arc-light | 8 | 8 | 2 | 1 |
| bulwark cell vs iron-root | 8 | 8 | 5 | 8 |
| bulwark cell vs march-wall | 8 | 8 | 1 | 1 |
| bulwark cell vs gate-stone | 8 | 8 | 1 | 1 |
| fabricator cell vs spark-line | 8 | 8 | 1 | 1 |
| fabricator cell vs ledger-fly | 8 | 8 | 2 | 4 |
| **all** | **64** | **64** | **15** | **21** |

So `W−L` over 64 matches carries roughly **eight correlated cells** of
resolution, not 64 independent observations, and a 64-point swing in the
attribution table means "five cells flipped", not "sixty-four trials
disagreed". The per-cell rows are the honest instrument; territorial progress is
the finer one; the disjoint seed set is the replication.

**The one regression, named.** Against `ledger-fly` the shipped revision loses a
cell revision 6 won (5-3 → 0-8, +3.6 → −7.9), and it is the cell where it casts
most (18.4 entries per match). A five-slot fabricator fields more bodies than
this chassis can remove, and a doctrine tuned to finish duels spends its entries
on a stream of replacements. It reproduces on the disjoint seed set (6-2 → 0-8).
Nothing in a fan-integration budget fixes it; it is a target for the next
commission.

---

## Qualification

```
nilbots experiment frontline-labs qualify \
  --bot out/bot.wasm --suite frontline-qualification-5 --out evidence/t4
```

Exit code **0** — **T4 awarded**, `balanceEvidenceEligible: true`.

| field | value |
| --- | --- |
| suite | `frontline-qualification-5` v1 |
| profile | `frontline-duel-depth-union-t4-v1` |
| qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| artifact hash under test | `c5cb1f102558c04f7351148e9f62ce83e7521a2e69b5373207cf7000d9d9349b` |
| seed | 104729 · runtime `wasm` |
| probes | `suppression-choke` PASS · `entry-initiative` PASS · `prediction-chamber` PASS · `front-rotation` PASS · `map-holdout` PASS |
| prerequisite | `frontline-qualification-4` / `frontline-duel-depth-union-t3-v1` PASS, T3, report sha256 `28d0c471c211a640561300dcbe7192ac873ea497defddf6897c02fdd86d680ea` |
| runtime faults | 0 across every probe |
| report | `evidence/t4/qualification.json`, sha256 `05011c866e28d069378b7321298f1b383f9e16ed1d3a6c4504731e32ee7ac201` |
| duration | ≈2 minutes, 213 MB of replays and viewers |

The qualification profile declares **no volley routes at all**, so
`Skills.VolleyFrom` returns null on every form and `Cast.TryEnter` returns on its
first condition. Everything this revision changed is inert there by construction,
which is the intended property: a class-armed doctrine has to stay
contract-driven to pass a contract that lacks its class routes.

---

## Reproduction

| item | value |
| --- | --- |
| toolchain | `nilbots` 0.9.25 · SDK 0.10.8 · runtime protocol 0.1 / actor 1.0 · game rules 0.5 |
| compiler | NativeAOT-LLVM 10.0.0-rc.1.26306.1 (platform-matched Docker builder) |
| shipped artifact sha256 | `c5cb1f102558c04f7351148e9f62ce83e7521a2e69b5373207cf7000d9d9349b` |
| `--no-cache` rebuild FROM the frozen tree | `c5cb1f102558c04f7351148e9f62ce83e7521a2e69b5373207cf7000d9d9349b` |
| verdict | **reproduces exactly** — byte-identical (`cmp` clean), 8.0 s, cache key `047c4d0879d48693221e65eb47a0ab60eedada0b814ec8676cb09eed7210b59f` |
| per-file source digests | `sha256s.txt` in this directory |
| revision 6 baseline, rebuilt on this CLI | `9455352cbc1e401e16751683e76ade7046efd6045bb19163fdf37ee971c87a9d` (from the wave-6 frozen source, unchanged) |

The revision-6 comparison baseline is that lineage's frozen source rebuilt on
0.9.25 / SDK 0.10.8 rather than the pre-built 0.10.7 artifact, so both sides of
every "before/after" row above were compiled by the same toolchain. The 0.10.7
artifact was used only where it appears as an *opponent*.

---

## Platform friction

Ordered by what it cost this round.

**1. `--print-candidate-contract` prints the identity, not the contract.** The
four declared numbers this entire revision turns on — the volley's
`damagePerHit`, the entry route's `windup.durationTicks` and its route
`cooldownTicks`, and the stance gun's `cooldownTicks` — are reachable from no
CLI flag. `--print-candidate-contract` emits a 15-line block of ruleset ID plus
five fingerprints. Reading the actual values meant running a throwaway match and
digging them out of `replay.json → header.contract.rules`. The CLI help text is
accurate ("emits the exact resolved identity"), so this is a naming and
capability gap rather than a doc bug: either rename it
`--print-candidate-identity`, or add a flag that dumps the resolved contract the
bot will actually receive. Every author on every arm needs those numbers before
writing a line, and every one of them is currently reverse-engineering them out
of a replay.

**2. `self.routeCooldowns` is redundant with the legality mask, and the addendum
does not say so.** DECISIONS #181 reads: "requesting the same route from the same
UNIT SLOT is refused (an ordinary Blocked)". That phrasing says the request
reaches the engine and costs a tick, which makes reading the published clock look
like a real saving. It is not one. Measured over one mirror match: **155 ticks
with a live clock, on every one of which `transform` was reported
`available: false` with an EMPTY `allowedFormIds`; 263 ticks with no clock,
available on all 263.** A bot that reads the mask — which it must, for every
other reason — never observes a Blocked whether it reads the clock or not.
Compiling the clock read out changed **nothing to the decimal across 64
matches**: identical W-L-D, identical territorial progress, identical cast
counts, identical everything except the artifact hash. One sentence in the
addendum ("the mask refuses it too; read the clock to PLAN around the window,
not to avoid a Blocked") would have saved a build and a sweep.

**3. A replay hash is not a behavioural identity check.**
`header.provenance` carries each participant's **name** and artifact hash, both
of which feed `replayHash`. So two artifacts that play byte-identical games
produce 64 different replay hashes — and renaming a build directory is enough to
do it, because the directory name becomes the participant name. Verified twice
this round (`build-f-001` vs `build-ship`, and `build-ship` vs `build-noR2`):
every replay hash differed, every gameplay metric of all 64 matches agreed. The
consequence for anyone doing ablation work is that "did this edit change
behaviour?" has to be answered by extracting metrics, not by diffing hashes. A
`replay --summary`-style behavioural digest that excludes provenance would make
this a one-liner.

**4. An arm that re-arms one weapon silently re-prices every derived worst case.**
`max(damagePerHit)` over all declared attack profiles is the natural contract
reading of "what an unseen contact costs", and it is what this lineage has used
since revision 4 to decide whether a body may enter a form it cannot dodge in.
That number was **1 on every arm this bot had ever been measured on** and is
**2** here. A rule that fired essentially never silently became "no wounded body
may ever cast" — deleting exactly the window the re-armed fan exists for. This is
an authoring hazard rather than a platform bug, and it is the kind that survives
review because nothing in the diff changed. The arm's section is excellent about
what it adds; a line about **what it moves that you did not touch** (largest
declared damage, and therefore every worst-case derivation keyed off it) would
be worth its length.

**5. `botarena.json`'s `sdkVersion` is not checked.** The shipped manifest still
declares `"sdkVersion": "0.10.6"`; the toolchain compiled it against SDK 0.10.8
and reported 0.10.8 in the build banner without a warning. It is left stale
deliberately here — correcting it would change the build-cache key and reset a
measured artifact for no behavioural reason — but a field that can silently
disagree with the thing that actually compiled is a field that will eventually
mislead someone.

**6. `nilbots build <dir>` writes into `<dir>`.** There is no `--out`, so
building a frozen tree mutates it. The commission's "copy it OUT to your scratch
before building — never build inside a frozen tree" instruction is doing real
work, and it should not have to. This also makes the required final act —
rebuild `--no-cache` from the frozen tree — a write to the artifact it is
verifying, which is survivable only because the build is reproducible.

**7. Timings, for anyone planning an ablation budget.** Cold WASM build ~10 s
idle, up to ~35 s under load. A 64-match WASM sweep (8 pairings × 8 seeds) is
~85 s idle and up to ~300 s at load average 30. T4 qualification including the
cumulative T3 prerequisite is ~2 minutes and writes 213 MB of replays and
viewers. The three-factor factorial in this round (8 builds + 8 sweeps) took
~25 minutes wall.

**What worked well, since friction lists are one-sided.** `--seeds a,b,c` with a
per-seed line and a `Total (N seeds, W = slot-0 bot wins)` footer is exactly the
right shape for this work. `--classes X-vs-Y --swap` binds the artifact to the
correct side and says so unambiguously in the participant banner, which removed
a whole category of "am I team 0 or team 1" error. Skill filtering announces
itself (`requested skills without an owning class in this cell change no contract
bytes and are dropped`) rather than silently resolving. And the qualification
runner's automatic rerun and hash-link of the cumulative T3 prerequisite means
"did I break something older" is answered without being asked.
