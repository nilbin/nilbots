# DX notes — ledger-fly revision 8 (the channel, the economy, TeamRandom)

## Isolation statement

Written from this project's own sources, its own frozen **wave-6** predecessor
(two waves back — there is no wave-7 ledger-fly), its own qualification report,
and matches this entrant played against **its own rebuilt wave-6 source, its own
flag-ablated variants of this revision, and pre-built opponent artifacts**, and
nothing else. No other entrant's source file, `DX.md`, `README.md`, replay,
qualification report, standings table or aggregate balance report was opened.
No scratch directory other than my own was read or written.

Permitted material actually consulted:
`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`,
`docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md`, `docs/FRONTLINE-LABS-RULES.md`,
`docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (all read in full), the public SDK
types and XML documentation under `src/BotArena.Sdk/`,
`templates/botarena-generic-actor/`, my own frozen wave-6 directory
`arena-bots/frontline-labs/classes-wave-6-2026-07-30/ledger-fly/` (copied OUT to
scratch before building; left byte-untouched — verified by `cmp`), and
`sandbox/cli-publish/`.

| permitted document | sha256 | moved since wave 6? |
| --- | --- | --- |
| `FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` | `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` | unchanged |
| `FRONTLINE-LABS-RULES.md` | `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` | unchanged |
| `EXPERIMENTAL-FRONTLINE-CLASSES.md` | `e7e1f023ca696faf5d57103eba14b72744bc6fead8e44cfe87929efe8987409c` | **changed** (was `2333bd3c…`) — the channel, the economy and TeamRandom sections are new |
| `templates/botarena-generic-actor/ArenaBasics.cs` | `dfebec45d84c1e7e7a0a71362a3113bfb0bb94e5e61e6124769485618ba3f4b8` | **changed** (was `567e9faf…`) — carried byte-identical, see below |

Private scratch: `sandbox/ledger-fly-w8-scratch-3f9c62ae/` — uniquely named, not
a shared or guessable path.

### Four disclosures, in the spirit of the packet's exposure rule

1. **The cohort directory is still a shared parent of my output directory.**
   Creating my own freeze target puts it beside other entrants' directories. I
   opened none of them. Fourth wave running for this one; still cheap to fix by
   making per-entrant directories siblings of the cohort root rather than
   children of it.
2. **Finding the permitted opponent artifacts required listing directories that
   also contain forbidden material.** `sandbox/w8-baseline-0.10.10/*/out/bot.wasm`
   is permitted "artifacts only", but the artifacts sit inside per-entrant
   directories, so enumerating them printed other entrants' **file names** (not
   contents). I read no file in any of those directories except the `.wasm`
   artifacts, which I only ever passed to the CLI. If the intent is that a
   competitor never sees another's file layout, the baseline drop should be a
   flat directory of `<lineage>.wasm` blobs.
3. **I listed `arena-bots/frontline-labs/classes-wave-7-strikers-2026-07-30/`**
   (directory names only: `arc-light`, `still-water`, `vector-edge`). I did this
   because the commission asks for evidence against "a wave-8-baseline striker
   and bulwark artifact" and a bare `.wasm` carries no declared class, so there
   was no other published way to know which artifact plays which chassis. What I
   learned is a class assignment, not a strategy, a result or a standing. A
   `--print-candidate-contract` that reported an artifact's declared class, or a
   class suffix on the baseline filenames, would remove the need entirely.
4. **A mid-wave platform notice reached me from the orchestrator**, saying that
   the `A retained projectile must preserve its exact resolved committed path`
   abort I had independently hit and worked around was an engine defect, that it
   was fixed and republished, and that four sibling authors hit it too. That is
   the only thing I know about any other entrant's run, it is a host defect
   rather than a strategic or competitive fact, and I acted on it only by
   removing my workaround and re-measuring (below).

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Revision | 8 (wave-8 cohort; predecessor is wave 6 — this lineage has no wave 7) |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 (retained) |
| Budget | one doctrine pass integrating the capture channel and the scrap economy; mechanical and salvo-survival repairs free; the doctrine is not reopened |
| Predecessor | `arena-bots/frontline-labs/classes-wave-6-2026-07-30/ledger-fly` (untouched) |
| Primary game | `--classes fabricator-vs-striker --movement facing-locked --pendulum keel --skills kit --bend universal --five-slots wane --aim offset --stance-ground open --cooldown ticking --volley salvo --capture channel --economy scrap` |
| Resolved ruleset | `frontline-labs-1-fabricator-vs-striker-bastion-facing-locked`, rules fingerprint `c1e80fe7e8aedee31e5f1d98b1fb6e31ac2968642ea8f8eda252fa677c80eb81`, topology `two-team-one-controller-asymmetric-slots-4-3-v1` |
| Toolchain | nilbots CLI **0.9.27**, SDK **0.10.10**, game rules 0.5, runtime protocol 0.1 / actor 1.0, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Source-tree sha256 | `e6ef24c82134fbaea4e6559fffa2c1b4162c4cb557f548ad115328028c81b87f` |
| Build cache key | `7958e0d36616b2f7c935207454b8f233aa7951c8d07f0a9a1a02141fe4a142ab` |
| **`out/bot.wasm` sha256** | **`b4de0047ef9870e64fafaf62260633a795475da7fc134d294a2dc026a689a953`** (3,603,233 bytes) |
| Qualification | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, WASM, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `34a486b250158b2e97d1803af4d06b160574a3575d4804f5d2f071f07ba29c12` |
| T3 prerequisite report sha256 | `cb5302f00067f0ce5ebee6f0d5e9f64620bd344272c8ef806193bbe61b896028` |
| T2 prerequisite report sha256 | `2e9530ad256145ca7bd9a383e102bd922db93abfa3031cf138da8f50d83ea49f` |
| Verified probe replays | 36 under `evidence/t4/`, one spot-verified with `nilbots verify` (**OK**, stored hash `81776ffa5d2d45925ed4ef92afc1c1b732a47e768509a9467cc6694e08f9cb67`) |
| Sparring baseline | wave-6 source rebuilt `--no-cache` under CLI 0.9.27 → `f4c7e2497ba31d580fe944d2a70d5a59164b46d5ffc9c1b49fe012e34fd6f2ce` |
| **Rebuild proof** | `--no-cache` build **from the frozen tree**: same cache key, artifact **`b4de0047…`** — identical to the scratch artifact every number below was measured on |

Per-file sha256 of the submitted set is in `SHA256SUMS`. The source-tree hash
construction (name, NUL, big-endian 8-byte length, bytes, sorted, over `.cs` +
`.csproj` + `.json`) is carried unchanged from revisions 2–6 and was verified by
reproducing wave 6's recorded `63a6f2fe…` from its frozen directory before being
used here.

**Changed from wave 6:** `ArenaBasics.cs` (template swap), `Convoy.cs`,
`Field.cs`, `LedgerFly.cs`, `MatchLens.cs`, `botarena.json` (manifest
`sdkVersion` only — see friction). **New:** `Channel.cs`, `Doctrine.cs`,
`Scrap.cs`. **Byte-identical to wave 6:** `Bearings.cs`, `Coordination.cs`,
`FabricationRoute.cs`, `Gunnery.cs`, `Kinematics.cs`, `Ledger.cs`, `Ratchet.cs`,
`Stances.cs`, `LedgerFly.csproj`.

**The wave-6 artifact hash does not reproduce under 0.9.27, and that is expected
— but this time it can be cross-checked.** The frozen wave-6 source rebuilt
byte-for-byte gives `f4c7e249…` where wave 6 recorded `49f452a1…`; the staged
SDK/Guest bytes are part of the cache key by design, so a CLI bump invalidates
it. What is new is that `f4c7e249…` is **exactly** the artifact the orchestrator
shipped as this lineage's wave-8 baseline, so the divergence is a toolchain
version fact and not a source drift.

## The commission, and what actually shipped

The brief named three arms. Here is what each turned into, and what it is worth.

### 1. The capture channel

The channel does not change the doctrine's unit of account. A capture is still
`threshold / gain` ticks and a body is still its own slot's rebuild clock. It
changes **which ticks are convertible**, and the whole revision is that
sentence unpacked:

- **claim is stillness, denial is presence.** `Channel.cs` reads both weights
  each tick from the frozen observation. Stillness is read from the published
  `PreviousActionResolution` — a movement action whose outcome was `Success` —
  never from remembered positions, because private memory is life-scoped and a
  life born this tick would compute a different plan from its siblings. The rule
  counts a fresh body as stationary, and a body with no previous resolution *is*
  a fresh body, so the observation-only reading agrees with the rule on exactly
  the case memory could not cover.
- **the useful number of claimers is `their weight + the cap`,** not the cap.
  Getting this wrong was a measured, expensive mistake — see the ledger below.
- **the interrupt inverts wave 5.** Wave 5's `WorthTheHit` stood its ground on a
  contested tile because leaving handed the enemy an enemy-sole decay tick.
  Under a declared interrupt the arithmetic reverses: eating the bolt costs
  `damage × revertPerDamagePoint` of a run we already own, while a step *inside*
  the region costs one tick of gain and keeps the denial weight, which counts
  movers. So a claimer dodges in-region, and leaving the region entirely is
  priced ten times higher than it was.

### 2. The scrap economy

`Scrap.cs` reads the whole block. One scrap is priced in the doctrine's own
currency and nothing is tuned: a tier costs a declared number of scrap, a plate
tier is a share of the bank's body, the bank's body is its own return clock plus
every pipeline clock it stalls, so one scrap is the quotient. The ladder's order
comes from the contract's declared **effect policy IDs**, never from track names,
and every step has a published stopping condition:

1. `spawn-max-health-delta` while the prime's spawn health `<=` the biggest
   single contact any opposing form declares — against a salvo fan that lands 2
   on a 2-health prime this is the difference between a one-bolt kill and a
   two-bolt kill, and it is the only tier that pays on a body that stands behind
   the line;
2. `vision-range-delta` while sight `<` our own gun travel;
3. `mobile-attack-travel-tiles-delta` while our travel `<` the longest declared
   opposing travel;
4. then the remainder, so the pot is spent rather than admired.

Against a striker this resolves plate → optic → edge; in a fabricator mirror,
where the biggest declared enemy hit is 1, it correctly *skips* step 1 and opens
with optic. `purchaseMode` is read, so on the `scrap-flat` control level the
purchase routine is simply absent.

**The courier line is complete, gated three ways, measured, and shipped OFF.**
See the ledger. What ships instead is the assay: stepping onto a wreck banks one
scrap at the tile with no transport, which is worth about **9 scrap a match**
with no allocation cost at all, and funds one or two tiers.

### 3. TeamRandom

The template's `OrderedDirections` now draws from `context.TeamRandom` instead of
the per-life stream. I carried the new helper **byte-identical** rather than
patching it. This matters more than it looks for this bot specifically: wave 6's
whole coordination layer projects each sibling's route and reserves the tiles it
needs, and those projections were computed with a team-wide fixed order while
each body actually walked with its own private tie-break. Team-shared ordering
makes the projection and the walk agree. Measured at **+4.7** territorial margin
against the wave-8 bulwark artifact and **0.0** in the other two cells (row
`w6order` below).

I did **not** find a use for TeamRandom beyond this. The obvious one — an
unpredictable-but-shared lane assignment across four bodies — is already solved
better by `Convoy`'s priced right-of-way, which is a total order derived from
declared clocks, and replacing a priced decision with a coin flip is not
something this doctrine can justify. That is a deliberate refusal, not an
omission.

## Per-rule attribution

Three cells that disagree with each other, 6 disjoint seeds each, all replays
distinct (`distinct = n` throughout, so no seed is double-counted):

- **bastion** — `fabricator-vs-striker`, full arms, against my own rebuilt
  wave-6 source;
- **redoubt** — the fabricator mirror with full arms, same opponent;
- **bulwark** — `bulwark-vs-fabricator`, full arms, against the wave-8
  `march-wall` artifact.

Every row is one build differing from the shipped tree by exactly one line in
`Doctrine.cs` (or, for `w6order`, by the template helper). Margins are signed
territorial progress, mine minus theirs, averaged over the 6 seeds.

| build | bastion | redoubt | bulwark |
| --- | --- | --- | --- |
| **SHIPPED** | W6 L0 D0 **+32.00** | W0 L0 D6 **+0.00** | W3 L3 D0 **−1.33** |
| all six lines off | W6 L0 D0 +32.00 | W0 L3 D3 −10.67 | W2 L4 D0 −16.00 |
| no-Still | +32.00 | W0 L6 D0 −2.00 | W0 L6 D0 −23.33 |
| only-Still | +32.00 | W3 L3 D0 +9.00 | W1 L5 D0 −14.33 |
| no-Escort | +32.00 | +0.00 | W1 L5 D0 −16.00 |
| only-Escort | **W0 L6 D0 −32.00** | −10.67 | −16.00 |
| no-Interrupt | +32.00 | +0.00 | −1.33 |
| only-Interrupt | +32.00 | −10.67 | −16.00 |
| no-Invest | +32.00 | W3 L3 D0 +11.00 | −1.33 |
| only-Invest | +32.00 | W6 L0 D0 **+26.00** | W0 L5 D1 −20.67 |
| no-Lethal | +32.00 | +0.00 | −1.33 |
| only-Lethal | +32.00 | −10.67 | −16.00 |
| **courier turned ON** | +32.00 | W5 L1 D0 +6.00 | **W0 L6 D0 −23.67** |
| wave-6 direction order | +32.00 | +0.00 | W2 L4 D0 −6.00 |

Leave-one-out (shipped minus the build without that line; positive = the line
earns its place):

| line | bastion | redoubt | bulwark | sum |
| --- | --- | --- | --- | --- |
| **still** | 0.0 | +2.0 | **+22.0** | **+24.0** |
| **escort** | 0.0 | 0.0 | **+14.7** | **+14.7** |
| **interrupt** | 0.0 | 0.0 | 0.0 | **0.0** |
| **invest** | 0.0 | **−11.0** | 0.0 | **−11.0** |
| **lethal** | 0.0 | 0.0 | 0.0 | **0.0** |
| **team-shared order** | 0.0 | 0.0 | +4.7 | +4.7 |
| courier (cost of turning it ON) | 0.0 | +6.0 | **−22.3** | **−16.3** |

Six honest readings from that table, including the ones that do not flatter it.

1. **`bastion` is saturated and discriminates almost nothing.** Every build
   except one scores exactly +32.00 with six wins. Against my own two-wave-old
   predecessor in its own cell, the ceiling is reached before any single line
   matters. The one build it *did* catch is worth the whole cell (see 3). I
   report the cell because it is the commissioned one, not because it is
   informative.
2. **Still is the revision.** +22.0 against the bulwark on leave-one-out, +19.7
   over the floor on add-one-in in the mirror. It is also the largest *negative*
   anywhere I measured: in a fabricator mirror **with the economy arm absent**
   (`sap`) it costs **−21.3** against the same opponent — `only-Still` there
   scores −32.00 where the all-off floor scores −10.67. Standing still is the
   only way to take ground and the reason you get shot, and which of those
   dominates depends on the cell. I tried gating it on a declared roster
   surplus; that fixed the mirror (−32 → −8) and cost −35.3 in the mirror *with*
   the economy, so it is not shipped and the raw line is.
3. **Escort alone is catastrophic; escort with still is free.** `only-Escort`
   loses `bastion` **0–6 at −32.00** — a body pulled off the point while nobody
   is holding it still is simply a body missing from the front. In combination
   it is +14.7 against the bulwark and 0.0 elsewhere. Anyone porting this should
   treat the two as one rule, not two.
4. **Interrupt and Lethal both measure exactly 0.0, in all three cells, both
   ways.** Not "small" — zero. I ship them because each is a correct reading of
   a published rule that the shipped opponents happen never to punish, and I say
   so rather than claiming a number.
5. **Invest is not separable and its leave-one-out is negative.** Alone it is the
   single biggest positive in the table (+36.7 over the floor in the mirror);
   removed from the full build it *improves* the mirror by 11.0 and changes
   nothing elsewhere. That is an interaction with Still, not an effect. I ship it
   — the shipped build still draws 6/6 in that cell against a null of −14.67, and
   it is the only line that reads the store at all — but the −11.0 is on the
   record.
6. **The courier is a measured loss and is shipped off.** −22.3 and 0W6L against
   the bulwark, +6.0 in the mirror, 0.0 elsewhere. The line is complete: one
   body at a time, named by a rule every life evaluates identically off the
   frozen observation, gated on a declared roster surplus, on no enemy claim
   standing, and on not being behind on the only channel that scores. It fetched
   85 scrap and 2.7 tiers a match against a live striker. It still costs more
   ground than the tiers buy back, and the doctrine's own rule — never sell a
   body for less ground than it costs to replace — decides it. The code stays in
   the tree as the instrument that measured it, switched off, with the number
   written down.

## Results

### Versus my own wave-6 self, rebuilt from my own frozen source

All four commissioned cells, `fabricator-vs-striker`, 6 disjoint seeds each, WASM
runtime, all replays distinct:

| cell | ruleset | record | mean margin |
| --- | --- | --- | --- |
| `bastion` (salvo + channel + scrap) | `…-fabricator-vs-striker-bastion-facing-locked` | **6W 0L 0D** | **+32.00** |
| `siege` (salvo + channel) | `…-siege-facing-locked` | **6W 0L 0D** | **+32.00** |
| `forge` (salvo + scrap) | `…-forge-facing-locked` | **6W 0L 0D** | **+60.00** |
| `swell` (salvo only) | `…-swell-facing-locked` | **5W 1L 0D** | **+32.33** |

**23W 1L 0D over the four cells.** The `swell` row is the graceful-degradation
check that matters most: with neither new arm present the revision is not merely
un-broken, it is ahead by the same margin — every wave-8 line is inert by
*reading* on a contract that does not declare it, not by a flag.

### The fabricator mirror, where it is weakest

Both sides, against the same opponent, beside the null (wave 6 against itself on
the identical cell and seeds), which is itself strongly side-asymmetric:

| cell | shipped | wave-6 null | delta |
| --- | --- | --- | --- |
| `redoubt` (mirror, both arms), team 0 | 0W 0L 6D **+0.00** | 1W 4L 1D −14.67 | **+14.7** |
| `redoubt`, team 1 (`--swap`) | 0W 6L 0D −32.00 | +14.67 | **−46.7** |
| `sap` (mirror, channel, no economy) | 0W 6L 0D −32.00 | 1W 4L 1D −14.67 | **−17.3** |

This is the revision's real weakness and I am not going to dress it up. In a
fabricator mirror the surplus this doctrine is built around does not exist by
construction — two kiting defenders hold three stationary attackers — and the
still line, which is worth +22 against a shallower roster, is worth −21 here.
The economy partly pays for it (`redoubt` is +14.7 over the null; `sap`, the same
cell with the economy removed, is −17.3 under it), which is a coherent story:
stillness is affordable on a body that bought the health to survive being shot.
The team-1 row is worse than the team-0 row by 32 points on both the shipped
build and the null, so a large part of that column is the cell, not the bot.

### Cross-class, against wave-8 baseline artifacts

Each candidate row is paired with the **same match-up played by my own rebuilt
wave-6 source** on the same 6 seeds, so the comparison is against a null rather
than against zero. I am the fabricator in every row.

| opponent (class) | shipped | wave-6 null | delta |
| --- | --- | --- | --- |
| `still-water` (striker) | **6W 0L 0D +32.00** | 1W 0L 5D +5.33 | **+26.7** |
| `arc-light` (striker) | 5W 0L 1D +13.33 | 6W 0L 0D +32.00 | **−18.7** |
| `march-wall` (bulwark) | **3W 3L 0D −1.33** | 2W 3L 1D −10.67 | **+9.3** |
| `iron-root` (bulwark) | 0W 6L 0D −32.00 | 0W 6L 0D −32.00 | 0.0 |
| `gate-stone` (bulwark) | 0W 6L 0D −32.00 | 0W 6L 0D −6.00 | **−26.0** |
| `spark-line` (fabricator) | 6W 0L 0D +32.00 | 6W 0L 0D +32.00 | 0.0 |

Aggregate: **20W 15L 1D** for the revision against **15W 15L 6D** for its
predecessor over the same 36 matches — the revision converts draws into wins and
loses the same number. `still-water` is the artifact the wave-7 read says took
two seeds off my predecessor; it now loses all six. Against the bulwark chassis
I remain behind on aggregate, and against `gate-stone` I am 26 points worse than
wave 6 — the honest summary is that this revision improved decisively against
strikers and its own class, improved against one bulwark, and regressed against
another.

### Channel and economy usage, from the replays

Per match, in `bastion` against wave 6 (6 seeds):

| statistic | mine | theirs |
| --- | --- | --- |
| captures completed (front advances) | **5.0** | 3.0 |
| channeling ticks / gain | 64.0 / 66.0 (mean multiplier 1.03) | 30.0 / 30.0 |
| progress reverted by damage on the point | **1.0** | 4.0 |
| scrap banked | 9.0 | 6.0 |
| invest decisions | 0–1 (`plate`) | 0 |
| scrap carried / courier interceptions suffered | 0 / **0** | 0 / 0 |
| refused steps (self-obstruction) | 3.0 | 1.0 |

Against `still-water` on `bastion`: 5.0 advances a match to 3.0, **1.0** progress
reverted a match against their 4.7, one `plate` tier bought every match at tick
322, 14.5 scrap banked a match. In `forge` (economy without the channel) the
ladder gets further — `plate` **and** `optic` every match — because matches run
longer. The `edge` tier is never reached: with the courier off, assay income
funds one or two tiers, not three. That is a real finding about the arm, not a
limitation of the routine — **the fixed pot is only reachable by a team willing
to pay a body for it, and this doctrine measured that body as too expensive.**

Reverts suffered are the one number I would point at as evidence the channel
lines do what they claim: **1.0 per match against 4.0–4.7 for opponents playing
the same objective**, which is the interrupt reading and the escort geometry
doing their job even where the margin cannot see them.

### Distinct outcomes, stated plainly

Six seeds per cell, `distinct-replay-hash` count equal to the seed count in
**every** run reported above — so no line is inflated by re-counting one match.
But the seeds barely move this game: in the four commissioned cells, six seeds
produced **one** distinct margin in three of them and three in the fourth. Read
every "6 seeds" as *six confirmations of one or two stories*, not as six
independent observations. That was true in wave 6 and it is more true now; the
cross-class table above is the only place in this report where seed variance
carries real information, and that is why it is the table with the nulls.

## Friction

### 1. The `invest` verb aborted the match, and only a bot that reads its mask could find it

The first genuinely new player verb since Split had a host-side defect: buying
the `edge` (gun-travel) tier while a bolt was in flight aborted the match with
`A retained projectile must preserve its exact resolved committed path.
(Parameter 'projectiles')`. I hit it on my first six-seed sweep, bisected it to
`invest` (matches completed with the line off), then to the travel track
specifically (matches completed with every other track allowed), and shipped a
workaround that declined the track outright — because a bot **cannot** rule the
precondition out by looking: our own bolts are in `visibleProjectiles` only while
some allied sensor covers them, and a facing-quadrant union does not cover a bolt
flying away behind us. My narrower "no own bolt visible" guard did not stop it,
which is exactly that gap.

The orchestrator later reported the defect fixed and republished; I removed the
workaround, rebuilt, and re-measured all eight affected cells. Every margin was
**identical** to the workaround build, because with the courier off the ladder
never funds a third tier — so the workaround cost me nothing measurable and the
shipped artifact has the correct rule.

Three things worth keeping from this:

- **The abort was reachable only by a contract-driven bot.** A bot that ignores
  the economy, or that hard-codes a track order and happens to stop at two,
  never touches it. The arm shipped to a cohort in which most artifacts never
  cast the verb at all.
- **Some abort paths returned exit code 0 with the error on stdout.** My first
  sweeps had cells with 3 or 5 replays where I had asked for 6, and nothing in
  the exit status said so. Every number in this report comes from a run whose
  replay count I checked explicitly; I would not have caught that without
  counting. A sweep that aborts should not be able to look like a sweep that
  finished.
- **An aborted cell measures nothing, and it is very easy to read it as a loss.**
  My reporting script identifies my own team by artifact hash and counts files,
  so a truncated run showed up as a smaller `n` rather than as defeats. That was
  luck in the design, not foresight.

### 2. `ArenaBasics.Capture` reports the channel as "surplus does not scale gain"

The template helper decides `SurplusWeightScalesGain` by testing whether
`controlPolicy` contains `net-positive-objective-weight-difference`. The channel
policy is `stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-…`
— which scales gain with surplus, and does not contain that substring. So a bot
that reads the shipped helper is told the exact opposite of what the arm does,
on the arm the brief describes as rewriting the front. My wave-6 code was
conditioned on that flag in three places; I read the raw
`FrontlineCapture.ControlPolicy` instead. The helper should test for gain
scaling generally, or return the policy ID and let the caller decide.

### 3. `carriedScrap` is on the observation but not on the authoritative life

Reading the replay to measure my own courier, I looked for `carriedScrap` on
`tickStart.state.activeLives[]` — it is not there. It exists on the
**observation's** `self`, on allies, and on visible enemies, which is correct for
the bot but means the load is only recoverable from a replay by joining actor
turns rather than by reading authoritative state. Anyone writing replay analysis
for this arm will lose the same hour I did, silently, because the missing key
defaults to zero and the resulting table looks plausible.

### 4. `botarena.json` carries an `sdkVersion` that nothing uses

My wave-6 manifest declared `0.10.4` while the SDK was 0.10.6, and it built
fine. I bumped it to `0.10.10` this wave and the build cache **hit** the previous
key with an identical artifact — so the manifest is not in the cache key and the
declared version is neither used nor validated. Either validate it or drop it;
right now it is a field whose only function is to be wrong.

### 5. The channel's three new contract fields are inert-omitted, and the SDK says so clearly

Credit where it is due: `stationaryGainMultiplierCap`, `opposingErosionMultiplier`
and `claimInterrupt` are absent on rulesets that do not channel, the XML
documentation states that "zero means the field is inert and absent", and
branching on presence gave me one code path that plays every arm. Same for
`scrapEconomy`, `ScrapTeams`, `ScrapPiles` and `carriedScrap`, all of which are
empty-or-zero rather than null on a ruleset without the economy. This is the
part of the brief that cost me the least time, and it is the part with the most
new surface.

### 6. `PreviousActionResolution` is the only observation-pure way to read stillness

The channel gates on "did this body change tile this tick", and there is no
published `previousPosition`. For my own body I could remember it; for allies I
could not, and a life born this tick has no memory at all — which is exactly the
case the rule singles out as stationary. `PreviousActionResolution` on self and
on every ally closes it: an accepted movement action whose outcome was `Success`
moved, and everything else did not, including a `Blocked` move, which is
precisely the rule's own wording. It took a while to notice that the field
already answers the question. A sentence in the channel section pointing at it
would save every author the same search.

### 7. Freeze-tree hygiene, carried forward, plus one divergence

`nilbots build` globs every `.cs` under the project directory, so an ablation
archived inside a freeze tree makes that tree fail to rebuild with
duplicate-member errors. All fourteen ablation and instrument builds live in my
scratch directory; the freeze contains only the submitted set plus `evidence/`.
Rebuilt `--no-cache` from the frozen tree as the final act: same cache key, same
artifact hash.

One deliberate divergence from wave 6: I **deleted the 36 `viewer.html` files**
from `evidence/`, which took the directory from 214 MB to 21 MB. Wave 6 kept them
and cost 213 MB; the host is at 91 % disk and the brief says to watch it. The
replays are the verifiable artifact and every one of them is retained; a viewer
is regenerable from a replay at any time.

## Budget ledger

| item | spent | notes |
| --- | --- | --- |
| doctrine pass | 1 | the channel, the economy, TeamRandom, integrated over the wave-6 coordination layer |
| mechanical/contract repairs (free) | 3 | template helper swap; the `ArenaBasics.Capture` misreading; the `invest` abort workaround and its later removal |
| salvo-survival repairs (free, in scope) | 1 | the lethal line, authored as a wall, measured, repaired to a priced yield |
| measured re-authorings inside the pass | 3 | the courier's allocation gate; the berth cap corrected from `cap` to `denial + cap`; the still line's surplus gate, built, measured at net −13.3 and **not** shipped |
| doctrine reopened | 0 | not one line of the unit of account changed |
| artifacts built | 22 | 1 shipped, 1 wave-6 baseline, 20 instruments — all instruments in scratch |
| matches played | 966 | all against my own predecessor, my own instruments, or pre-built opponent artifacts |
| qualification runs | 2 | one on an intermediate artifact, one on the shipped one; both exit 0, both T4 |

Every strategic number in this report was produced **after** the source was
frozen in the sense that matters: no opponent's source, standings or replays
were consulted at any point, and the only feedback loop was against my own
predecessor, my own flag-ablated variants, and artifacts whose insides I never
opened.
