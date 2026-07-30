# arc-light — DX and freeze record

Wave-4 Frontline Labs entrant. Class **striker**, fresh lineage (no predecessor).
Phase-2 doctrine target: `--pendulum keel --skills kit --bend universal
--movement facing-locked` (registered token `rig`).

## Isolation statement

Material read while authoring, and nothing else:

- `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`
- `docs/FRONTLINE-LABS-RULES.md`
- `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (read in full)
- `templates/botarena-generic-actor/` (the scaffold; `ArenaBasics.cs` is retained
  in this project and used for its contract readers)
- `src/BotArena.Sdk/` public types and XML documentation
- `sandbox/cli-publish/` (nilbots 0.9.15) and its `--print-candidate-contract`
  output, my own replays, and my own qualification report

No other entrant's source, standings, replays, DX notes, or aggregate report was
opened. No `docs/BOT-QUALIFICATION-SUITE.md`, no `DECISIONS.md`, no Engine or App
source. Private scratch was `sandbox/arc-light-scratch-6e1f9d2a/` — a uniquely
named directory created for this authoring pass and used by nothing else. No
accidental exposure to another author's material occurred. Nothing was committed
to git.

Sparring partners were built from permitted material only: the unmodified
scaffold starter with a class declared (`Starter`, striker; `AegisDummy`,
bulwark; `SwarmDummy`, fabricator) and two ablations of *this* project's own
source (`ArcNoKit`, `ArcNoBank`). All were rebuilt from source with
`--no-cache` against the current SDK, so nothing frozen pre-0.10.6 was played.

## Freeze identity

| item | value |
| --- | --- |
| artifact | `out/bot.wasm` |
| bot.wasm SHA-256 | `cc6ccf624a6e7f934512aaab4233705118817aa32cdc8c9f451dd1b79a753f7e` |
| build | `nilbots build <project> --no-cache`, cold cache, reproduced identically on a second cold build |
| toolchain | nilbots 0.9.15, SDK 0.10.6, game rules 0.5, NativeAOT-LLVM 10.0.0-rc.1.26306.1, Docker builder (macOS host) |
| entry | `ArcLight` (`botarena.json` `entryType`), declared `"class": "striker"` |
| qualification | `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, seed 104729, runtime wasm |
| qualification result | **T4**, `passed: true`, `balanceEvidenceEligible: true`, exit code **0** |
| qualification report | `evidence/t4/qualification.json`, SHA-256 `7b68aae43599e307d2311b52e3a508ee67750c3ba6ac3e873d173398941c126f` |
| T3 prerequisite | `frontline-qualification-4` rerun and hash-linked, passed, report SHA-256 `4505d8be7a8d54aa…` |
| source-tree hash | `654615d7523bcb144c6bf69f71985a89392e75aa5a1b46f2d7201538f9cd52cd` (SHA-256 over the name+digest lines below, name-sorted) |

Per-file SHA-256:

| file | sha256 |
| --- | --- |
| `ArcBoard.cs` | `f6e665c994634a7d281358b8297da0f3c24ca74a2ea82b937a7a2b9978afca94` |
| `ArcFacts.cs` | `74e15d58a0cb02ba0ced2dc96b27b21fa66adb4e01bd2b8d52008902bf5a1f0d` |
| `ArcGun.cs` | `cfa541bf6247af0344c124f1b60e538eae5110956f03e3fd6dc78634c9e20594` |
| `ArcKeel.cs` | `7b0a94297e42fdd357eab80a597af0cc7749d69a36fe3e74d39560aca8fba1da` |
| `ArcLight.cs` | `05cc3a9284b145bf4458d8bd5c571143688e151b0284b48234f93a10e162d9c1` |
| `ArcMove.cs` | `87af585dbde925da3675a1fe79a2c2c3a4814c82e2063e350c4ce605b7ea7a0f` |
| `ArcStance.cs` | `5ac9a425a84146347825a6efe2540a6d526f4d92c4dcd8ebc1b3d381ed740c5d` |
| `ArcThreat.cs` | `0613553dda2adecdfc0422285b8bd7d94391f77af24a4615239282eecaa33c29` |
| `ArenaBasics.cs` | `a198af0a28ace85ed9034a9a93d8e106f21a907681547ac7a65e9e21871ce773` (scaffold, unmodified) |
| `botarena.json` | `ada09877c60994dc6d799ae0b5d0864e10b1d18a07b29fdc6827c8a64805ba98` |
| `ArcLight.csproj` | `8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573` |

Arm identities the one artifact plays (striker mirror, all facing-locked):

| cell | flags | rulesetId | rules fingerprint |
| --- | --- | --- | --- |
| A | `--pendulum keel` | `frontline-labs-1-striker-vs-striker-keel-facing-locked` | `4e7c714f87…` |
| B | `+ --skills kit` | `…-helm-facing-locked` | `3666788559…` |
| C | `+ --bend universal` | `…-veer-facing-locked` | `4e7c714f87…` |
| D | `+ kit + universal` | `…-rig-facing-locked` | `3666788559…` |

**A and C share rules bytes, and B and D share rules bytes.** In a striker mirror
the bend factor is inert: the striker already declares the 1–4 tile envelope, so
`--bend universal` hands nothing to either side. The two cells remain distinct
registered identities with distinct match fingerprints, but they are the same
ruleset mechanically, and their records below are identical for that reason
rather than by coincidence.

## Doctrine, in one paragraph

arc-light is a striker that treats aiming as an interception problem and treats
the volley as a tool with a price. Every tick it reads the contract rather than
an arm: the coupling (a facing-locked body may only step where it faces, so an
enemy's reachable set is a **ray**, not a ball, and every shot is aimed at the
ray the target is confined to), the declared bend envelope (every legal arc is
enumerated and previewed through the SDK's own bend rule, and a *curved* arc is
only spent on a hard interception — the tile the target stands on, or the one
tile it can reach exactly as the bolt lands), the live territory hold
(`holdOwnerTeamId`/`holdEndsAtTick` read, never inferred: inside an enemy hold a
completed capture is spent, so the claim is banked short of the threshold and
completed when the hold lifts), the capture policy (surplus objective weight
scales gain, so presence is pressure and a second body is worth as much as the
first; only an enemy standing *alone* erodes a claim, so stepping off an empty
objective is free and stepping off a contested one is not), and the stance routes
(a fan stance is entered only where the map's transition tags permit it, only
when no bolt can reach the tile during a windup that permits nothing but waiting,
only when it does not shed objective weight that is currently load-bearing, and
only when the fan beats what the ordinary gun would do — which is the blind spot
the class's zero initial-aim envelope creates at close diagonal range, or a fan
that reaches two bodies at once). Ownership is the only test applied to an
incoming bolt, so a bolt returned by an aegis shell is hostile without a special
case; enemy guards are counted from published `projectile-deflected` events so a
shield one contact from breaking is fed rather than avoided; and bodies spread by
rank among the team's active lives so independent lives with empty private memory
enter from different bearings instead of stacking into one lane that feeds a fan.

## Measured per-arm records

8 seeds × both team assignments = **16 matches per arm**, WASM runtime, all cells
`keel` + `facing-locked`. Records are arc-light's (W-L-D); `prog` is mean signed
territorial progress; `casts` / `fans` / `bends` are arc-light's own counts summed
over the 16 matches, taken from replay events and traversals.

### vs `Starter` — the unmodified scaffold starter, striker (primary baseline)

| arm | record | mean end tick | prog | casts | auto-returns (fans fired) | bends fired | slots fielded |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A `keel` (kit off) | **12-3-1** | 339.9 | +18.62 | 0 | 0 | 84 | 2.88 |
| B `helm` (kit on) | **16-0-0** | 160.0 | +30.00 | 16 | 8 | 8 | 2.00 |
| C `veer` (kit off) | **12-3-1** | 339.9 | +18.62 | 0 | 0 | 84 | 2.88 |
| D `rig` (kit on) | **16-0-0** | 160.0 | +30.00 | 16 | 8 | 8 | 2.00 |

All 16 wins in the doctrine-target cell are base breaches (+30 = three advances).
Zero runtime faults in every arm.

### vs `ArcNoKit` — this project's own source with the fan-stance route hidden

The only difference is one method: `ArcFacts.FanStanceRoute` returns nothing, so
every volley decision declines. This is the causal A/B for the skill.

| arm | record | mean end tick | prog | casts | fans fired |
| --- | --- | --- | --- | --- | --- |
| A `keel` (kit off) | 0-0-16 | 499 | 0.00 | 0 | 0 |
| B `helm` (kit on) | **0-16-0** | 449.3 | −26.25 | 179 | 113 |
| C `veer` (kit off) | 0-0-16 | 499 | 0.00 | 0 | 0 |
| D `rig` (kit on) | **0-16-0** | 449.3 | −26.25 | 179 | 113 |

The kit-off cells draw at the tick cap in all 16 matches with 0.00 progress,
which is the correct signature for two behaviourally identical artifacts and
confirms the harness is clean. **In the kit-on cells the volley user loses every
match to its own non-using twin.** This is the wave's most load-bearing number
and it is reported as measured, not smoothed: see "What the volley is worth".

### vs `ArcNoBank` — same source with the published hold clock never deferring a capture

| arm | record | mean end tick | prog | casts |
| --- | --- | --- | --- | --- |
| A / C (kit off) | 0-0-16 | 499 | 0.00 | 0 |
| B / D (kit on) | 0-0-16 | 499 | 0.00 | 272 |

Every match is a draw, in both cells, because **the hold-clock code path is never
reached in mirror self-play**: neither side ever completes a capture, so no
territory hold ever exists to bank against. The banking doctrine is implemented
and contract-driven but is *untested by self-play*; it needs an opponent that
actually advances the front. Treat it as unmeasured rather than as neutral.

### Cross-class probes (doctrine-target `rig`, 4 seeds × both assignments = 8)

| pair | arc-light record | resolved topology |
| --- | --- | --- |
| `bulwark-vs-striker` | **8-0-0** (+240 total progress) | 3 vs 3 slots |
| `fabricator-vs-striker` | **8-0-0** (+240 total progress) | 5 vs 3 slots, fingerprint `db60b6fef94f` |

Worth knowing what these actually prove. Because WASM artifacts carry no class
manifest, `--swap` swaps *chassis*, not just sides: in half of these matches
arc-light drove a **bulwark** (aegis shell and reversible anchor in its own form
list) or a **fabricator** (explicit fabrication, five slots) and still won. The
artifact is genuinely contract-driven rather than striker-shaped. Two mechanics
stayed **unexercised**, and neither absence is arc-light's doing: no shell was
ever raised in any of these matches, because the only permitted bulwark sparring
partner is a scaffold starter that never transforms, and arc-light's own stance
model enters *fan* stances only (see friction 3). So the deflection-aware
paths — returns as hostile bolts, `projectile-deflected` accounting, feeding a
shield to its third contact — are implemented and compiled but carry zero
measured evidence.

### What the volley is worth (five priced configurations)

Every configuration below is the same doctrine with one pricing rule changed, run
against `ArcNoKit` in the kit-on cell. The volley was not "adopted" or "ignored";
it was priced, five times, and the price was measured.

| pricing of the cast | matches | casts | record vs own ablation |
| --- | --- | --- | --- |
| permissive — fan lane coverage beats the aimed shot † | 16 | 101 | 2-14-0 |
| strict — forced-hit cast posts only † | 8 | 0 | 0-0-8 |
| blind spot only, may leave the objective † | 8 | 41 | 0-7-1 |
| blind spot, never trades held ground † | 8 | 56 | 0-8-0 |
| + fan that reaches two bodies † | 8 | 61 | 1-7-0 |
| final artifact (same rule, after the T4 repairs) | 16 | 179 | 0-16-0 |
| never cast (the ablation itself) | 16 | 0 | 0-0-16 |

† measured on intermediate builds that predate the five qualification repairs
listed at the end of this file, so they are comparable to each other but not
byte-comparable to the frozen artifact. The direction of the effect is identical
in all six.

Two conclusions, in the order of confidence:

1. **The mechanics are not inert.** The stance is entered, the fan launches three
   bolts on adjacent headings, and the engine's own budget returns the body:
   113 of the 179 casts ended in an `automatic-threshold-return` after exactly one
   attack, and 31 ended in a deliberate early `mobilize` below the threshold.
   Every one of those is a published event with a start and a completion tick.
2. **For a striker that already searches its whole bend envelope, the volley is
   dominated.** A cast costs two immobile wait-only ticks plus the stance gun's
   cooldown 5 against the mobile gun's cooldown 2 — roughly three bolts of
   tempo — and delivers three bolts that cannot be aimed, only pointed. Against a
   facing-locked mover that is a strictly worse trade: bolts fell from 1456 to
   1067 and kills from 432 to 219. Three *aimed* bolts beat three *fanned* ones.
   The one place the fan is not a trade at all is the blind spot the class creates
   for itself: the striker's declared initial-aim range is **zero** and a bend
   cannot start before the first tile, so a body inside the near diagonal cannot
   be shot by the ordinary gun at all, and the fan's adjacent headings are the
   only weapon that reaches it.

The final artifact keeps the cast for exactly that case (plus a fan that reaches
two bodies), never trades held ground for it, and consequently beats the scaffold
baseline **better** with the kit on (16-0-0 at tick 160) than with it off
(12-3-1 at tick 340) while still losing the mirror against a non-using twin. Both
facts are true and they answer different questions.

## Top 3 frictions

**1. Every objective tile carries the transition-forbidden tag, which silently
un-designs the volley — and nothing says so.** `EXPERIMENTAL-FRONTLINE-CLASSES.md`
sells the stance on "objective weight stays 1, so it still holds ground", and the
rule card only mentions the tag in the context of *Anchor* ("Anchor is illegal on
every contract-tagged transition-forbidden tile"). Both are true and together
they are misleading: a stance is an ordinary same-life route, so it inherits the
same `forbiddenOutputTileTags`, and on `frontline-labs-01-classes` that tag covers
all 22 objective tiles, the entire central corridor, and 112 of 233 open tiles.
The one sentence that would have saved several hours — *the fan cannot be cast
from the ground it is meant to deny* — is derivable only by dumping
`map.tileTags` and intersecting it with the objective regions. Cost: a complete
redesign of the skill's role (from "free fan from atop the objective" to "fan
from the shoulder beside it"), discovered from a legality mask reading
`transform: available=false` with no reason attached. **A legality entry that is
unavailable should say which constraint refused it**; `Available: false` with a
populated `FormTargetConstraint` is the least informative possible answer, and it
is indistinguishable from a cooldown gate. I could not determine from eight
hundred observations whether `transform` is *also* cooldown-gated, because the
bot never stood on a permitted tile while on cooldown.

**2. The published CLI is named `botarena`, and a built artifact forgets its
class.** The brief and every doc invoke `sandbox/cli-publish/nilbots`; the file on
disk is `sandbox/cli-publish/botarena`, and the version banner it prints is
`nilbots 0.9.15`. Separately, `botarena.json`'s `"class": "striker"` resolves the
arm when you pass *project* paths, but a `out/bot.wasm` carries no manifest, so
every frozen-artifact run needs an explicit `--classes striker-vs-striker` or it
fails with "`--skills` … needs a class pair". That is exactly backwards from the
authoring flow it documents: you develop with project specs (class implicit) and
freeze with artifacts (class suddenly explicit), and the failure arrives at the
end. Worse, with artifacts `--swap` then swaps *chassis* rather than sides, so a
mirrored-accounting sweep silently runs half its matches with the bot driving the
opposing class. That is a fine robustness test and a terrible default: it means
"16 matches, both assignments" does not mean what it means everywhere else in
this experiment.

**3. The scaffold's own helpers are a generation behind the contract it
describes, in the exact places the doc says not to guess.**
`ArenaBasics.ClassOf` recovers a team's class by splitting form IDs on `-` and
comparing prefixes — the precise thing `EXPERIMENTAL-FRONTLINE-CLASSES.md`
forbids ("Do not parse a `FormId` prefix to recover any of these facts") now that
`ClassId` is published on self, allies, enemies, participants, and teams. Its
doc-comment even promises "a typed classId replaces this helper's body in a later
contract generation" — that generation has shipped, and the helper hasn't moved.
`ArenaBasics.Capabilities` has the same shape of gap: it reports `AnchorRoute` by
asking whether any same-life route leads to a zero-objective-weight form, which
now also matches nothing useful for the two *stance* routes (both preserve weight
1), so a bot that branches on it concludes a striker has no routes at all. A
related asymmetry cost me a wrong model for an hour: the scaffold offers
`Capture`, `LiveHold`, `ObjectivePresence`, `ArrivalsRallyForward`, and
`ExpectedArrivalTiles` as first-class contract readers, but nothing at all for the
two new skill facts (`AttackProfile.Volley`, `Form.ProjectileGuard`) or for the
stance budget on `FormTransition.AutomaticReturn`, which are the whole subject of
the wave. Every entrant in this wave will write those five readers independently,
and every one of them will decide for itself whether "fan form" means a volley
field or a form-ID substring.

## Build and qualification timings

Cold `--no-cache` WASM build: ~11 s (Docker builder, macOS arm64 host). Full
`frontline-qualification-5` run including the hash-linked T3 and T2 prerequisites:
~2 min. One 500-tick WASM match: ~1.3 s. The 4-arm × 8-seed × 2-assignment grid
(64 matches): ~90 s.

## Repairs made from probe feedback (mechanical, not strategic)

Recorded because the packet asks for them explicitly. All five were contract
handling, found from my own qualification report and my own replays:

1. `Unmask` (in-objective repositioning) stepped into a bolt's path because it
   scored destinations for lane value only — it now prices the destination's
   incoming threat. Failed `cadence-parity` (`made-no-evasive-move-while-the-
   threat-was-apparent-only`, `took-no-damage`).
2. Evasion rejected any destination a bolt reached no later than the current tile,
   which made retreating *along* a bolt's own lane illegal even though the bolt
   has finite remaining travel. Now only a next-tick arrival disqualifies a tile.
   Failed `straight-evade` (`took-no-damage`).
3. Interception scored set intersection rather than timing, so a straight ray got
   credit for crossing somewhere a target might wander and beat a bend that would
   land. Now weighted by slack between arrival and earliest-possible-occupancy.
   Failed `wall-terminated-bend` (`fired-at-least-one-curved-shot`).
4. A *curved* arc was spent on any path that passed near a body, including one
   whose only qualifying tile was three steps from the target. Bends now require a
   hard interception (target's current tile, or a tile one step away reached
   exactly as the bolt lands). Failed `strict-corner` (`fired-no-curved-shot`).
5. Routing refused every step onto a threatened tile, which answered suppression
   with paralysis and never reached the objective. It now prefers an equally
   closing safe step and otherwise walks through anything short of lethal. Failed
   `entry-initiative` and `map-holdout` (`first-life-reached-the-active-objective`).

Strategy passes: one authoring pass plus five volley-pricing iterations, all
measured against the project's own ablation rather than tuned by eye.
