# DX notes — spark-line revision 7 (fabricator, `spark-line-v1`)

Population: Frontline Labs classes wave 8, revision 7. Role: verdict-doctrine,
target cumulative T4. Revisions 1–6 are frozen under
`arena-bots/frontline-labs/classes-wave-{1,1-revision-2,1-revision-3,4,5,6}-…/spark-line/`
with their own DX notes; those are not restated except where a friction changed
status.

## Isolation statement

This pass read **only**:

| Material | sha256 |
| --- | --- |
| `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md` | `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| `docs/FRONTLINE-LABS-BOT-AUTHOR-PACKET.md` | `d3ea99a318bf932a63b9b0231c7e8fbb93cadc265a84cd42d4945befb439fc12` |
| `docs/FRONTLINE-LABS-RULES.md` | `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` | `e7e1f023ca696faf5d57103eba14b72744bc6fead8e44cfe87929efe8987409c` |
| `templates/botarena-generic-actor/ArenaBasics.cs` | `dfebec45d84c1e7e7a0a71362a3113bfb0bb94e5e61e6124769485618ba3f4b8` |

plus `src/BotArena.Sdk/` public types and XML documentation (`ActorDecision`,
`GenericActorActionArgument`, `GenericActorActionLegality`,
`GenericActorContext`, `GenericActorRulesContract`,
`GenericActorResolvedMatchContract`), this entrant's own frozen wave-6
directory (`arena-bots/frontline-labs/classes-wave-6-2026-07-30/spark-line/`,
copied out before any edit), replays this entrant generated in this session,
the resolved contract embedded in those replays, and the CLI at
`sandbox/cli-publish/`.

**Not** read: any sibling entrant's directory contents, `docs/DECISIONS.md`,
any aggregate balance report, Engine/App/Cli source, or any other scratch.
Sibling artifacts under `sandbox/w8-baseline-0.10.10/*/out/bot.wasm` were played
as opaque WASM only. `ledger-fly` — the sibling fabricator — was **not** played
and its directory was not opened at all.

Two exposures to disclose exactly, both incidental:

- Locating the baseline artifacts required `ls` on
  `sandbox/w8-baseline-0.10.10/*/`, which printed sibling **file names** (e.g.
  `ArcBoard.cs`, `StoneCrew.cs`). No sibling file was opened, and no name
  informed any decision here. A directory containing only `out/bot.wasm`, or a
  published manifest of artifact paths, would make even that impossible.
- The harness injects the repository's `CLAUDE.md` as ambient context rather
  than as a file I opened. It is project-wide agent guidance about engine and
  App architecture; nothing in it concerns another entrant and nothing in this
  revision derives from it. Recorded because material I did not choose to read
  is still material.

Scratch was a uniquely named private directory,
`sandbox/spark-line-w8-scratch-7a41c9e3/`, never a shared or guessable path.
Writes went only there and to the assigned output directory. Nothing was
committed to git.

## Freeze identity

| Item | Value |
| --- | --- |
| Entrant | `spark-line` (entry type `SparkLine`), revision 7 |
| Class | `fabricator`, declared in `botarena.json`, unchanged since revision 1 |
| **`out/bot.wasm` SHA-256** | `55ca1c3b339724e9cefdd2c07c91c8d5f6ed195542156293c4d17b591c2ce334` |
| Reproduction | `nilbots build <frozen tree> --no-cache` run **five times** from the frozen directory — cache miss, compiled, every time — and the artifact hash was **identical every time** and identical to the artifact every measurement in this file was taken with (`sandbox/spark-line-w8-scratch-7a41c9e3/w8-src/out/bot.wasm`, same sha256). Successive comment-only edits moved the cache key through `f86a2281cc9ad433e0f4e1d52f625a681a6244e46b60f233b8712cc5b9f499db`, `371f5a7d626b43735808b1bb8a55c20b470aa86a219b54ad083f0f0a33b2871d` and finally `e2ab391ad8f52cf8608b210044a6cbe8e97a8615a3a1c8da6e539c6b5924b0f4` — three different cache keys, one artifact hash. **Comments move the cache key and not the codegen**, the same observation wave 6 recorded, and the frozen tree's key is the last of the three. |
| Source-tree hash | `ac7c301612df8e259b95d369eeb406e39b7b73f5ea15ce482d42ab2f593083ef` (sha256 of the sorted per-file sha256 lines for `*.cs`, `*.csproj`, `botarena.json`) |
| Per-file digests | `SHA256SUMS.txt` |
| Builder | CLI 0.9.27, SDK 0.10.10, game rules 0.5, runtime protocol 0.1, actor protocol 1.0, NativeAOT-LLVM 10.0.0-rc.1.26306.1, wasi-wasm p1 core module, platform-matched Docker builder (macOS host) |
| Qualification | `experiment frontline-labs qualify --suite frontline-qualification-5` → **exit 0**, tier **T4**, `passed: true`, `profileComplete: true`, `balanceEvidenceEligible: true`; all five probes PASS plus the hash-linked T3 prerequisite (`frontline-qualification-4`, `tierAwarded: T3`, `passed: true`) |
| Report | `evidence/t4/qualification.json`, sha256 `6bdcb3d2090eec0a4dc09482a6abe9596cfc438ad63ffb456a0ece5f691c9d2a` |
| Submitted source | `SparkLine.cs` (4007), `ContractLens.cs` (1024), `Squad.cs` (576), `Supply.cs` (408, **new**), `Tactics.cs` (394), `Channel.cs` (289, **new**), `Doctrine.cs` (41, **new**), `ArenaBasics.cs` (1220, template verbatim) |
| Sparring baseline | wave-6 source **rebuilt** on this SDK from the frozen tree → `fe9da90c54bfcadfa21a645a750c284de612a6b397b63af38106554110103566` (cache hit on key `fbada05b…`). The wave-6 artifact **as frozen** is `fc397fc5…`; it was built on CLI 0.9.22 / SDK 0.10.6 and is a different artifact, so only the rebuild is a fair opponent and only the rebuild was played. |

## Budget ledger

| Item | Spend |
| --- | --- |
| Doctrine passes | **1** (the commissioned pass; this file's rules 1–8) |
| Mechanical/contract repairs (free) | `ArenaBasics.cs` re-synced to the template (`OrderedDirections` now draws `context.TeamRandom`); `TryBuildArguments` taught the `upgrade-track` parameter kind, which it silently skipped — the last-resort builder would otherwise submit `invest` with no argument |
| Salvo-survival repair (explicitly free) | LETHAL LANES + BREAK THE LANE, and the plate-first rung of the ladder |
| Within-pass corrections after measuring my own variants | 4, all inside the one pass and all recorded under *measured and rejected* below |
| Builds | 1 baseline rebuild, ~14 candidate builds, 8 leave-one-out variants ×2 generations, 6 probe variants, 2 no-cache freeze rebuilds |
| Matches | **228** WASM matches in the reported final round (bastion + siege, 0 aborted), plus roughly the same again in rounds discarded when the engine fix landed, plus two T4 qualification runs |
| Seeds | 104729, 3001, 55501 (disjoint), on every reported cell |
| Wall clock | ≈4 h, of which ≈35 min compiling and ≈2 h in matches |

## T4 evidence

```
Qualification suite: frontline-qualification-5
Profile:             frontline-duel-depth-union-t4-v1
Artifact:            spark-line [55ca1c3b3397…]
prerequisite T3       PASS
suppression-choke     PASS
entry-initiative      PASS
prediction-chamber    PASS
front-rotation        PASS
map-holdout           PASS
Tier awarded:         T4          (exit 0)
```

Report and every probe replay are under `evidence/t4/`.

## Per-rule attribution — leave one out, on the shipped composition

Cell **bastion** (`--movement facing-locked --pendulum keel --skills kit --bend
universal --five-slots wane --aim offset --stance-ground open --cooldown
ticking --volley salvo --capture channel --economy scrap`). Six opponents ×
three disjoint seeds = **18 matches per variant, 0 aborted**. `diff` is summed
signed territorial progress (mine − theirs); one breach is ±32.

Each variant is the shipped tree with exactly one `Doctrine` switch flipped and
nothing else changed. Raw table: `evidence/ablation/f3-loo-bastion.txt`.

| Variant | diff | W-D-L | rule is worth |
| --- | ---: | :---: | ---: |
| **SHIPPED** | **−286** | **4-2-12** | — |
| no STILLNESS | −494 | 1-0-17 | **+208** |
| no STANDOFF | −430 | 1-0-17 | **+144** |
| no CHANNEL WEIGHT | −416 | 3-0-15 | **+130** |
| no LETHAL LANES | −384 | 3-0-15 | **+98** |
| no WALK THE POINT | −344 | 4-0-14 | **+58** |
| no SCREENS | −286 | 4-2-12 | **0 — inert** |
| no INVEST | −176 | 6-0-12 | **−110** |
| no SUPPLY | −166 | 6-1-11 | **−120** |

Four things in that table are worth more than the ranking.

**The composition does not decompose, in both directions.** Removing INVEST and
SUPPLY *together* measured **−352** — worse than removing either alone and
worse than shipping both. Two rules that each look like a net cost are not a
cost the team can bank by dropping both, and a leave-one-out delta is therefore
a statement about the margin and not a licence to subtract.

**SCREENS is exactly inert and I can say why.** A surplus body only reaches the
screen branch when the team's weight on the point already exceeds what the
capture arithmetic wants, and on a two-to-four-body fabricator against a
three-body opponent that condition is reached in a handful of ticks per match.
The rule is correct, cheap, and currently unreachable; it is shipped because the
five-slot cells it was written for are the ones this class is heading toward,
and it costs literally nothing measured.

**The resolution is coarser than the numbers look.** Eighteen matches produced
eighteen distinct replay hashes — the seed genuinely moves the trajectory — but
all three seeds agreed on the outcome for five of the six opponents. So there
are effectively **six independent observations**, each worth ±32, and a
difference of one breach is the smallest thing this instrument can see. Read
±32 as noise; read ±100 as signal.

**INVEST and SUPPLY are net-negative at the margin, and that is the pass's least
comfortable finding.** Both are shipped anyway, for the reason above — the
both-off build is worse than either one-off build — and the fact is stated in
the verdict below rather than hidden.

### Rules measured and rejected inside this pass

Every one of these was authored, built, measured against the same opponents, and
removed. None of them is in the leave-one-out table above — that table prices the
rules that SHIPPED. These are sequential A/Bs across generations of the tree, and
where the attribution is not clean the row says so.

| Rejected rule | What it did | Measured |
| --- | --- | --- |
| **Stack to the cap against a live defence** | weight target = enemy denial + the declared stationary cap, which is what the gain formula reads like if you stop at the formula | **−94** on a two-seed round, and it starved SCREENS by never leaving a body spare. Replaced by "against a broken defence stack, against a live one screen", now worth **+130** |
| **A dedicated scrap courier** | elected from the body furthest from the objective, timed to the deposit schedule, banking a full 6-scrap deposit | **−56**. The pot is fixed, so the second walker buys no income at all — only absence, on a board where two movers hold three stationary attackers |
| **"Alone" meaning alone on the TILES** | stand off while no ally is standing on the region | a deadlock I could read off the replay before I could measure it: two bodies both off the region both stood off, each waiting for the other, and the point went uncontested for whole matches. The two opponents that never contest the centre early were conceded outright. Not a clean A/B — the fix landed with the gate change beside it |
| **Leaving the region for cover** | BREAK THE LANE allowed to step off held ground when every region tile was covered | the body left a point it held with a live claim and never came back. Replaced by "reposition inside the region or not at all" |
| **`invest` at the very bottom of the order** | cast only on a tick that was going to be a Wait | **−132**, but in a probe that also tightened the pile detour, so the attribution is not clean. The clean A/B is the other direction: `invest` ABOVE the lethal dodge instead of below it cost **24** over the same eighteen matches, and the shipped placement is the middle of the three |

## Results vs the wave-6 self, and cross-class

Same cell, same seeds, same instrument. Wave 6 is its own frozen source rebuilt
on this SDK. Raw: `evidence/ablation/f3-cmp-bastion.txt`,
`evidence/ablation/f3-cmp-siege.txt`.

**bastion** (channel + scrap), 15 shared matches:

| opponent | wave 6 | revision 7 |
| --- | ---: | ---: |
| arc-light (striker) | −96 · breach at t52 on all three seeds | **−56** · **+8 win** at t499, then breaches at t446 and t245 |
| vector-edge (striker) | −96 · breach at t51 | −96 · breach at t52 |
| still-water (striker) | **+96** · win at t247 on all three | −64 · breach at t182, a t499 draw, breach at t182 |
| march-wall (bulwark) | −28 · t499 | **+58** · **breach win** at t444, a t499 draw, +26 at t499 |
| gate-stone (bulwark) | −96 · breach at t60 | −96 · breach at t231 on all three |
| shared total | **−220** | **−254** |
| mirror vs wave 6 | — | **−32** · breaches against me at t410 and t406, a breach **win** at t355 |

**siege** (channel, no economy), same 15: wave 6 **−220**, revision 7 **−320**.

**The verdict, stated plainly: this pass did not net-improve the lineage.** It
is 34 territorial worse than its predecessor on bastion and 100 worse on siege,
and it loses the mirror 1-0-2. What it did do is exactly what it was commissioned
to do and nothing more:

- **The named failure is fixed.** The salvo striker that breached this lineage at
  tick 52 on every seed now takes 245–499 ticks and does not breach at all on
  one of them. The prime survives to its first slot unlock instead of dying at
  tick 10, which is the single fact the whole revision was about.
- **The bulwark matchup flipped**, −28 → +58 on bastion and −28 → +64 on siege.
- **It paid for that with `still-water`**, +96 → −64. That opponent does not
  contest the centre early, and STANDOFF concedes ground to an opponent who was
  never coming for it. The gate I added — stand off only while genuinely the
  only weighted body on the team — bounds the concession to the pre-unlock
  window but does not remove it. A gate that also asked "is anything actually
  threatening this ground" cost the arc-light gain when I measured it, and I did
  not find a formulation that kept both inside this budget.
- **The mirror loss is the honest cost of caution against a chassis with no
  heavy gun.** In a fabricator mirror the catalog's heaviest bolt is 1 and this
  prime has 2 health, so LETHAL LANES and STANDOFF correctly switch themselves
  off — the −32 is bought by CHANNEL WEIGHT, STILLNESS and INVEST alone, and
  `no-INVEST` recovers it (+12). It is the same margin as the ablation table's
  least comfortable row, seen from the other side.

I am reporting this as a **refutation, not a win**: on this opponent set the
fabricator's two-economies thesis does not pay, and the class's answer to the
salvo is survival rather than numbers. A wave-9 author should start from the
STILLNESS and STANDOFF rules (+208 and +144, the two largest effects in the
lineage's history) and re-derive the economy from scratch.

## Economy and channel usage

Measured from the shipped artifact's own bastion replays (`evidence/ablation/stats.py`
regenerates it from any replay):

| opponent (seed 104729) | advances mine/theirs | reverts suffered | `invest` casts | peak bank | tiers bought | peak carry |
| --- | :---: | :---: | :---: | :---: | --- | :---: |
| arc-light (**+8 win**) | 7 / 7 | 2 | 2 | 10 | edge 1, plate 1 | 0 |
| vector-edge | 0 / 2 | 0 | 0 | 0 | — | 0 |
| still-water | 1 / 3 | 1 | 0 | 4 | — | 0 |
| march-wall (**breach win**) | 7 / 5 | 1 | 2 | 11 | edge 1, optic 1 | 0 |
| gate-stone | 2 / 4 | 0 | 0 | 6 | — | 0 |
| mirror vs wave 6 | 4 / 6 | 1 | 1 | 10 | optic 1 | 0 |

Five things this says:

- **Every `invest` cast resolved `success`.** Not one Blocked, across every
  match. Reading the mask instead of pricing the ladder works exactly as
  advertised, and the single-caster election never collided.
- **The ladder resolved differently per cell, from effects alone.** Against the
  salvo striker it bought **plate** (one contact kills a 2-health prime) and
  **edge** (travel 7 against a declared 8). Against the bulwark, whose heaviest
  declared bolt is 1, it skipped plate entirely and bought **edge + optic**. In
  the fabricator mirror, where nothing outranges me and nothing one-shots me, it
  bought **optic** and nothing else. No track ID appears in a branch anywhere.
- **Two tiers is the ceiling this doctrine reaches; three never happened.** Peak
  bank tops out at 10–11 with 4–9 left unspent at the horn. The 48-scrap pot is
  theoretical; a team that never diverts collects roughly one tier per hundred
  ticks from wreckage alone.
- **Peak carry is 0 in every match.** With the courier removed, every unit of
  scrap this bot banks is an **assay** — a wreck worth exactly 1, paid at the
  tile, with nothing left to carry. The team was therefore never intercepted
  carrying a load, because it never carried one: **courier interceptions
  suffered = 0, and interception is not a risk this doctrine takes.**
- **The interrupt is nearly disarmed.** 0–4 reverts per match against a
  mechanic that reverts a whole run per point of damage. That is the STILLNESS
  and WALK THE POINT pair doing its job, and it is consistent with STILLNESS
  being the largest single term in the ablation.

Two replays are cited in full and `verify`-clean:
`evidence/cited/striker-arc-light-bastion-s104729.json`
(`f188b55e…`, 7–7 advances, a +8 win on the horn) and
`evidence/cited/bulwark-march-wall-bastion-s104729.json`
(`8bbbf7e7…`, breach win at tick 444). The mirror, the hard loss, and a siege
counterpart are beside them.

**Distinct-outcome disclosure.** These bots are deterministic. Every reported
cell ran three disjoint seeds and every match produced a distinct replay hash,
but the *outcome* agreed across all three seeds for five of six opponents on
bastion; only `arc-light` and `still-water` split. Three seeds is therefore
three observations of trajectory and roughly one observation of outcome per
opponent. Nothing in this file is a mean over 18 independent trials and it
should not be read as one.

## Friction

**1. `ArenaBasics.Capture` mis-reads the channel — and it is the kind of miss
that is invisible.** The helper derives `SurplusWeightScalesGain` by testing
whether the control policy contains `net-positive-objective-weight-difference`.
The channel's policy is
`stationary-claim-weight-versus-total-denial-weight-scales-gain-capped-…`, which
does not contain that substring, so a bot inheriting the scaffold reads the most
weight-scaled policy in the game as **binary control** and quietly targets one
body on a point where two would double its rate. Nothing errors and nothing
looks wrong. The scaffold should either expose the raw `controlPolicy` string
beside the booleans, or gain first-class `StationaryGainMultiplierCap` /
`OpposingErosionMultiplier` / `ClaimInterrupt` accessors the way it gained
`LiveHold`. I wrote my own reader (`ContractLens.ResolveChannel`); every author
in this wave will have written the same twenty lines.

**2. `ArenaBasics.ObjectivePresence` cannot express the channel at all.** It
returns one weight per side, and the channel needs **two** — stationary claim
and total denial. The helper is not wrong, it is one arm out of date. A
`(OwnClaim, OwnDenial, EnemyClaim, EnemyDenial)` shape would have made the
central arithmetic of this brief a one-liner instead of a class.

**3. The last-resort argument builder does not know `upgrade-track`.** The
scaffold pattern every author copies switches over `ArgumentConstraint` kinds
and skips unknown ones, then submits the action with whatever it built — so
`invest` goes out with no track and is rejected. It is a silent trap that costs
a body its tick, and it will recur with the next parameter kind. A default arm
that *refuses the action* rather than submitting it partially would fail safe.

**4. An engine invariant abort, since fixed mid-wave.** `error: A retained
projectile must preserve its exact resolved committed path. (Parameter
'projectiles')` ended matches outright whenever an `edge` (gun-travel) tier
settled with a bolt in the air. It was deterministic per (artifact pair, seed)
and cost me roughly an hour: I first mistook it for a pairing problem, then
isolated it to the `invest` verb by ablation (removing INVEST turned a crashing
`arc-light` match into a 499-tick win), then to the travel track by building
single-track variants. My workaround — defer travel to the last rung and hold it
while a bolt is visible — is **no longer in the shipped source**; the coordinator
republished a fixed CLI, I verified a previously-aborting cell now completes,
restored the principled ladder order (health → reach → sight), and re-ran every
sweep in this file on the fixed engine with **0 aborts**. Two DX points survive
the fix. First, the abort path **wrote no replay while some invocations still
returned exit 0**, so a harness that checks return codes silently records
"nothing" as a loss; mine only noticed because I count replay files. Second,
this is the failure mode a `--strict` or a non-zero exit on engine abort exists
to prevent, and four authors hit it independently.

**5. Seeds do not buy observations here.** Three disjoint seeds gave three
distinct replay hashes and, for five of six opponents, one outcome. Against
deterministic policies the seed moves tie-breaks, not verdicts. The lab's
instrument for a doctrine claim is *opponent breadth*, and I would rather have
had ten opponents on one seed than three seeds on five.

**6. `--skills` needs `--classes` for a raw `.wasm`, and the error says so
clearly — but the class-side binding is a trap worth documenting.** Pairs are
alphabetical and team 0 is the first class, so playing my fabricator against a
bulwark requires `--classes bulwark-vs-fabricator --swap`, and forgetting
`--swap` silently measures the *opponent's* bot as me with my artifact on the
bulwark chassis. A `--as <class>` or `--bot-class` flag would make the intent
unambiguous.

**7. The mode observation publishes no revert, and the derivation is
life-scoped.** The brief is right that a revert is "`captureProgress` going
down", but a body has to have been alive last tick to see it — and a fabricator
creates most of its bodies mid-match with empty private memory. This is the same
shape of gap `holdOwnerTeamId` was published to close in wave 4. A
`claimRevertedThisTick` flag, or the last tick's progress on the mode
observation, would make the interrupt legible to a life on its first tick.

**8. Positive: `context.TeamRandom` is the best thing in this SDK drop.** The
XML documentation states the invariant (same tick, same draw index, same value,
including for a life born this tick) and the one rule (draw before you branch)
in fewer words than it took me to get it wrong in wave 6. Re-syncing
`ArenaBasics.cs` was the entire cost of adopting it.

**9. Positive: absent-means-inert held everywhere, again.** `stationaryGainMultiplierCap`,
`opposingErosionMultiplier`, `claimInterrupt`, and the whole `scrapEconomy`
block are absent on the cells that do not carry them, and every branch in this
policy that depends on them switched itself off with no flag and no cell
detection. One artifact played four cells and two class pairings without a
single ruleset-name comparison.
