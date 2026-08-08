# GateStone — DX findings and freeze record (wave 8, the channel wave)

Wave-8 Frontline Labs entrant, class **bulwark**, revision 4 of the `gate-stone`
lineage (wave-6 revision 3 is its parent; wave 7 re-authored the striker cohort
only). Written before seeing any other entrant's source, replays, standings, or
any aggregate balance report.

## Isolation statement

- **Read:** `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
  (sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e`),
  `docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md`
  (`d3ea99a318bf932a63b9b0231c7e8fbb93cadc265a84cd42d4945befb439fc12`),
  `docs/FRONTLINE-LABS-RULES.md`
  (`06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8`),
  `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`
  (`e7e1f023ca696faf5d57103eba14b72744bc6fead8e44cfe87929efe8987409c`).
  The packets and the rule card are byte-identical to the values my wave-6
  freeze recorded; **only the classes addendum moved** (wave 6 recorded
  `2333bd3c…`), which is where the channel, the economy and the salvo were
  added. Plus `templates/botarena-generic-actor/` (`ArenaBasics.cs`
  `dfebec45d84c1e7e7a0a71362a3113bfb0bb94e5e61e6124769485618ba3f4b8`), type
  declarations and XML documentation under `src/BotArena.Sdk/`
  (`GenericActorContext.cs`, `GenericActorRulesContract.cs`,
  `GenericActorActionLegality.cs`, `GenericActorActionArgument.cs`,
  `ActorDecision.cs`, `ActorCanonicalContractReader.cs`), the CLI help of
  `sandbox/cli-publish/`, my own frozen wave-6 directory, and my own contracts,
  replays and qualification evidence.
- **Not read:** any sibling's source, DX, README, manifest, replay, standing or
  aggregate report; `docs/DECISIONS.md`; any Engine, App or CLI implementation
  file; any other `docs/` file. `CLAUDE.md` was injected automatically by the
  harness as repository context; it is an agent guide, not an entrant's
  material.
- **Private scratch:** `sandbox/gate-stone-w8-scratch-2f7a4c19/` — uniquely
  named, created by me, never shared, never read from by anything else. Nothing
  was written outside it and this output directory.
- **Sparring:** my own wave-6 predecessor, **rebuilt from its frozen source**
  with `nilbots build … --no-cache` into that scratch directory (artifact
  `8feb533b3b08fce9fa7fcdf2948ae53f4b536f17e71691aaf55776fa83e0b16a`; its own
  freeze recorded `06b4ae21…`, and the difference is the toolchain moving CLI
  0.9.22 → 0.9.27 / SDK 0.10.6 → 0.10.10, which is exactly why the brief says
  rebuild). Cross-class sparring used the pre-built baseline **artifacts only**
  at `sandbox/w8-baseline-0.10.10/*/out/bot.wasm` — `vector-edge`
  (`d939889f927ef8…`) as the striker and `ledger-fly` (`f4c7e2497ba31d…`) as the
  fabricator. I did not open any file in those directories.

### Two exposures to disclose

1. **I listed `arena-bots/frontline-labs/classes-wave-7-strikers-2026-07-30/`
   once**, to learn which lineage names are strikers so I could pick a striker
   and a fabricator artifact for the cross-class evidence. That is three
   directory names (`arc-light`, `still-water`, `vector-edge`) and no file
   contents. I also listed the file names inside each
   `sandbox/w8-baseline-0.10.10/*/` while checking the artifacts existed, which
   shows siblings' source *file names*. No sibling file was opened.
2. **The host is shared and the process table is not.** While waiting on my own
   sweeps I ran `ps` to diagnose why they had become five times slower, and the
   output showed four other lineages sweeping the same box, including their
   private scratch directory names and some of their ablation variant names
   (e.g. a `no-…` and a `loo_…` directory name). I read nothing of theirs and
   this is disclosed rather than used, but an author who wants strict isolation
   should know that `ps` on this host leaks other entrants' ablation vocabulary.

- **Git:** nothing committed, nothing staged. This tree is untracked.

## Freeze identity

| field | value |
| --- | --- |
| entrant / class | `gate-stone` / `bulwark` (declared in `botarena.json`) |
| lineage | wave-8 revision 4; parent = wave-6 revision 3, same name |
| role / target | `verdict-doctrine`, target T4 (suite 5), achieved **T4** |
| doctrine | `capture-arithmetic-gate` + the wave-6 crew layer + the wave-8 interrupt/economy pass |
| the game | `bastion` = `--movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open --cooldown ticking --volley salvo --capture channel --economy scrap` |
| resolved cells | `frontline-labs-1-bulwark-vs-striker-bastion-facing-locked` and the two sibling pairs; `…-siege-…` without the economy, `…-forge-…` without the channel, `…-swell-…` without either |
| toolchain | nilbots CLI **0.9.27**, SDK 0.10.10, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, Docker platform-matched builder |
| runtime | actor protocol 1.0, configuration 1.0, contract profile `generic-actor-match-2` |
| build | `nilbots build . --no-cache`, cache key `e80758d447219c2c8ba9d3364cbc3c010d94fec874ff367346f08b19dd2ea2a9` (miss/compiled) |
| **bot.wasm sha256** | **`b1da63b710872d384361958644e89deb0716ea0eb6d16320a1f2dfb6bc4f3ed4`** |
| qualification | `evidence/t4/qualification.json`, sha256 `987c131a070235838df88493806681e31e592ecc245b3b6d2766951317944813`, tier **T4**, `passed: true`, `balanceEvidenceEligible: true`, prerequisite **T3 PASS**, all five T4 probes PASS, one attempt |
| source-tree digest | `0d50a5a4c6eadb430ac156e12b5a0872d8e48c6f1bdf862453abe8ddf779f8db` (sha256 of the sorted per-file digest list below) |

Submitted sources (sha256):

| file | lines | sha256 |
| --- | --- | --- |
| `GateStone.cs` | 1143 | `cfe3e731d1db9681c82fdf6d6de436c5045ea13691903d21ebec694fc36eb57e` |
| `StoneChannel.cs` | 457 | `e6972a06d41dfcf3858f89549f7e9384bd21ca4653883c8ba32db009a394b5c2` (new this wave) |
| `StoneScrap.cs` | 316 | `fbf4b491e17f0924fd7305a73b3cc13b1921a68fc53a1bbe68ee3a991efcba91` (new this wave) |
| `StoneDoctrine.cs` | 125 | `50c07a843de3ed4e8950f871fe9568022b3383757ab9c6593fe3c4950b73494a` (new this wave) |
| `StoneGround.cs` | 1259 | `6ee1523f3147c3e689b1ecdf19339f5a1c7778a6d4fef505f821263d74b0d257` |
| `StoneContract.cs` | 978 | `9bacd0b1a45e4bbe9ef46f373ebdab65c5d429a860d73b4f49c66aa7882eacb5` |
| `StoneCrew.cs` | 497 | `ac198e90e1651397ace4b07af958698cfb259c78968ef8507e11f7cc77cd6676` |
| `StoneAim.cs` | 533 | `19ed5535e471cf92ee577a435715853e716a13a3b43aee14dbc3c06fc3752d74` |
| `StoneMemory.cs` | 250 | `5a8926c84addd9a12bffa414188520771588add2bef4ec8465f85aed306d19b4` |
| `ArenaBasics.cs` | 1220 | `4cf44e99d876b16878092289ec4b36f9febc05432d3e4bbcac37f4f960e31229` (scaffold, refreshed from the current template; the only difference from `dfebec45…` is the bot name in one doc-comment) |
| `GateStone.csproj` | — | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |
| `botarena.json` | — | `3eaceec4a3b9c89b4e63a7fc6894a0bee4acd377a9ecc1774cd8d121e20bfc6b` |

Evidence layout: `evidence/t4/` (suite-5 report plus 36 probe replays including
the hash-linked T3 and T2 prerequisites), `evidence/pairs/<opponent>/<cell>/`
(nine WASM matches on `bastion`, one per class pair against each of the three
opponents), `evidence/siege/<cell>/` (three WASM matches on the channel-only
arm). **All twelve pair/siege replays `nilbots verify` OK.** Replays are stored
gzipped — the uncompressed suite is 214 MB, the gzipped set is under 1 MB and the
same bytes; `gunzip -k` before `verify`.

## Doctrine delta, in one paragraph

The capture channel changes the ledger's UNIT. For five waves the unit was one
tick of objective weight; the channel splits presence into a CLAIM that counts
only bodies which did not change tile this tick, capped at two bodies of
surplus, and a DENIAL that counts every body standing there — and the two stop
travelling together. I built the whole ledger on that split: the marginal
progress a body's stillness or presence moves the claim by, this tick, with the
surplus cap, the erosion multiple and the damage interrupt all read off
`gameMode.capture`. **Measured over eighteen distinct games, that arithmetic
plays worse than the weight arithmetic it replaces — +17 against +79 — and that
is the wave's main result.** What ships of the channel is its other half, the
interrupt read as a fire-control fact: a bolt into a body of the controlling
team standing on the point takes progress back at one point per damage, an
eighth of a capture at threshold 8. Beside it the economy ships whole — bank the
scrap you walk over, never go shopping while the front is live, and cast the
store's verb out of ticks that were already worth nothing — and it is the
largest single gain of the wave.

## What the channel asks of a bulwark, and what it does not

**The dodge/eat decision is a non-decision, by arithmetic.** The contract sets
`revertPerDamagePoint = 1` against `gainPerSoleTeamTick = 1`, and the surplus cap
holds the *marginal* body's build contribution at exactly 1 at every stack depth
(a second stationary body takes the gain 1 → 2; a third takes it 2 → 2). So a
channeller that steps aside forfeits one point and a channeller that eats the
bolt reverts one point, at every depth, and stepping aside additionally keeps the
health. The only state where standing wins is erosion, where the build is
multiplied by four and the revert is not. The rule implementing this measured
**exactly inert**: it is correct and it never decided a game.

**The escort pattern is a two-body pattern and this is a one-and-a-half-body
team.** Every contract fact under the screen is real
(`projectilesStopOnFirstEnemyActor`, `alliedProjectileContact: pass-through`,
interrupt scope `controlling-team-bodies-on-active-objective-region`), and it
measured **−7** and was cut. A screen body that dies costs eighteen to thirty
ticks of absence; the bolt it ate was worth one point out of eight. With three
slots unlocking at 0/120/260, the bulwark rarely has a body whose best use is
standing in a firing lane on purpose.

**The surplus cap almost never binds.** Three stationary bodies on one objective
is the state the cap exists to price, and the "third body is free, so anchor it"
opening — the most attractive consequence of the whole arm — barely occurs.

**What does matter is the interrupt, and the store.** Threshold 8 makes a point
of damage on the point worth an eighth of a capture. And the store converts ticks
this doctrine already prices at zero into permanent stat tiers, which is the one
place where "spend the ticks that are worth nothing" pays exactly as the design
intends.

## The measurement design, and why it is not seeds

Seeds are inert (friction 2 below), so a "three-seed sweep" is one game printed
three times. Variance comes from **cell × side × opponent**:

- 3 class pairs (`bulwark-vs-bulwark`, `bulwark-vs-fabricator`,
  `bulwark-vs-striker`), each played from **both** sides with
  `--ignore-declared-classes` so a single-class author can be measured across
  pairs;
- 3 opponents: my own rebuilt **wave-6** predecessor, and two **wave-8 baseline
  artifacts** — one striker lineage and one fabricator lineage, compiled
  `bot.wasm` only;
- one seed, 104729.

That is **18 distinct games** per configuration — not 54 replays — and every
table below is that exact set on the `bastion` game. Outcomes are attributed per
replay from the side the candidate held, never from the CLI's summary column.
**`same-side`** is the subtotal over the games in which my bot holds the
*bulwark* chassis: this entrant declares its class in `botarena.json`, so on any
run that honours declared classes it plays only those games, and the other six
exist purely for breadth.

The `w6` third of that set is a self-mirror whenever the candidate is
behaviour-identical to its predecessor, which is exactly the control the
refactor check uses: it scores +8/−8, −16/+16, +15/−15 = **+0**, and pins the
side advantage in each cell.

## The refactor is behaviour-neutral, and that is checkable

Every wave-8 change sits behind a named switch in `StoneDoctrine`. With all
switches off the artifact reproduces the wave-6 predecessor **decision for
decision** — 711 submitted decisions compared element by element over a whole
`bulwark-vs-striker` match, identical — and scores **+79 aggregate / +78
same-side**, which is the predecessor's own number on the same eighteen games.
So every row below is attributable to a rule and not to the rewrite. Getting
there took three rounds of gating (see the budget ledger); the first version of
this refactor drifted −60 without a single doctrine rule enabled, and I would
not have known which half of the wave was responsible.

## Per-rule attribution, leave-one-out

Base row is the **shipped** artifact; each row removes exactly one rule's effect
and reruns the same eighteen games. Two rows lost a single game to a
runtime-contended run on a shared host and are marked; a missing game is worth
between 0 and ±16, which is the honest error bar on those two rows.

| configuration | record | aggregate | same-side | the rule is worth |
| --- | --- | --- | --- | --- |
| **all switches off** (= the wave-6 predecessor, decision-identical) | 11-6-1 | **+79** | **+78** | — |
| **SHIPPED** | 11-7-0 | **+41** | **+87** | — |
| shipped minus **D8 turret relief** (17 games) | 8-9-0 | −59 | −13 | **+100 / +100** |
| shipped minus **D7 invest** | 11-7-0 | −8 | +38 | **+49 / +49** |
| shipped minus **D6 salvage** (17 games) | 10-6-1 | +29 | +42 | **+12 / +45** |
| shipped minus **D4 revert gun** | 12-6-0 | +52 | +98 | **−11 / −11** |
| the full eight-rule build, i.e. shipped **plus D1–D3, D5** | 9-7-2 | +17 | +17 | **−24 / −70** |

Four things in that table are worth reading twice.

- **The shipped doctrine beats its predecessor on the chassis it declares and
  loses to it overall.** +87 against +78 on the bulwark games; +41 against +79
  across all eighteen. The six games it loses ground on are the ones where the
  harness forces a bulwark-shaped policy onto a striker or fabricator chassis,
  which a declared-class run never plays. I report both columns because they
  disagree and hiding that would be dishonest, and I chose on `same-side`
  because that is the set of games this artifact will actually be in.
- **The channel arithmetic — the wave's headline idea, faithfully implemented —
  is the single largest negative in the table.** It is contract-driven, it is
  correct about what a tick earns, and it costs 24 aggregate and 70 same-side.
  Its verdict is argued out in `StoneDoctrine.ChannelArithmetic`: the marginal
  is an exact answer about *this tick*, and nearly every decision it was asked
  to price spans twenty.
- **The turret-relief repair is worth more than every doctrine rule combined.**
  Wave 6 asked "does the gate need a body?" from inside a turret and priced the
  answer on the turret's own objective weight, which the contract declares as
  zero — so the question was asked several hundred times a match and always
  answered "no". Pricing it on the mobile body the turret would become is four
  lines and +100. It was found by accident, while restructuring `Price` to take
  a body rather than read `context.Self`.
- **One shipped rule measures negative in its own leave-one-out, and I kept
  it.** The revert gun measured **+14** on an earlier six-game base and **−11**
  here; both magnitudes sit inside one game's territorial swing (±16), and it is
  the only rule in the artifact that expresses the interrupt — the half of the
  channel this wave concluded is worth having. Cutting a rule whose sign flips
  with the base is how a doctrine gets tuned into noise. It is kept, and both
  numbers are printed rather than the flattering one.

### The three rules built, measured, and refused

**1. The channel-priced lease.** Charge a fortify, an errand or a held anchor
the channel marginal — zero inside the surplus cap, zero behind a sibling that
already denies. Same arithmetic as the station, right about the tick, and it
cost **25 points** on the six-game base (−19 with it, +6 with wave 6's flat
weight in its place). Kept as a comment in `StoneChannel.StandingWorth`.

**2. The screen.** A body off the objective on the lane into the channeller,
eating the bolt that would have reverted the run. Every contract read under it
is real; it cost **7**. A screen body that dies costs eighteen to thirty ticks
of absence against a revert of one point out of eight. Kept as a comment in
`StoneGround.Covering`; the geometry it needs is still computed and exported by
`StoneChannel.ScreenValue`.

**3. Channelling under the arc.** "A shell on the point cannot be made to step,
so hold the arc while its stillness pays." Exactly backwards: a **transform does
not move the body**, so dropping the shield costs no stillness at all and the
mobile body that comes out is on the same tile with a gun. It tripled shielded
ticks (375 → 1209) while the claim it was protecting stopped moving. Kept as a
comment in `GateStone.Shielded`.

## Results

### Head to head against the rebuilt wave-6 predecessor (bastion, six games)

The control is the same six games played by two behaviour-identical policies,
which by construction score +0 and pin the side advantage in each cell.

| cell | side my bot held | wave-6 self-control | GateStone w8 |
| --- | --- | --- | --- |
| `bulwark-vs-bulwark` | 0 (bulwark) | +8 W | **+1 W** |
| `bulwark-vs-bulwark` | 1 (bulwark) | −8 L | **+10 W** |
| `bulwark-vs-fabricator` | 0 (bulwark) | −16 L | −16 L |
| `bulwark-vs-fabricator` | 1 (fabricator) | +16 W | −16 L |
| `bulwark-vs-striker` | 0 (bulwark) | +15 W | **+16 W** |
| `bulwark-vs-striker` | 1 (striker) | −15 L | −16 L |
| **total** | | **+0**, 3-3-0 | **−21**, 3-3-0 |
| **bulwark chassis only** | | **−1** | **+11** |

The row that matters most is `bulwark-vs-bulwark` **side 1**: the control shows
team 0 wins that cell whenever the two policies are identical, and this revision
wins it from the disadvantaged side by +10. Both of the wave-6-relative losses
are my bot driving the *other* chassis — a fabricator and a striker — which a
declared-class run never asks it to do.

### Against the wave-8 baseline artifacts (bastion, six games each)

| opponent | record | aggregate |
| --- | --- | --- |
| wave-8 baseline **fabricator** lineage | 4-2-0 | **+65** |
| wave-8 baseline **striker** lineage | 3-3-0 | **−3** |

### Frozen WASM confirmation

Nine `bastion` matches (three cells × three opponents) plus three `siege` matches
(channel, no economy) against the wave-6 self, all seed 104729, all from the
frozen artifact, **all twelve `nilbots verify` OK**:

| arm / opponent | `bulwark-vs-bulwark` | `bulwark-vs-striker` | `bulwark-vs-fabricator` |
| --- | --- | --- | --- |
| bastion vs wave-6 self | win, max-ticks | win, max-ticks | loss, breach t183 |
| bastion vs w8 striker | win, **breach t388** | win, max-ticks | loss, breach t306 |
| bastion vs w8 fabricator | win, **breach t207** | win, **breach t59** | loss, max-ticks |
| siege vs wave-6 self | win, max-ticks | win, max-ticks | loss, breach t478 |

The artifact also plays `forge` (economy, no channel) and `swell` (neither)
without a fault, winning `bulwark-vs-striker` on both, so it degrades correctly
when either arm is absent.

### Channel and economy usage, per eighteen games

| | shipped | all switches off (wave-6 behaviour) |
| --- | --- | --- |
| captures completed / conceded | 57 / 51 | 64 / 55 |
| ticks our claim advanced | 493 | 638 |
| runs interrupted by damage / progress reverted | 19 / 62 | 22 / 81 |
| body-ticks standing still on the objective | 2095 | 2397 |
| body-ticks anchored as a turret | 244 | 456 |
| body-ticks shielded | 235 | 240 |
| scrap banked / pickups | **210 / 203** | 190 / 190 |
| **tiers bought** | **14** (13 `optic`, 1 `plate`) | 0 |
| base breaches for | 6 | 8 |

The economy line is the shipped doctrine's clearest behavioural signature: it
banks slightly more than a bot with no economy code at all (because it steps onto
piles it passes rather than only onto the ones it dies next to) and it converts
that bank into **fourteen permanent tiers across eighteen games**, which the
predecessor cannot do at all. Almost all of them are `optic`: a bulwark declares
vision 4 and travel 6, so two optic tiers make it see exactly as far as it
shoots, and the third track it would want is refused for the reason in friction 1.
## Budget ledger — every configuration measured, in order

Rows 1–9 are the doctrine pass proper; rows 10–14 are the measurement that
decided what shipped, and they are the honest cost of the pass: this wave spent
far more of its budget re-measuring than a single pass implies, because the
first three configurations were dominated by my own bugs and the fourth by a
refactor drift I could not see until I gated it. "narrow" is the 6-game subset (3 cells × 2 sides, wave-6 opponent
only) used for fast iteration; "wide" is the 18-game set above.

| # | configuration | set | aggregate | note |
| --- | --- | --- | --- | --- |
| 0 | wave-6 predecessor rebuilt on 0.10.10 | wide | **+79** | the baseline every row is read against |
| 1 | refactor + all rules off | wide | **+79** | decision-for-decision identical to row 0 (verified over a whole match, 711 decisions) |
| 2 | first full doctrine, seven rules | narrow | **−249** (0-18-0) | two bugs, below |
| 3 | + build/deny split; shell-stay rule cut | narrow | −288 | still wrong |
| 4 | + opportunity-cost rent | narrow | −82 | |
| 5 | + counted-flag fix in the marginal | narrow | −17 | the root cause of 2–4 |
| 6 | + station not zeroed by the pause; errand window | narrow | −19 | |
| 7 | + screen cut | narrow | −19 | |
| 8 | + channel lease cut | narrow | **+6** (3-3-0) | first configuration ahead of the predecessor |
| 9 | row 8 on the wide set | wide | +17 | the narrow set had flattered it |
| 10 | row 9 with the scaffold's team-shared lateral coin replaced by the per-life one | wide | +40 | see below |
| 11 | + every remaining unflagged delta gated behind a named rule | wide | (row 1 = +79 with all off) | the refactor is now inert |
| 12 | full eight-rule doctrine on the neutral base | wide | +17 | the channel arithmetic is the whole loss |
| 13 | drop the channel field and its three consumers | wide | **+41** (same-side **+87**) | **shipped** |
| 14 | leave-one-out over the shipped four rules, four rows | wide | see the attribution table | |

### The three bugs that cost rows 2–5, and what they have in common

All three are the same mistake in different clothes: **a marginal is a
difference between two worlds, and the published numbers are already on one side
of it.**

1. **The two halves of a body's worth are not interchangeable.** A body on the
   objective earns by *stillness* (it builds the claim) and by *presence* (it
   denies theirs), and I priced the dodge against the larger of the two. But
   denial counts a body that moved — so the rule made a body stand and eat bolts
   to protect something a sidestep would not have cost it. `−249, 0-18-0`.
2. **A counterfactual has to know whether the body is already counted.** Asking
   "what would this body be worth standing on the point?" while subtracting its
   weight from a claim that does not include it answers **zero for every body
   not already standing there** — which is every body walking toward the fight.
   The relief anchored on the shoulder and the objective stood empty.
3. **A station is a place; a price is a tick.** The redeploy pause zeroes what a
   body earns *this tick*, which is right for deciding what to spend the tick on
   and wrong for deciding where to stand: the pause ends, and the body that
   walked to a shoulder during it has to walk back.

### Row 10: the scaffold's team-safe tie-break cost 23 points

`ArenaBasics.OrderedDirections` moved to `context.TeamRandom` between waves, for
a good and documented reason: a plan built on a per-life coin silently diverges
between teammates, and this lineage's whole coordination layer is built on
deriving a sibling's route with the same code. I adopted it, and it measured
**−23 aggregate** against the same helper on the per-life stream (+17 over 18
games against +40 over 17 — one match of that pair was lost to a contended run,
so read the effect as "about twenty points", not as three significant figures).
It was measured on the eight-rule build rather than the shipped one, which is
the honest caveat on it; I kept the per-life stream because the sign is
unambiguous and the mechanism is.

The reason is the mirror image of the docstring's warning. A team-shared coin
makes two bodies choosing between two equal-cost routes choose the **same**
lateral — and equal-cost routes toward one objective are exactly the situation
where you want them to diverge. Correlated tie-breaks are correlated bodies, and
correlated bodies share a firing lane and queue in the same corridor. The
scaffold anticipates this precisely — "pass a different random only when you
deliberately want a per-life tie-break that no teammate will reproduce" — so the
shipped artifact passes `context.Random` at both router call sites, and pays for
it with a derived sibling route that is one coin-flip less accurate. That trade
is measured; the docstring's default is not wrong, it is just not right for a
doctrine whose bodies converge on one tile by design.
## Top frictions

### 1. Buying gun range kills the match, and the control arm proves it is not the bot

`invest` on the `edge` track — declared effect `mobile-attack-travel-tiles-delta`
— aborts the run with

```
error: A retained projectile must preserve its exact resolved committed path. (Parameter 'projectiles')
```

No replay is written, the exit code is non-zero, and the match is simply gone.

It is **not my code and not the verb**. The pre-registered control arm
`--economy scrap-flat` removes the verb entirely and lets the bank buy greedily
in declared track order (which is `edge` first), and with that flag a match
between two copies of my own **wave-6** artifact — a bot containing no economy
code whatsoever — dies the same way on `bulwark-vs-striker`,
`bulwark-vs-bulwark` and `striker-vs-striker`. `fabricator-vs-striker` survived,
and its replay shows `edge` tier 1 bought at tick 303 on a tick with nothing in
flight. So the fault is a purchase that changes a gun's declared travel while a
projectile of that team is retained across the settle: the engine's own
invariant, tripped by the engine's own store.

**There is no sound guard available to a bot.** A purchase settles *after* the
tick's launches, so the dangerous bolt can be one a teammate fires on this very
tick — and a teammate's next action is the one commitment the observation does
not publish (wave-6 friction 3, now with teeth). Nor can a bot see its own bolts
reliably: this chassis declares vision 4 and travel 6, so its own bolt outruns
its own perception. I implemented and measured both guards that look sufficient
— "no bolt of ours in the perception union" and "no bolt at all in the
perception union" — and both still faulted.

So the shipped artifact **refuses the whole `edge` track**, written against the
declared effect rather than the track name so it lifts by itself on a ladder
that does not move a bolt. That costs this class the purchase the arm is most
obviously designed to give it: a bulwark shoots 6 against a striker's 8, and
`edge` tier 2 erases that standoff exactly. One third of the ladder, and the
third that matters most to the class, is unreachable — and a cell run with
`--economy scrap-flat` cannot be measured at all.

### 2. Seeds still do not reach bot behaviour — on *either* random stream

Wave 6 recorded that these bots are seed-invariant because they never touch
`context.Random`. The scaffold has since routed `OrderedDirections` through
`context.TeamRandom`, this revision consumes it on the router's lateral
tie-break, and four disjoint seeds (104729 / 130363 / 155921 / 199933) still
produce **byte-identical matches** — same length, same completion tick, same
result, and in the three-seed sweeps every per-game statistic identical to the
unit. The replay hash still differs, because the seed is in the header, so
`--seeds a,b,c` produces three directories, three hashes and three summary lines
for one observation. For a wave whose method is A/B this remains the most
expensive trap in the harness: every sweep in this freeze therefore takes its
variance from **cell × side × opponent** and reports one seed, and every number
below is 18 *distinct* games rather than 54 replays. Either make the seed reach
the team stream, or say plainly in the rule card that a match seed reaches bot
behaviour only through `context.Random`.

### 3. The channel's central quantity is not published, and cannot be derived by a fresh life

Capture gain counts bodies "whose tile did not change this tick". Observations
freeze *before* movement, so nothing in the observation says whether a body held
its tile: a bot must remember the previous tick's positions and compare — which
is what this revision does, one tick late, in `StoneMemory.HeldTile`. That is
fine for a body that has been alive a while and **impossible for a body that has
not**: private memory is life-scoped, so a life born this tick cannot tell
whether any body on the objective is contributing to a claim or merely standing
there, including its own teammates. The arm otherwise takes great care to
publish what it changes — the hold owner, the route cooldowns, the scrap piles,
both banks — and this one boolean per observed body ("held its tile into this
tick") is the difference between reading the mechanic and reconstructing it.

### Smaller notes

- **`ArenaBasics.Capture(...).SurplusWeightScalesGain` is silently wrong on the
  channel.** The helper decides it by looking for
  `net-positive-objective-weight-difference` in the control policy; the channel
  policy is
  `stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-…`,
  which scales harder than the policy the helper recognises, and the helper
  answers **false**. A doctrine that keeps trading in that answer prices every
  push on a binary curve and never notices, because both answers are plausible
  integers. Everything in this freeze reads `gameMode.capture` directly.
- **`coordinationGradeAwarded` is still `null`** in a suite-5 report (wave-6
  friction 2, unchanged), and `qualify` still writes one `viewer.html` per probe
  — 36 of them, 214 MB — with no `--viewer` opt-out on that subcommand. The
  frozen evidence here is the same 36 replays gzipped, 948 KB.
- **A `--skills` ablation is unreachable on the shipped game.** Trying to isolate
  the fault above by dropping one skill (`--skills shell` or `--skills volley`)
  fails the 64-character canonical ID budget, because only the full registered
  composites (`bastion`, `siege`, `forge`, `swell`) fit beside a class pair and
  `facing-locked`. That is defensible for identity but it means the one
  diagnostic move an author reaches for first — turn one arm off — is not
  available on the cell being diagnosed.
- **zsh does not word-split** (wave-6 note, still true): `extra="--capture
  channel"` hands the CLI one argument. `${=extra}` or an array.
- **Times** (this host, shared): in-process build 0.7 s; `--no-cache` WASM build
  ≈ 9 s; suite-5 qualification in WASM 7.5 s for 36 probe replays; one 500-tick
  in-process match 3–5 s; an 18-game sweep 90 s idle and 4–6 minutes while other
  lineages were sweeping the same box.

## Freeze integrity: the rebuild from the frozen tree

The last action of this freeze was a fresh `nilbots build . --no-cache` invoked
**on this directory**, after the evidence was written and the prose was
finished. It reproduced the artifact exactly:

| | value |
| --- | --- |
| hash recorded when the evidence was generated | `b1da63b710872d384361958644e89deb0716ea0eb6d16320a1f2dfb6bc4f3ed4` |
| hash from the `--no-cache` rebuild of the frozen tree | `b1da63b710872d384361958644e89deb0716ea0eb6d16320a1f2dfb6bc4f3ed4` |
| cache key, both times | `e80758d447219c2c8ba9d3364cbc3c010d94fec874ff367346f08b19dd2ea2a9` |

Per the wave-6 freeze-integrity warning, `nilbots build` globs every `.cs` under
the project directory, so no ablation variant may live inside the freeze. All of
this wave's variants — the eight leave-one-out builds, the all-off build, the
two scaffold controls, and every intermediate revision — live under
`sandbox/gate-stone-w8-scratch-2f7a4c19/`, outside this tree. This directory
contains exactly the ten submitted `.cs` files, the project file, the manifest,
the two documents, `out/bot.wasm` and `evidence/`.

## Repairs and mechanical fixes, itemised

| # | repair | how it was found |
| --- | --- | --- |
| 1 | Read the capture policy from `gameMode.capture` instead of `ArenaBasics.Capture(...)`, whose `SurplusWeightScalesGain` answers **false** on the channel's control policy | reading the resolved contract out of a replay before writing a line of doctrine |
| 2 | Price a turret's "does the gate need a body?" on the mobile body it would become, not on the turret's declared zero weight | restructuring `Price` to take a body; measured **+100** |
| 3 | Never `invest` in a track whose declared effect changes projectile travel | the match aborting; falsified against the control arm with a bot that has no economy code |
| 4 | `StoneMemory` tracks every observed body's previous tile, so stillness is computable at all | the channel publishes no stillness fact |
| 5 | Pass `context.Random` to `ArenaBasics.OrderedDirections` rather than taking the new team-shared default | measured **−23** for the shared coin |
| 6 | Gate every wave-8 delta behind a named switch until all-off reproduces the predecessor decision-for-decision | a −60 refactor drift that no rule explained |

Qualification history: **one attempt**, tier **T4**, `passed: true`,
`balanceEvidenceEligible: true`, prerequisite T3 PASS, all five T4 probes PASS.
The suite runs the classless duel-depth union profile and this revision needed
no repair to pass it: the new code is contract-driven throughout — it asks
`gameMode.capture` whether an interrupt exists, `gameMode.scrapEconomy` whether
a store exists, the legality mask which tracks are affordable, and the form
catalog what a weight-zero form may carry — so it degrades correctly on a
profile with no channel, no economy, no classes, no shell and no turret.
