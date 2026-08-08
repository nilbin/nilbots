# arc-light — wave-7 DX and freeze record

Wave-7 striker-only Frontline Labs entrant. Class **striker**, doctrine
**flank-and-collapse skirmisher**. Revision of my own wave-6 lineage
(`arena-bots/frontline-labs/classes-wave-6-2026-07-30/arc-light/`), which was the
only bot source read.

Wave game **`swell`** — the wave-6 open game on the ticking cooldown clock with
the volley re-armed:

```
--classes <pair> --movement facing-locked --pendulum keel --skills kit \
  --bend universal --aim offset --stance-ground open --cooldown ticking \
  --volley salvo [--five-slots wane]
```

Assignment: **ONE doctrine pass on fan integration** — when to enter the stance,
whom to fan, and how the entry clock plus the free gun afterward reshape collapse
timing. Not a rewrite: the wave-6 coordination layer is shipped untouched.

## Isolation statement

Material read while authoring, and nothing else. SHA-256 of each:

| item | sha256 |
| --- | --- |
| `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` | `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| `docs/FRONTLINE-LABS-RULES.md` | `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` | `251f2425f68fbfae953eb654f46dbc5635ae77b454b099ab6a76522d3d27fbf7` |
| `src/BotArena.Sdk/GenericActorContext.cs` (types + XML docs) | `bcd4cee21bfb56bd980e28e725127d2c5698dfeac988935aae91631a77b9ce74` |
| `src/BotArena.Sdk/GenericActorRulesContract.cs` (types + XML docs) | `5ed88367f33ad1e8968b46534122cdcaec0969b712b3542b0b443ba583f70537` |
| `sandbox/cli-publish/nilbots` (0.9.25, SDK 0.10.8) | `dc31f848488b25794fb28e51cea6ac4805a7a5becfcb4f7d5d21192b8fe3e578` |
| my own wave-6 `README.md` | `ea383c962cf7bd5e196ffabf576af43a7681a730bf36414c2f255936529a9895` |
| my own wave-6 `DX.md` | `ce4e076395af930d33dd797b37dc6feb818ac5e7172697ded10d01727eb962ad` |
| my own wave-6 source tree (13 files) | per-file table below; unchanged files keep their wave-6 digests |

Also read: `templates/botarena-generic-actor/` (the scaffold — `ArenaBasics.cs` is
retained byte-identical and used for its contract readers), the resolved contract
JSON embedded in replays I generated myself, and the CLI's own help output.

**Opponent artifacts played but not opened.** The pre-built wave-6 rebuilds at
`sandbox/w6-rebuilt-0.10.7/*/out/bot.wasm` were used as WASM opponents only. No
sibling entrant's source, README, DX, standings, replays-not-mine, or aggregate
report was opened — including the two sibling strikers `vector-edge` and
`still-water`, whose directories I played against and did not read. Their digests
as played:

| artifact | class played | sha256 |
| --- | --- | --- |
| `iron-root` | bulwark | `836aef45f3718bd8031e3e67fb323d60fdf19ad478a5bd97a8bdc85643a6a586` |
| `march-wall` | bulwark | `0dc27eefd7c249042aec421c842de642bcec757412faa53aad51709c73723a95` |
| `gate-stone` | bulwark | `118d2bf68ccf570c77d9d1bb45b1ca711ae89d9027ae5e8b3d8369db453e4de8` |
| `spark-line` | fabricator | `aa51577daf01eb975bbc8289885934b2741cfb42a7f3ccddb7fd31c46bf99a31` |
| `ledger-fly` | fabricator | `4f8d64a477f0da61492e7e5773f8264e56ce562266f8f46cdeefb4a5695d76cf` |
| `vector-edge` | striker (sibling) | `b148b76b4ad1835cbb72105583cdec27ac5e79367da11b2a7756c9721496520c` |
| `still-water` | striker (sibling) | `7b97d522dfc8a6d45789ce246cfe1d9205656d8c83782a3e29932abfbf5bc37f` |

No `docs/DECISIONS.md`, no `docs/BOT-QUALIFICATION-SUITE.md`, no `DESIGN-*` or
`FORENSICS` file, no aggregate balance report, no Engine/App/Cli source. Private
scratch was `sandbox/arc-light-w7-scratch-3ca97f1e/`, a uniquely named directory
used by nothing else; nothing was written outside it and this output directory.
**No accidental exposure to another author's material occurred.** Nothing was
committed to git.

The wave-6 predecessor I measure against is my own frozen wave-6 source
**rebuilt on this CLI** (`49742f6372cc991033669f3d3e9a5e93a36dd2fb799e1b55fe38150f5b4645b3`),
not the frozen wave-6 artifact, so both sides of every A/B are the same SDK. Every
ablation partner is *this* revision's source differing by exactly one boolean, and
every ablation source lived in scratch — never under the freeze.

## Freeze identity

| item | value |
| --- | --- |
| artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | **`7b586b428070388345b322c9f0d5c5b48eec6b88df897ce94013e0144cd5f23e`** |
| build | `nilbots build <project> --no-cache`, cold cache |
| toolchain | nilbots **0.9.25**, SDK **0.10.8**, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, Docker builder (macOS arm64 host) |
| entry | `ArcLight` (`botarena.json` `entryType`), declared `"class": "striker"` |
| qualification | `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, seed 104729, runtime wasm |
| qualification result | **T4**, `passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`, **exit code 0**, first attempt, no repairs |
| probes | `prerequisite T3`, `suppression-choke`, `entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout` — all PASS |
| qualification report | `evidence/t4/qualification.json`, SHA-256 `65c17e3b0af5c5bd35f40119714348b9eda8925802579d8f239ed2b38e1e75e0` |
| T3 prerequisite | `frontline-qualification-4` (`frontline-duel-depth-union-t3-v1`) rerun and hash-linked, passed, report SHA-256 `0b49ecc3a3d12d0f014cfc7e9a1880b62d4d78cf6595002b175f56c85747a22d` |
| source-tree hash | `1ad8c27d6dc0fc542fc3186b2dd3fa07f604477f0ae45ad39b43536c3defa781` (SHA-256 over the name+digest lines below, name-sorted) |

Per-file SHA-256 (**bold** = changed this wave; the rest are byte-identical to
wave 6, and to wave 5 where wave 6 left them alone):

| file | sha256 |
| --- | --- |
| `ArcBoard.cs` | `f6e665c994634a7d281358b8297da0f3c24ca74a2ea82b937a7a2b9978afca94` |
| **`ArcFacts.cs`** | `bec62364ff07e2be48e366b083f59532d77e240830d304a2a01a462d9e321eef` |
| **`ArcGun.cs`** | `766672d832b8a4e9d0b1f717af6d495ad8689b2e45cfef25913dc3014010be81` |
| `ArcKeel.cs` | `9e8bdf8cb47ee110c6515454666925e4692027591f18afbdff7719c4d9a735ed` |
| **`ArcLight.cs`** | `e2611b3ce4ce5cd27425f92b7468fc4ad75d733f944e8186d19ffaa15de20c83` |
| `ArcLight.csproj` | `d2288bda995372814943941c1fba7becbfe016f883691052f3f6f5c6d9e17ef6` |
| `ArcMemory.cs` | `8f8338426c6619ccab641ce7f36296fff252c2763c2229dc1c93e876216c5924` |
| `ArcMove.cs` | `12afc06f65c56c734baed8c60fbc93ef597a3a646d63407201274e2e79159f60` |
| **`ArcRules.cs`** | `a5af3bd548434a2381cc43533c6c0a255103f7c154ac57fdcd54603232f64a4e` |
| **`ArcStance.cs`** | `5dd789d3ec099dde64f6b8a42fd93be67533b38b7f9274add3d7343573832820` |
| `ArcThreat.cs` | `bbb244d4c9d0c6ca0f12bc276d856656a18f21ccac3fafabcbbae19a4aa87cb5` |
| `ArcTraffic.cs` | `8b65e5ad262d2b54bd8a37f98b74e3f5a2d23820d2f7247aab5d34ecbc264e5f` |
| `ArenaBasics.cs` | `a198af0a28ace85ed9034a9a93d8e106f21a907681547ac7a65e9e21871ce773` (scaffold, unmodified) |
| `botarena.json` | `ada09877c60994dc6d799ae0b5d0864e10b1d18a07b29fdc6827c8a64805ba98` |

Five files carry the wave, and the whole coordination layer (`ArcTraffic.cs`),
the router (`ArcMove.cs`), the threat model (`ArcThreat.cs`), the territory model
(`ArcKeel.cs`) and the life memory (`ArcMemory.cs`) are untouched — this was a
doctrine pass, and it stayed one.

### Reproduction from the frozen tree

Final act, run **from this directory** after the freeze was closed:

```
nilbots build arena-bots/frontline-labs/classes-wave-7-strikers-2026-07-30/arc-light --no-cache
```

| | sha256 |
| --- | --- |
| shipped artifact | `7b586b428070388345b322c9f0d5c5b48eec6b88df897ce94013e0144cd5f23e` |
| rebuilt from the frozen tree | `7b586b428070388345b322c9f0d5c5b48eec6b88df897ce94013e0144cd5f23e` |

Identical, on a reported cold-cache miss (key
`236ab91bba2f6cbc396ace044e48f7aa02d1aad625ed0b5502d74381dc0bf487`), so the
artifact was genuinely recompiled from these bytes rather than served. The freeze
tree contains exactly the fourteen source files above and nothing else, so
`nilbots build`'s `.cs` glob cannot pick up an archived variant — the wave-6
freeze-integrity failure mode, checked again here.

One reproduction detail worth recording because it is easy to misread: this
artifact is **byte-identical to the `EntryBearingBudget`-off ablation build**,
which is correct and expected — retiring V5 *is* that ablation, and a doc-comment
edit alongside it does not reach the IL.

### Resolved arm identities this one artifact played

| pair | flags beyond the common set | rulesetId | rules fingerprint | topology |
| --- | --- | --- | --- | --- |
| `striker-vs-striker` | — | `frontline-labs-1-striker-vs-striker-swell-facing-locked` | `b433356e692941ae…` | `…-three-slots-v1` |
| `bulwark-vs-striker` | — | `frontline-labs-1-bulwark-vs-striker-swell-facing-locked` | `636239cb4dd2d0b1…` | `…-three-slots-v1` |
| `fabricator-vs-striker` | `--five-slots wane` | `frontline-labs-1-fabricator-vs-striker-swell-facing-locked` | `e0d2f194bb4b6bc9…` | `…-asymmetric-slots-4-3-v1` |

## Budget ledger

| item | spent |
| --- | --- |
| doctrine pass (the whole budget) | fan integration: five candidate rules authored, measured leave-one-out, four shipped on and one shipped off |
| mechanical/contract repairs (free) | read `self.routeCooldowns` instead of ignoring it; refuse an entry whose gun cannot fire on the first stance tick; apply the route-cooldown read to the guard and fortify routes too, so the same code is right on a chassis this doctrine was not written for |
| coordination layer | **untouched** — `ArcTraffic.cs` byte-identical, wave-6's C1–C5b shipped exactly as frozen |
| authoring passes | one authoring pass, then three measured revisions (see below), then two full leave-one-out sweeps |
| matches run for this freeze | 1 072 across 67 sweeps, every replay summarised to text and deleted immediately |
| build time | cold `--no-cache` WASM build ~8–10 s; `frontline-qualification-5` including hash-linked T3/T2 ~8 s; one 500-tick WASM match ~2.5 s; a 16-seed sweep ~40–70 s |

Revisions found from my own measurements, not from the qualification suite (which
passed first try):

1. **V5's health-based bearing tolerance was a loss and is shipped off.** Found by
   the leave-one-out, not by eye. See its verdict below.
2. **The whole rule set had to be re-based after V5 was retired.** The first
   leave-one-out was run against a whole that included V5; retiring V5 changes
   what the other four are worth (V2 goes from "worth two mirror games" to
   "without it the fan is never entered at all", because with wave-6's surplus
   rule restored the kill credit is the only thing that ever buys a bearing of
   slack). Every number in the attribution table below is from the **second**
   sweep, against the shipped whole. Attribution by leave-one-out is only
   meaningful relative to a fixed whole, and the whole moved.
3. **`RequiredFanHits` had to be generalised rather than retuned.** The first
   version returned a literal 1. Rewritten as
   `ceil(forgone mobile damage / fan bolt damage)` it returns 1 here and the
   wave-6 answer of 2 on the wave-6 contract, which is the difference between a
   doctrine and a constant.

## The rules I shipped, with measured attribution

Five switches, each `static readonly bool` in `ArcRules.cs` so removing exactly
one is a one-line edit and therefore a real single-lever ablation (`const` would
fold at compile time and the ablation build would differ by dead-code elimination
as well as by behaviour — wave 6's lesson, kept).

Four measurement legs, 16 seeds each, every one against a rebuilt wave-6 artifact
I am permitted to play:

- **mirror** — `striker-vs-striker` against my own rebuilt wave-6 predecessor;
- **still-water** — `striker-vs-striker` against the sibling striker artifact;
- **vector-edge** — `striker-vs-striker` against the other sibling striker artifact;
- **march-wall** — `bulwark-vs-striker` against a bulwark artifact.

**Seeds are inert for deterministic bots and I am not presenting 16 seeds as 16
observations.** Every sweep produced 16 distinct replay hashes; the number of
distinct *outcomes* per 16-seed leg is given in the last column, and it is 1 on
several of them. Records are reported because they are what the leg measured, and
the distinct-outcome count is what says how much independent evidence is behind
one.

### Leave-one-out from the shipped whole

| build | mirror | still-water | vector-edge | march-wall | total | casts/match |
| --- | --- | --- | --- | --- | --- | --- |
| **SHIPPED** | **16-0-0** +28.44 (3) | **16-0-0** +30.00 (1) | **10-6-0** +2.50 (2) | 3-13-0 −9.12 (11) | **45-19-0** | 15.4 |
| V1 `FanPricedInDamage` off | 0-0-16 +0.00 (1) | 0-16-0 −30.00 (1) | 0-16-0 −27.06 (4) | 2-14-0 −19.00 (4) | 2-46-16 | 1.0 |
| V2 `FanExecutes` off | 0-0-16 +0.00 (1) | 0-16-0 −30.00 (1) | 16-0-0 +30.00 (1) | 2-14-0 −20.88 (9) | 18-30-16 | 0.2 |
| V3 `EntryClockIsACharge` off | 16-0-0 +31.25 (6) | 16-0-0 +30.00 (1) | 0-16-0 −26.06 (3) | 3-12-1 −11.19 (9) | 35-28-1 | 17.8 |
| V4 `FanRespectsGuards` off | 16-0-0 +28.44 (3) | 16-0-0 +30.00 (1) | 10-6-0 +2.50 (2) | **0-16-0 −30.00 @69t** (1) | 42-22-0 | 15.9 |
| V5 `EntryBearingBudget` **on** | 16-0-0 +25.81 (5) | 13-3-0 +19.00 (5) | — | 3-9-4 −8.50 (8) | — | 20.4 |

Parenthesised numbers are distinct outcomes out of 16 seeds.

### Per-rule verdicts

**V1 — the break-even is priced in DAMAGE, not in bodies. KEPT; it is the wave.**
Mirror **16-0-0 → 0-0-16**, still-water **16-0-0 → 0-16-0**, vector-edge
**10-6-0 → 0-16-0**; casts collapse from 15.4 to 1.0 a match. The rule is three
lines of `ArcFacts.RequiredFanHits`: the stance costs its declared cycle, the gun
would have fired `ceil(cycle / cadence)` bolts of its own declared damage in that
window, a diverging fan lands at most one bolt per body, so break-even is
`ceil(forgone damage / fan bolt damage)`. On this arm — entry 1, one cast, exit 1,
mobile cadence 2, mobile damage 1, fan damage 2 — that is **one body**; wave 6's
bolts-to-bodies comparison returns **two**. Two is not a rounding difference from
one: it is the difference between "the fan answers a duel" and "the fan answers a
clump", and the wave-6 artifact's own diagnostics are the proof. Playing this arm
unchanged, wave 6 refuses the cast with the veto `pay1s0r2g0` — one body forecast,
two required, no gun bearing — **45.8 times a match against iron-root and 157.6
times a match against march-wall**, and casts a total of 0.00 times in a 500-tick
striker mirror. *The owner's complaint has an exact numeric form: the doctrine was
not shy, it was solving the previous arm's arithmetic correctly.*

**V2 — the fan executes. KEPT, and it is what makes V1 usable.** Mirror
**16-0-0 → 0-0-16**, still-water **16-0-0 → 0-16-0**; casts fall to **0.2 a
match**, i.e. with V2 off the stance is essentially never entered even though V1
has already lowered break-even to one. That interaction is the finding: wave-6's
bearing rule refuses a cast whenever loaded enemy guns exceed the surplus over
break-even, so at exactly break-even the surplus is zero and any bearing at all
refuses. The kill credit is what produces a surplus — `value = bodies + kills`, so
a single lethal body scores 2 against a required 1 — and therefore what buys the
one bearing of tolerance that lets the cast happen at all. The rule itself is one
comparison: a fan bolt removes `damagePerHit`, a diverging fan lands one bolt per
body, so a body at or below that health dies. On this arm that is a wounded
striker and **a fabricator prime at full health** — two health, and also the
supply line, which is why the spark-line leg is 16-0-0 in 66 ticks with exactly
three casts and three kills. V2 also moves an executing cast above the aimed bolt
in the tick order; a non-executing cast keeps wave 6's measured place below it.
*One caveat reported honestly:* on the vector-edge leg V2-off measures **16-0-0
+30.00** against the shipped build's 10-6-0 — against that one opponent, not
casting at all is better than casting well. It is one distinct outcome out of 16
seeds, and it is the only leg of the four that prefers a fanless striker.

**V3 — the entry is a charge, and the clock is published. KEPT.** vector-edge
**10-6-0 → 0-16-0**, march-wall −9.12 → −11.19; the mirror is marginally better
without it (+31.25 vs +28.44 on the same 16-0-0 record), which is what a rule that
spends a resource carefully looks like against an opponent that cannot punish
waste. Three consequences of one contract fact — frequency is priced on the entry
route, whose cooldown is scoped to the unit slot and published on
`self.routeCooldowns`:
 (a) never request a held route. Measured: **0.00 blocked transforms per match on
 every leg**, against a live clock that vetoes with `clock1`…`clock7` several
 times a match, so the read is doing work rather than being vacuously satisfied.
 (b) never buy a bearing for a held route — a rotation is a whole tick under a
 facing lock.
 (c) never waste a charge: refuse an entry whose gun cannot fire on the first
 stance tick (`hot2` fires 5–15 times a match), and fire before returning once
 inside, because the engine's automatic return spends the identical exit windup a
 `mobilize` would and leaving unfired throws away eight ticks of route clock.
 Measured: **unfired exits 0.00 per match shipped, against 3.06 with V3 off** in
 the mirror and 1.06 on vector-edge — and against **1.06 for the wave-6 build**
 against iron-root, which entered the stance and walked straight back out.
 The clock is *asked*, never inferred: it survives this body's death, so a
 respawned life has no history to derive it from and any derivation would be
 wrong exactly when it matters.

**V4 — the fan does not feed a shell. KEPT, and it is worth an entire matchup.**
Exactly **inert on all three striker legs** — records, progress, end ticks, cast
counts and every other counter are byte-identical with and without it, because no
striker form declares a `projectileGuard`. On the bulwark leg it is the difference
between **3-13-0 / −9.12** and **0-16-0 / −30.00 breached at tick 69** with 3
deflections, 3 casts and 3 own bodies lost. The rule: a body whose facing quadrant
contains my tile is not a forecast fan hit, and a denial lane stops at one. The
reason it is now decisive and was not before is arithmetic — a deflection returns
a bolt carrying the damage class of the bolt it caught, and the fan bolt went from
1 to 2, so poking a shell with the fan is the opposition firing a two-damage bolt
at a three-health chassis for free. This is the cleanest single-lever result in
the wave and it is a matchup rule that costs nothing anywhere else.

**V5 — the health-based bearing tolerance. SHIPPED OFF; the wave's negative
result.** The idea was sound-looking: wave 6's "guns must not exceed the surplus
over break-even" was calibrated against a two-tick entry inside a four-tick
immobile cycle, the entry is one tick now, so what the body can afford should be a
question about its health against the hardest bolt visible rather than a constant.
One flag apart, 16 seeds a leg: against still-water **16-0-0 / +30.00 in 157 ticks
with it off** against **13-3-0 / +19.00 in 383 with it on**; in the mirror 16-0-0
/ +28.44 off against 16-0-0 / +25.81 on. Only march-wall prefers it, and barely —
3-9-4 / −8.50 on against 3-13-0 / −9.12 off, the same three wins with four draws
turned into losses. Why, once measured: the health term prices *surviving* the
contact, but what a cast loses when it eats one is the *exchange*, and a striker
that trades a body for a body has spent the scarcer thing. It is left in the
source as `false` with these numbers in its doc comment so the negative result
stays auditable instead of becoming folklore. This is the second wave running in
which the obvious generalisation of a shipped rule measured as a loss.

### Fan usage, before and after

Volley stance entries per match, same 16 seeds, same opponents, one wave apart:

| leg | wave-6 | wave-7 | unfired exits w6 → w7 |
| --- | --- | --- | --- |
| striker mirror (vs my wave 6) | **0.00** | 28.88 | 0.00 → 0.00 |
| striker mirror, swapped assignment | 0.00 | 3.00 | 0.00 → 0.00 |
| vs still-water | 0.00 | 6.00 | 0.00 → 0.00 |
| vs vector-edge | 0.00 | 27.75 | 0.00 → 0.00 |
| vs iron-root | 2.25 | 6.44 | **1.06** → 0.00 |
| vs march-wall | 1.88 | 10.69 | 0.00 → 0.00 |
| vs gate-stone | 0.00 | 22.94 | 0.00 → 0.00 |
| vs spark-line | 0.00 | 3.00 | 0.00 → 0.00 |
| vs ledger-fly | 1.00 | 23.88 | 0.00 → 0.00 |
| **mean over the seven cross legs** | **0.73** | **14.39** | — |

Blocked entry requests: **0.00 per match on every leg**, both waves — wave 6
because it never asked, wave 7 because it reads the clock.

### Records versus wave 6, and the regression

| leg | wave-6 self | wave-7 (frozen) | distinct outcomes /16 (w7) |
| --- | --- | --- | --- |
| striker mirror, both assignments (32 matches) | 0-0-16 per assignment (identical artifacts) | **32-0-0**, +28.44 / +30.00 | 3 / 1 |
| `striker-vs-striker` vs still-water | 0-16-0, −30.00 | **16-0-0, +30.00 @157t** | 1 |
| `striker-vs-striker` vs vector-edge | 0-16-0, −30.00 | **10-6-0, +2.50** | 2 |
| `fabricator-vs-striker` vs spark-line | 0-16-0, −30.00 @157t | **16-0-0, +30.00 @66t** | 1 |
| `fabricator-vs-striker` vs ledger-fly | 0-16-0, −30.00 @258t | 0-16-0, −28.56 @416t | 5 |
| `bulwark-vs-striker` vs gate-stone | 0-16-0, −30.00 @179t | 2-14-0, −21.12 @461t | 10 |
| `bulwark-vs-striker` vs iron-root | 0-16-0, −26.31 | 0-16-0, −29.75 | 2 |
| `bulwark-vs-striker` vs march-wall | **12-4-0, +14.38** | **3-13-0, −9.12** | 11 |
| **seven cross legs, 112 matches each** | **12-100-0** | **47-65-0** | — |

Zero runtime faults and zero disqualifications across every match measured for
this freeze, verified programmatically from the replays' final participant state.

**The march-wall regression is real and I am not going to dress it up.** Wave 6
wins that leg 12-4 by almost never casting; wave 7 loses it 3-13 while casting
10.7 times a match. The leave-one-out says the regression is not attributable to
any one shipped rule — **removing any of V1, V2, V3 or V4 makes march-wall worse,
not better** (2-14, 2-14, 3-12-1 and 0-16 respectively), and adding V5 back moves
it 3-13-0 → 3-9-4 while costing three wins on still-water. So the cost is the
doctrine itself: against that one opponent, three immobile ticks inside a
six-range bulwark gun is a bad trade even when the fan connects, and the
territorial number says so directly — damage dealt goes *up* (28.62 → 33.75) while
progress goes down, i.e. wave 7 out-trades and under-holds. Fixing it would mean a
sixth rule aimed at one artifact, which is over-fitting a population instrument;
the honest report is that the fan doctrine is worth 35 games across the other six
legs and costs 9 on this one.

*Two other cross-class facts worth passing on.* The **gate-stone** leg is the
clearest non-mirror improvement that is not a win: same 0-16 record for wave 6 and
2-14 for wave 7, but wave 6 is **breached at tick 179 for −30.00** and wave 7
survives to tick 461 for −21.12, i.e. the fan converts a collapse into a contest.
And the **spark-line** leg is the doctrine in one sentence: 16-0-0, **+30.00 in 66
ticks, three casts, three kills, one body lost** — a fabricator prime has two
health, a fan bolt removes two, and the prime is the supply line, so the whole
match is three executions of the one body that can rebuild.

## Top 3 frictions

**1. `movement-blocked` shipped — and it still does not name what blocked you.**
Three waves of my DX have asked for a refusal to name its cause. Wave 7 opens the
replay and finds a brand-new observed event kind, `movement-blocked`, carrying
`actorId`, the submitted `action`, `from`, `attemptedTo` and `facing`. That is a
real improvement and it is the event I asked for by name. But the payload answers
"which move of mine failed", which the `ActionResolution` already answered, and
**not** "what beat me" — there is still no rival actor, no contested tile
ownership, no discrimination between *(a)* a sibling claimed the same destination
this tick, *(b)* a sibling was standing there, and *(c)* an enemy or a bolt was.
Those three demand different responses and remain indistinguishable at runtime; I
could still only tell them apart by reconstructing every actor's submitted
destination offline. **One more field — `blockedBy` naming the rival actor, or a
`reason` enum — turns a published event into a usable one.** The gap between "an
event exists" and "the event carries the decision-relevant fact" is exactly one
field wide, and it has been one field wide for four waves.

**2. `self.routeCooldowns` is the best-designed contract addition I have consumed,
and the thing it is attached to has no matching affordance.** The clock does
everything right: it is published rather than derivable, it is scoped to the unit
slot so it is correct across a death, the field is absent when nothing is held so
old contracts look unchanged, and the doc-comment says in plain words why
inferring it from your own completion history is wrong. My consumption of it is
nine lines and it produced **zero blocked requests across 1 072 matches**. The
asymmetry: the *other* half of the same decision — "can my gun fire on the tick I
will need it to" — has no such affordance. `self.cooldown` is published, and the
tick phase order and `cooldownUpdate` policy are published, so the answer is
derivable; but deriving "will this be zero in N ticks under this transition's
`combatState.cooldownContinuity`" means reading three separate policy ID strings
(`preserve-remaining-ticks`, `advances-with-time`,
`successful-attack-sets-configured-ticks-otherwise-subtract-one-floor-zero`) and
composing them by hand. I got it right and I cannot prove I would have on an arm
where the cooldown clock is the historical armed-form one. **A `readyAtTick`
beside `cooldown`, in exactly the grammar `routeCooldowns` already uses, would
make the two halves of a stance decision look like each other.**

**3. `qualify` still writes 36 viewers nobody asked for, and
`coordinationGradeAwarded` is still silently null.** Both are verbatim repeats
from my wave-6 DX and both are cheap. `evidence/t4` arrived at **214 MB**, of
which **193 MB is `viewer.html` beside every probe replay**; stripping them left
21 MB and did not change the report hash
(`65c17e3b…` before and after, verified). `experiment` gained `--viewer`/`--open`
opt-in two CLI versions ago and `qualify` never got the counterpart. And
`qualification.json` still publishes `coordinationGradeAwarded: null` with
`passed: true` and `tierAwarded: "T4"`, with nothing in the packet, the rules card
or `--help` saying which suite populates it — a published field that is silently
null is indistinguishable from a capability you failed to earn.

**Honourable mentions, all repeats.** The published CLI is still
`sandbox/cli-publish/nilbots` while the banner and every document say `nilbots` —
that one is now fixed on disk and this note can retire. `ArenaBasics.ClassOf`
still recovers a class by splitting form IDs on `-`, which the classes doc
explicitly forbids now that `ClassId` is published on self, allies, enemies,
participants and teams, and its doc-comment still promises a replacement that
shipped three waves ago. `botarena.json` still declares `"sdkVersion": "0.10.6"`
in a tree that builds green against **0.10.8** — the field is neither validated
nor updated, so a manifest can drift arbitrarily far from what it actually
compiled against; I left it untouched rather than perturb a measured artifact, and
that is the wrong reason to leave a wrong value in a freeze. And the scaffold
still offers no reader for the facts each wave turns out to be about: this wave
needed `Projectile.DamagePerHit` on an attack profile (not the observed
projectile, which has had it since wave 6), `FormTransition.CooldownTicks`, and
`ObservedSelfState.RouteCooldowns` — three fields, all published, none of them in
`ArenaBasics`.

## Strategy passes

One doctrine pass, exactly as commissioned: re-derive the fan's price from the
contract's own moved numbers, add lethality to the forecast, consume the published
entry clock, and stop feeding shells. Then two full leave-one-out sweeps — the
second because the first retired a rule and the whole moved underneath it. The
coordination layer was not opened. Nothing in the frozen build was tuned by eye:
every number in this file was measured against my own rebuilt predecessor, against
wave-6 sibling artifacts I played but did not read, or against my own single-flag
ablations, and the one rule that measured inert on three legs (V4) is reported as
inert on those three legs rather than as a general win.
