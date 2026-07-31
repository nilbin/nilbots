# arc-light — wave-8 DX and freeze record

Wave-8 full-cohort entrant. Class **striker**, doctrine **flank-and-collapse
skirmisher**. Revision of my own wave-7 lineage
(`arena-bots/frontline-labs/classes-wave-7-strikers-2026-07-30/arc-light/`),
which was the only bot source read.

The new game: the capture **channel** plus the **SCRAP** economy plus
`TeamRandom`.

```
--classes <pair> --movement facing-locked --pendulum keel --skills kit \
  --bend universal --aim offset --stance-ground open --cooldown ticking \
  --volley salvo [--capture channel] [--economy scrap] [--five-slots wane]
```

Assignment: **ONE doctrine pass integrating the channel and the economy.** Not a
rewrite: the wave-6 coordination layer and the wave-7 fan arithmetic are shipped
untouched (`ArcTraffic.cs` and `ArcMove.cs` are byte-identical to wave 7; all
five wave-7 volley switches keep their wave-7 values).

## Isolation statement

Material read while authoring, and nothing else. SHA-256 of each:

| item | sha256 |
| --- | --- |
| `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` | `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| `docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md` | `d3ea99a318bf932a63b9b0231c7e8fbb93cadc265a84cd42d4945befb439fc12` |
| `docs/FRONTLINE-LABS-RULES.md` | `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` | `e7e1f023ca696faf5d57103eba14b72744bc6fead8e44cfe87929efe8987409c` |
| `templates/botarena-generic-actor/ArenaBasics.cs` | `dfebec45d84c1e7e7a0a71362a3113bfb0bb94e5e61e6124769485618ba3f4b8` |
| `templates/botarena-generic-actor/botarena.json` | `42d375f7f23ce1be4209efd92114ab7a91deb25968dbb01ad53d89e4e5939527` |
| `src/BotArena.Sdk/GenericActorContext.cs` (types + XML docs) | `b954d2bed023d0ae0acbb4f6ed13763988515d8ada8b0df9eb7b2eb21e7cb498` |
| `src/BotArena.Sdk/GenericActorRulesContract.cs` | `e99b48632042469f17e5c7dc752bdf81b33adf621d9ec70a96449baf2893b126` |
| `src/BotArena.Sdk/GenericActorActionLegality.cs` | `cd0ea1aea3ba440186d3d1a2ed8f0f77442e70efcc977defa8ac385c1f3eb06b` |
| `src/BotArena.Sdk/GenericActorActionArgument.cs` | `193a590ad30c7f7a829e125256f5a97a2f6e16587f3ab829c3cdd19f0ed2edfc` |
| `src/BotArena.Sdk/GenericActorResolvedMatchContract.cs` | `69a0f31504667fabcb52ef28aa88598bfe5d5f47471fe9f59db7a6f1c976203b` |
| `src/BotArena.Sdk/GenericActorMapContract.cs` | `3b16e8d50864ac3bd81cb41f6f4618ce3bc46c0de4760f3490e2d3ee25b72422` |
| `sandbox/cli-publish/nilbots` (0.9.27, SDK 0.10.10) | `dc31f848488b25794fb28e51cea6ac4805a7a5becfcb4f7d5d21192b8fe3e578` |
| my own wave-7 freeze (source, README, DX) | per-file, unchanged from the wave-7 DX table |

Also read: the resolved contract JSON embedded in replays I generated myself,
and the CLI's own `--help` and `--print-candidate-contract` output.

**Opponent artifacts played but not opened.** The pre-built wave-8 baseline at
`sandbox/w8-baseline-0.10.10/*/out/bot.wasm` was used as WASM opponents only. No
sibling entrant's source, README, DX, standings, replays-not-mine, or aggregate
report was opened — including the two sibling strikers `vector-edge` and
`still-water`, whose artifacts I played and whose directories I did not read.
Digests as played:

| artifact | class played | sha256 |
| --- | --- | --- |
| `arc-light` (my own wave-7 source, rebuilt on this SDK) | striker | `7a3a57f14ffe25db47747f44c0535d4dea4db67b251413feed1fd914115e8a1a` |
| `iron-root` | bulwark | `060ecfa0e8462f8e7cc47c3e9dd4878ed5233e1435be42fcfe32aa625a228591` |
| `march-wall` | bulwark | `033be0a1c3b8eb3edb701f81b7db5d355e384f76a67d19d03ba2fd7364ff9528` |
| `gate-stone` | bulwark | `8feb533b3b08fce9fa7fcdf2948ae53f4b536f17e71691aaf55776fa83e0b16a` (not played this wave) |
| `spark-line` | fabricator | `fe9da90c54bfcadfa21a645a750c284de612a6b397b63af38106554110103566` |
| `ledger-fly` | fabricator | `f4c7e2497ba31d580fe944d2a70d5a59164b46d5ffc9c1b49fe012e34fd6f2ce` (not played this wave) |
| `vector-edge` | striker (sibling) | `d939889f927ef8690607bc05ab789bede9159f0f5ceb1fb9a4e2fda78b1c14c7` |
| `still-water` | striker (sibling) | `e710280b5a4c45f6e8bc364c76ca64d74c9bedbaa57cafa3540543c60a22dd7e` |

`sandbox/w8-baseline-0.10.10/arc-light/` also contains source; it is **my own
wave-7 source**, byte-identical to my frozen wave-7 tree (verified file by file),
so reading it read nothing I did not already own.

No `docs/DECISIONS.md`, no `docs/BOT-QUALIFICATION-SUITE.md`, no `DESIGN-*`,
`FORENSICS-*` or `BLIND-REVIEW-*` file, no aggregate balance report, no
Engine/App/Cli source. Private scratch was
`sandbox/arc-light-w8-scratch-5e2b74c9/`, a uniquely named directory used by
nothing else; nothing was written outside it and this output directory.
**No accidental exposure to another author's material occurred.** Nothing was
committed to git.

The wave-7 predecessor I measure against is my own frozen wave-7 source
**rebuilt on this CLI** (`7a3a57f1…`), not the frozen wave-7 artifact, so both
sides of every A/B are the same SDK. Every ablation partner is *this* revision's
source differing by exactly one boolean, and every ablation source lived in
scratch — never under the freeze.

## Freeze identity

| item | value |
| --- | --- |
| artifact | `out/bot.wasm` |
| **bot.wasm SHA-256** | **`0252d32c2ad2270c8409513471b7bd18d7716f3ae8149fbf483437b69817a940`** |
| build | `nilbots build <project> --no-cache`, cold cache |
| toolchain | nilbots **0.9.27**, SDK **0.10.10**, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, Docker builder (macOS arm64 host) |
| entry | `ArcLight` (`botarena.json` `entryType`), declared `"class": "striker"` |
| qualification | `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, seed 104729, runtime wasm |
| qualification result | **T4**, `passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`, exit code 0, first attempt, no repairs |
| probes | `prerequisite T3`, `suppression-choke`, `entry-initiative`, `prediction-chamber`, `front-rotation`, `map-holdout` — all PASS |
| qualification report | `evidence/t4/qualification.json`, SHA-256 `d9384a88fe5ee3a3f3c031cfbbe6cc6dd3e4296c06cc85f57d64f864a392d0ed` (run from the frozen tree; the identical suite run from scratch, differing only in the directory-derived `artifactName`, is `8554a594a92baacd4f86a8efff2ee88759549856d58709dcc7db5dde5de2ed18`) |
| source-tree hash | `dda704ff435427e80d789d1ed996e4bdd09de9708b7a33679d6e7d6e8a0349cb` (SHA-256 over the name+digest lines below, name-sorted) |

Per-file SHA-256 (**bold** = changed or added this wave):

| file | sha256 |
| --- | --- |
| `ArcBoard.cs` | `f6e665c994634a7d281358b8297da0f3c24ca74a2ea82b937a7a2b9978afca94` |
| **`ArcFacts.cs`** | `d5686060d5c62e46e6f8f1b3ecfbc64fbbe8d4c56d7a98779cae195856eec410` |
| **`ArcGun.cs`** | `23c50a20f72298b4c6caa9f27f4740b0f525f17d673da398b57677ab1ddd2d90` |
| **`ArcKeel.cs`** | `f6c76d33dce0a1311d1d901da388812671658d44ea04b621d55bafe1f4eaaa45` |
| **`ArcLight.cs`** | `4911f6588e0271e5c959776c56abb8c3c45011c715f7344a544d689aab1930e4` |
| `ArcLight.csproj` | `d2288bda995372814943941c1fba7becbfe016f883691052f3f6f5c6d9e17ef6` |
| **`ArcMemory.cs`** | `3228269a2cf866f59a3949c4c842c3eeb6f1315feec0afe095c9c2220f641305` |
| `ArcMove.cs` | `12afc06f65c56c734baed8c60fbc93ef597a3a646d63407201274e2e79159f60` |
| **`ArcRules.cs`** | `c634534306306ce1f1574312f01b8290ff4278aea9557b5b18a2855a8ea06c56` |
| **`ArcScrap.cs`** (new) | `d9ee6b4fa89e97399828289855ccf7b60bbb63f4471cd9c8fe8d0bcc1165d5a6` |
| **`ArcStance.cs`** | `ae3212750ac219ac739fd42b7a2f7dbdc335a6f79c1a17e1d2fc68c2ab3a42e2` |
| **`ArcThreat.cs`** | `288143199f7ffce177c131ddde65afec3f535dd030cd901ce128b05d73793b2e` |
| `ArcTraffic.cs` | `8b65e5ad262d2b54bd8a37f98b74e3f5a2d23820d2f7247aab5d34ecbc264e5f` |
| **`ArenaBasics.cs`** | `0442b3ff12ff1b131d2e79bb3d2c50557c87fa04596873b985e5690a61ccbd77` (scaffold, refreshed — see below) |
| **`botarena.json`** | `7b9dc34294bab2aab5bbf684d34466e538c9ad591e501b51c726041382d8c5e6` |

`ArenaBasics.cs` is the **current** scaffold byte-for-byte apart from the one
line naming the bot's own type, verified by diff. Refreshing it is what adopts
the team-safe `OrderedDirections` — the scaffold now draws its lateral order
from `context.TeamRandom` rather than `context.Random`, so two of my bodies
resolving the same tie resolve it the same way. My wave-7 copy still had the
per-life version.

`botarena.json` changed for exactly one reason: `"sdkVersion"` said `0.10.6` in
a tree that compiled green against `0.10.8` and now `0.10.10`. My wave-7 DX
flagged that and then left it wrong rather than perturb a measured artifact,
which was the wrong call; it is corrected here before any measurement was taken.

### Reproduction from the frozen tree

Final act, run **from the repository root** after the freeze was closed:

```
nilbots build arena-bots/frontline-labs/classes-wave-8-2026-07-31/arc-light --no-cache
```

| | sha256 |
| --- | --- |
| shipped artifact | `0252d32c2ad2270c8409513471b7bd18d7716f3ae8149fbf483437b69817a940` |
| rebuilt from the frozen tree | `0252d32c2ad2270c8409513471b7bd18d7716f3ae8149fbf483437b69817a940` |

Identical, on a reported cold-cache **miss** (key
`b2a3a372052947b75ad35d944e704572ccafbcd91c6a338e51843d1592f2e9ab`), so the
artifact was genuinely recompiled from these bytes rather than served. The freeze
tree contains exactly the fifteen source files above and nothing else, so
`nilbots build`'s `.cs` glob cannot pick up an archived variant.

### Resolved arm identities this one artifact played

| cell | flags beyond the common set | rulesetId (striker mirror) |
| --- | --- | --- |
| `bastion` | `--capture channel --economy scrap` | `frontline-labs-1-striker-vs-striker-bastion-facing-locked` |
| `siege` | `--capture channel` | `frontline-labs-1-striker-vs-striker-siege-facing-locked` |
| `swell` | — | `frontline-labs-1-striker-vs-striker-swell-facing-locked` |

Bastion rules fingerprint `83c926ebd67a3933…`, map fingerprint
`61f477904dfaf048…`, topology `two-team-one-controller-three-slots-v1`.

## Budget ledger

| item | spent |
| --- | --- |
| doctrine pass (the whole budget) | channel + economy integration: seven candidate rules authored, measured leave-one-out from the shipped whole, four shipped on and three shipped off |
| mechanical/contract repairs (free) | the channel control policy read (the scaffold's `Capture()` reports the channel as a **binary** contest); previous-tile memory for the stillness test; effective enemy gun travel under an edge tier; `sdkVersion` corrected; scaffold `ArenaBasics.cs` refreshed for team-safe `OrderedDirections` |
| coordination layer | **untouched** — `ArcTraffic.cs` and `ArcMove.cs` byte-identical to wave 7 |
| authoring passes | one authoring pass, then three measured corrections (below), then a full leave-one-out |
| matches run for this freeze | ~470 across 41 sweeps, every replay summarised to text and deleted immediately; peak disk under 1 GB |
| build time | cold `--no-cache` WASM build ~9 s; `frontline-qualification-5` including hash-linked T3 ~10 s; one 500-tick WASM match ~2–4 s; a 4-seed leg ~15–25 s |

Corrections found from my own measurements, not from the qualification suite
(which passed first try, first attempt, no repairs):

1. **The scrap errand was a TRIP and had to become a DETOUR.** The first version
   priced going to a deposit by how long the front survives the absence
   (`AffordableAbsence`, which is the contract's own capture arithmetic). Against
   a striker that test answers "long enough" right up until it does not: the trip
   version turned the 499-tick mirror draw into a **173-tick breach** and cost the
   whole vector-edge leg (231t → 116t). Rewritten as "what does this add to the
   walk I was making anyway", budgeted at a third of a tile per scrap.
2. **An errand must not redefine what "on station" means.** Even with the detour
   budget, letting a two-tile scrap goal replace the front's goals flipped
   `onStation` false, which raised the fire bar's own threshold from 8 to 55+ and
   **stopped the body shooting while it walked** — casts 9.0 → 4.0, vector-edge
   116t. `onStation` now answers "am I where the FRONT wants me", and the errand
   only routes.
3. **`invest` had to be shipped off, and the reason is an engine abort.** See
   friction 1. The measured cost of shipping it on, once it was reduced to the
   tracks that do not abort: still-water −14.00 → −16.00, iron-root 1-3-0/−6.00 →
   0-3-1/−8.00, spark-line −12.50 → −16.00. Buying sight and health with a body's
   action tick is a loss on every leg that measured it.

## The rules I shipped, with measured attribution

Seven switches, each `static readonly bool` in `ArcRules.cs` so removing exactly
one is a one-line edit and therefore a real single-lever ablation (`const` would
fold and the ablation build would differ by dead-code elimination as well as by
behaviour — wave 6's lesson, kept).

Four measurement legs on the **`bastion`** cell, 4 disjoint seeds each
(11/29/47/83), every one against a wave-8 baseline artifact I am permitted to
play. **Seeds are inert for deterministic bots and I am not presenting 4 seeds as
4 observations.** Every sweep produced 4 distinct replay hashes; the number of
distinct *outcomes* per leg is in the last column and it is 1 on several.

### Leave-one-out from the shipped whole

`x` is interrupt hits landed per match; the parenthesised number is distinct
outcomes out of 4 seeds.

| build | still-water | vector-edge | iron-root | spark-line |
| --- | --- | --- | --- | --- |
| **SHIPPED** | 0-4-0 −14.00 @320t (3) | 0-4-0 −16.00 @231t (1) | **1-3-0 −6.00 @479t x11.2** (3) | 0-4-0 **−12.50** @355t (4) |
| W1 `StillnessIsTheCapture` off | 0-4-0 −14.00 @320t (3) | 0-4-0 −16.00 @231t (1) | 1-3-0 **−8.50 @370t x3.0** (3) | 0-4-0 **−16.00** @317t (3) |
| W2 `ChannelDodgeIsPriced` off | identical | identical | identical | identical |
| W3 `ChannelersArePrey` off | 0-4-0 −14.00 @**415t** (3) | 0-4-0 −16.00 @**196t** (1) | identical | 0-4-0 −14.00 @332t (4) |
| W5 `ScrapOnTheWay` off | identical | identical | identical (s12.5 vs s13.5) | 0-4-0 **−14.25** @345t, **s8.0 vs s15.8** (3) |
| W6 `ReadEnemyTiers` off | identical | identical | identical | identical |
| W4 `InvestFromTheMask` **on** | 0-4-0 **−16.00** @314t (3) | identical | **0-3-1 −8.00** @479t (3) | 0-4-0 **−16.00** @315t (4) |
| W7 `TeamFlankJitter` **on** | — | identical | identical | identical |

### Per-rule verdicts

**W1 — stillness is the capture. KEPT; it is the wave.** The only rule that moves
a number on its own. Against the bulwark `iron-root` it is the difference between
**−6.00 surviving to tick 479 with 11.2 interrupts landed a match** and
**−8.50 breached at 370 with 3.0**, and against the fabricator `spark-line` it is
worth 3.50 of territory. The rule is one comparison in `ArcKeel`: claim weight
counts only bodies whose tile did not change, so `gain(with me standing still)`
against `gain(without me)`, scaled by the declared erosion multiple when an enemy
claim is what I would be eating into. When it says freeze, the four discretionary
movers — unmask, kite, step-aside, and walking between goal tiles — are refused,
and the tick goes to a rotation, a bolt, a cast or nothing. *Why it is worth most
against a bulwark:* that matchup is the one where arc-light actually gets to stand
on the point, and it is the one where wave 7 spent every one of those ticks
shuffling.

The derivation this rule needed is worth recording, because it is the only new
memory in the artifact. The observation publishes where every allied body **is**
and never where it **was**, so "did this tile change" has no answer for an ally
from one frozen observation. Every life receives the same allied body state, so
every life can accumulate the same previous-position map from its own
observations — and `ArcMemory.Close` is called on *every* return path, including
the early ones, because a map that skips a tick reports a two-tick-old tile as
"last tick" and the arithmetic built on it is then confidently wrong. A body with
no entry is treated as stationary, which is the engine's own rule for a life with
no previous position.

**W3 — a channeler is prey. KEPT, and it is the wave's honest split decision.**
Turning it off makes the still-water leg *survive longer* (320t → 415t at the
same −14.00) and the vector-edge leg *collapse sooner* (231t → 196t), and costs
1.50 of territory on spark-line. Net positive, and I am reporting the still-water
column rather than burying it: against that one opponent, a doctrine that
prioritises the body on the point over the body that is about to shoot me is
trading survival for tempo it does not convert. The rule is two credits computed
from `claimInterrupt`: the gun scores `revertPerDamagePoint × my damage × 25` for
a target standing on the active objective while its team owns the run, and the
cast counts each such body as one extra forecast body in the break-even. Both are
multiplied by the region test rather than added beside it, because damage one
tile off the objective reverts exactly nothing — which is the entire reason a
screen absorbs for free.

**W2 — do not eat a bolt while you channel. KEPT, and MEASURED INERT on all four
legs.** Records, end ticks, casts, interrupts and scrap are byte-identical with
and without it. The reason is a fact about this doctrine rather than about the
rule: arc-light is almost never the *controlling* team standing on the point with
a bolt inbound. It is the denier — and denial weight counts a mover, so wave 7's
unconditional objective-preserving shuffle was already the right answer for the
side this doctrine is usually on. I ship it on because it is contract-correct and
because the case it covers is exactly the one the doctrine is trying to reach, but
I am not claiming it won anything. *An inert rule reported as inert is worth more
than an inert rule reported as a win.*

**W5 — scrap is collected on the way. KEPT, small, and only visible on one leg.**
Inert on three legs and worth **+1.75 territory and nearly double the bank
(s8.0 → s15.8) against the fabricator**, which is the leg with the most corpses.
That is the rule working exactly as designed and no more than designed: it is a
detour budget, not a harvest, and the two rewrites it took to get there are in the
ledger above. The `siege` cell reproduces every `bastion` number exactly, which is
the cleanest possible statement of how small this doctrine's economy is.

**W6 — read the enemy's tier vector. KEPT, and MEASURED INERT for a stated
reason: nobody in this cohort buys anything.** Every opponent artifact I played
predates the `invest` verb, so no enemy edge tier ever appears and the effective
travel is always the declared travel. The rule is one addition in `ArcThreat`, it
is correct the moment an opponent does buy, and I would rather ship a correct
envelope than discover next wave that every lane, bearing and escape answer was
one tile short on the only tile that matters.

**W4 — buy from the mask. SHIPPED OFF, and this is the wave's expensive negative
result.** Two separate reasons, and they compound.
 *(a) The track this doctrine wants aborts the match.* See friction 1.
 *(b) What is left is a loss.* With gun travel refused, the mask offers sight and
 spawn health, and buying those costs the body its action tick for a tier applied
 only to the Prime slot: still-water −14.00 → −16.00, iron-root 1-3-0 → 0-3-1,
 spark-line −12.50 → −16.00. Measured purchases when on: 3–4 `optic` tiers and
 the occasional `plate` per 4-seed leg. A striker's binding constraint is its
 facing quadrant, not its sight radius, and the tier does not widen the quadrant.
 The eligibility arithmetic stays in `ArcScrap.Rank` with these numbers in its
 doc comment so the negative result stays auditable rather than becoming folklore.

**W7 — the shared flank bit. SHIPPED OFF; measured inert.** `context.TeamRandom`
is exactly the right instrument and the doctrine had exactly the wrong place to
put it. The draw is taken before any branch on private state, held for one
capture window (the contract's own threshold) in life memory, and used to mirror
the goal ordering. It changes nothing measurable, because the mirror only reaches
the **last two** sort keys of a seven-key ordering — choke, rally, fan lane, ally
clearance, distance and co-exposure all decide first, and by the time the
canonical `(y, x)` tie-break runs there is usually one candidate left. Against a
deterministic opponent that does not model me, unpredictability has no in-match
value anyway; its value is against an opponent that adapts, and there is none in
this cohort to measure against. Left in the source at `false` with this reasoning.

### Channel and economy usage, this build

Per match, `bastion`, 4 seeds a leg. `casts` is volley stance entries; `interrupt
hits` is damage this team landed on an enemy body standing on the active
objective while that enemy owned the running claim; `scrap` is bank plus tiers
bought; `invest` is zero everywhere by design.

| leg | casts | interrupt hits (w7 → w8) | scrap earned (w7 → w8) | invest |
| --- | --- | --- | --- | --- |
| striker mirror vs my wave 7 | 31.0 | 0.0 → 0.0 | 30.0 → 30.0 | 0 |
| vs still-water | 8.2 | 1.8 → 0.5 | 10.8 → 11.5 | 0 |
| vs vector-edge | 9.0 | 1.0 → 2.0 | 3.0 → 5.0 | 0 |
| vs iron-root | 0.0 | **2.8 → 11.2** | 9.2 → 13.5 | 0 |
| vs march-wall | 0.0 | 0.0 → 0.0 | 1.0 → 1.0 | 0 |
| vs spark-line | 0.0 | 6.2 → 7.5 | 9.2 → **15.8** | 0 |

### All four cells, and the not-overfitting check

This artifact is read in `swell` (neither arm), `siege` (channel only), `forge`
(economy only) and `bastion` (both), and every rule above branches on a contract
block that is **absent** in the cells that do not declare it.

| cell | resolved ruleset (striker mirror) | this build vs still-water, seed 11 |
| --- | --- | --- |
| `swell` | `…-swell-facing-locked` | breached 370t — **byte-for-byte the wave-7 result** |
| `forge` | `…-forge-facing-locked` | breached 371t (the errand fires once) |
| `siege` | `…-siege-facing-locked` | reproduces every `bastion` number exactly |
| `bastion` | `…-bastion-facing-locked` | the table above |

The `swell` identity is the check that matters: with no channel and no economy
declared, `ArcFacts.Channel` and `ArcFacts.Economy` are both null, every wave-8
rule short-circuits, and the artifact plays the wave-7 game to the same tick.
Nothing here is conditioned on an arm name.

Zero runtime faults and zero disqualifications across every match measured for
this freeze, verified programmatically from the replays' final participant state.
Zero blocked or otherwise non-successful actions beyond the occasional contested
`move`.

## Top 3 frictions

**1. `invest` on the gun-travel track ABORTS THE MATCH, and the abort names
nothing.** This is the wave's finding and it cost me the doctrine's best
purchase.

Buying a tier whose declared effect is `mobile-attack-travel-tiles-delta` kills
the whole match with

```
error: A retained projectile must preserve its exact resolved committed path. (Parameter 'projectiles')
```

exit code 1, **no replay written**, and no tick, no actor, no team, no
participant and no action named. From outside the engine the cause is legible —
"a purchase settles after every bolt has flown", so a bolt already in the air
carries a committed path resolved against a maximum travel the purchase then
changes under it — but I could only get there by construction, because the
message says nothing about *whose* projectile, *which* tick, or *what* changed.
Isolated by building four variants of one source and running nine identical
bastion matches each (three opponents × three seeds):

| build | aborted / ran |
| --- | --- |
| buys only the travel effect | **3 / 9** |
| buys only the spawn-health effect | 0 / 9 |
| never invests | 0 / 9 |
| my wave-7 predecessor, which has no purchase verb | 0 / 16 |

I then built and measured the obvious bot-side guard — refuse the purchase while
this team has a bolt in flight, which `visibleProjectiles` publishes with its
owner — and it is **not sufficient**: still 3 / 9, because a *teammate's* bolt
launched on the same tick as the purchase is a bolt no life can see when it
decides, and there is no fact in the observation that closes that window. So
there is no contract-driven bot-side fix, and the only safe answer for a
population instrument is to leave the tier in the bank.

Three things would each have saved this wave independently: **(a)** don't
re-resolve a retained projectile's committed path against a changed profile —
freeze it at launch, which is what "retained" already promises everywhere else in
this contract; **(b)** if the invariant must hold, make the *purchase* the thing
that is refused, as an ordinary `Blocked` on that tick, which is a grammar every
bot already handles; **(c)** at minimum, make the abort name the projectile, its
owner and the tick, and still write the replay — an engine invariant that fires
on legal play and produces no artifact is unbudgeted and unattributable. Note
which arm this is: `--capture channel --economy scrap` is described in the brief
as *the shipped game*.

**2. The scaffold's `ArenaBasics.Capture()` reports the channel as a binary
contest, and it is the one helper the whole arm turns on.** `CaptureRules
.SurplusWeightScalesGain` is computed as `controlPolicy.Contains("net-positive
-objective-weight-difference")`. The channel's policy ID is
`stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-opposition
-erodes-at-multiple-then-builds` — it scales gain, it says so in its own name,
and the helper returns **false**. A bot that trusts the scaffold on the arm the
scaffold shipped alongside prices every push as "one body nulls any number of
opposing bodies", which is the opposite of the truth. This is not a doc gap; the
classes brief is exact and I found it by reading the brief and disbelieving the
helper. **Either teach the helper the second policy, or make it return
`null`/throw on a policy it does not recognise** — silently answering `false` for
an unknown policy is the failure mode that turns a shipped helper into a trap.
The same file is where I would have wanted `ClaimWeight`/`DenialWeight` split out
of `ObjectivePresence`, since every channel decision needs the split and the
helper only returns the sum.

**3. Nothing publishes where an allied body stood last tick, and the channel
makes that the most important number in the game.** The claim counts bodies whose
tile did not change; `mode.captureProgress` tells me the result and never the
inputs. For **self** the answer is derivable (`previousActionResolution` plus my
own memory); for an **ally** there is no published previous position, no
`movedThisTick` flag, and no per-team claim/denial breakdown — so every bot on
this arm must build the same per-tick previous-position map out of its own
observation history, and every bot must remember to update it on every early
return or be wrong in exactly the way that is hardest to see. I built it
(`ArcMemory.MovedLastTick` + `Close`) and it is nine lines, but nine lines that
every entrant writes identically and one of which is a footgun is a scaffold
helper. **One boolean on `ObservedAllyState` — `movedThisTick` — or a
`claimWeight`/`denialWeight` pair on the Frontline mode observation, would delete
the memory entirely.** The economy got three well-designed new observation facts;
the channel, which rewrites the front for both teams, got none.

**Honourable mentions.** `qualify` still writes 36 `viewer.html` files nobody
asked for — `evidence/t4` arrived at **214 MB**, of which **193 MB was viewers**;
stripping them left 21 MB and did not change the report hash. `experiment` gained
`--viewer`/`--open` opt-in three CLI versions ago and `qualify` still has no
counterpart; this is the third wave of my DX asking. `coordinationGradeAwarded`
is still silently `null` beside `passed: true` and `tierAwarded: "T4"`, with
nothing in the packet, the rules card or `--help` saying which suite populates
it — fourth wave. `movement-blocked` still does not name what blocked you —
fourth wave, still one field wide. And `ArenaBasics.ClassOf` still recovers a
class by splitting form IDs on `-`, which the classes doc explicitly forbids now
that `ClassId` is published everywhere, with a doc-comment still promising a
replacement that shipped four waves ago.

One genuinely good thing, said plainly: **`upgrade-track` is the best-designed
new legality constraint I have consumed.** A track appears only when the bank
covers its next tier and no cap forbids it, so the bot never prices the ladder;
my whole affordability logic is zero lines. `TeamRandom`'s doc-comment is the
other one — it states the failure mode ("draw before you branch on private
state") in the place where the failure would happen, which is why my one use of
it is correct even though it turned out to be worthless.

## Strategy passes

One doctrine pass, exactly as commissioned: read the channel's control policy and
interrupt from the contract, derive the one bit the claim turns on, re-price
stillness and the dodge against it, make the interrupt a target and a cast
credit, read the enemy's tier vector, and give the economy a detour budget rather
than a harvester. Then a full leave-one-out from the shipped whole. The
coordination layer was not opened, `ArcMove.cs` was not touched, and every
wave-7 volley switch keeps its wave-7 value. Nothing in the frozen build was
tuned by eye: every number in this file was measured against my own rebuilt
predecessor, against wave-8 baseline artifacts I played but did not read, or
against my own single-flag ablations — and the three rules that measured inert
are reported as inert rather than as wins.
