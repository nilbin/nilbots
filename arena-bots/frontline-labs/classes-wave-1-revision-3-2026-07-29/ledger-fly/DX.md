# DX notes — ledger-fly revision 3 (Frontline classes population, pendulum arms)

## Isolation statement

Written from this project's own sources, its own frozen predecessors, its own
qualification report, and matches this entrant played against **its own rebuilt
revision-2 artifact and nothing else**. No other entrant's directory, source,
standings, replays, or aggregate balance report was opened; no scratch
directory other than my own was read. Permitted material actually consulted:
`docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`,
`docs/FRONTLINE-LABS-RULES.md`, `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`,
`templates/botarena-generic-actor/`, the public SDK types under
`src/BotArena.Sdk/`, my own frozen wave-1 and revision-2 directories (read only,
left untouched), and `sandbox/cli-publish/`. Private scratch for this pass was
`sandbox/ledger-fly-v3-scratch-b62e41/` — a uniquely named directory, not a
shared or guessable one. **No accidental exposure to disclose.**

## Assignment and freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Revision | 3 (wave-1 revision-3 pass, pendulum arms) |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 (retain) |
| Budget | **one** strategic revision; mechanical/contract repairs free |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-revision-2-2026-07-29/ledger-fly` (untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `fcd6358d30064f38ea00a2ddd88c9dd0c7406a79ab8bd165c938fc44014c36b4` |
| Template helper | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368` (carried byte-identical) |
| Source-tree hash | `4ae72a0f0dd9430b45b87ae30f1e7308b9c1663821f38fcea275bf8f92c54bc0` |
| Toolchain | nilbots CLI 0.9.10, SDK 0.10.4, game rules 0.5, runtime protocol 0.1 / actor 1.0, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Build cache key | `f8669e3b178a70aa1c6adbbac209b363c1071e121a8e0e9808ea6d8a987ffe79` |
| **`out/bot.wasm` sha256** | **`83db091374e7ca7b714b731546efaf8e1d27866c1d79638620236e19e1b11b8c`** (3,357,728 bytes) |
| Qualification | suite `frontline-qualification-5` (v1), profile `frontline-duel-depth-union-t4-v1`, contract fingerprint `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb`, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `76e57e0f1d721747a2f6266317ed602ca1304f2ff0ee34de5864c27c2b23438d` |
| T3 prerequisite report sha256 | `776cc622ef2a676f65f390bb24a64fe7c38c29a29789151b6da1719b97d9a846` |
| T2 prerequisite report sha256 | `4e7801897b3b2234909ecfba6974c9b28808b7ee25f1ef89366fea65c6a136d3` |
| Verified probe replays | 36 under `evidence/t4/` |

Per-file sha256 of the submitted set (the source-tree hash input, sorted):

```text
a05ec4b4ef15753836ee107586fb6442378ed8078a6d4a3cafef9a9a5bd56368  ArenaBasics.cs
28bdce1328322aa8457ac7a11d7991d9f4f0e856543bbc81f237a6578c80537e  FabricationRoute.cs
bb4079b84e3b43e1cc139fe2b5c2deb24ff4cb8abc642b9fa596a5d3f014b773  Field.cs
0c9091fa78e9df84d593a17cbdac51c01b46ce017ec6046a8988170cc9478a6f  Gunnery.cs
928e9e177546ece72a956873b60cdb18aa6642150063a6066e9b8bbd125505fc  Kinematics.cs
bcd6a4c64a4ac6fd509f9498aa5c5a929c1940c373574dc368dc63e350496f1d  Ledger.cs
de0a462acd63f6bf6b8040c1d3ac7f7e6a1b875a73c92716277d88fa52412e63  LedgerFly.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  LedgerFly.csproj
0effafa8dfc41563fa49fa906672258adeeb8e19cbf036f2b06e604a026d2a9d  MatchLens.cs
4302f09adf18733010a47322ee67b368ea604b6600703617d65077baac20f6da  Ratchet.cs
ca79e974a421862b5860c82d2cca293cfc8eb6b957d105ae76bcc6e876374e71  botarena.json
```

Source-tree hash is sha256 over the sorted submitted files, each contributed as
name, NUL, big-endian 8-byte length, bytes — the same construction as revision
2. `Ratchet.cs` is the only new file; `Field.cs`, `Gunnery.cs`,
`Kinematics.cs`, `FabricationRoute.cs`, `LedgerFly.csproj`, and `botarena.json`
are byte-identical to revision 2. Every suite-5 probe passed on the first
canonical build of this revision: T2 `contract-matrix`, `automatic-life-cycle`,
`objective-path`, `direct-fire`, `straight-evade`, `manual-fabrication`; T3
`wall-terminated-bend`, `strict-corner`, `cadence-parity`, `cooldown-window`,
`local-form-safety`; T4 `suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`.

## The mechanical repair that had to come first

The brief is right: **the frozen revision-2 `.wasm` faults on the sticky arms.**
`capture` gained `ratchetHoldTicks`, and an artifact whose compiled-in SDK
predates the field cannot decode the contract. Nothing in the source needed
changing for this — `nilbots build <project> --no-cache` against the current
SDK is the entire fix, and the rebuilt revision-2 source (artifact
`7c4e9c4de1c0785eec587631e45d8b8f576a096e1309871ef6f4850f455c1b08`) plays all
four arms. Every measurement below is against that rebuild, never against the
frozen wave-1 or revision-2 binaries.

Worth stating plainly for the cohort: **a frozen artifact is only frozen against
a frozen contract.** The freeze manifest records a wasm hash and a source-tree
hash, and the wasm hash silently stops being replayable the moment the contract
schema moves. The source-tree hash is the durable identity; the artifact is a
cache.

## The one strategic revision

**The ledger stops booking bodies and starts booking convertible
objective-ticks.**

Revisions 1 and 2 both denominated the doctrine in bodies — first against the
enemies we could see, then (the revision-2 fix) against the opposing slot
capacity the contract declares. Bodies are the right unit only where every tick
of sole presence is worth the same thing: every capture advances, one body of
positive weight nulls any number, and a death is a walk home. The pendulum arms
break all three, and all three breaks are *readable fields*, so the doctrine now
prices them instead of assuming them:

1. **`capture.ratchetHoldTicks` and the inferred hold owner.** Inside somebody
   else's hold a completed capture is spent — the claim resets exactly as a
   successful capture does and the front does not move — so the objective is a
   denial instrument and nothing else, and the bank does not spend bodies on it.
   Inside our own hold the front cannot come back, so committing forward cannot
   cost ground and the bank leaves its standoff.
2. **`capture.controlPolicy`.** When surplus objective weight scales capture
   pressure, presence is measured in weight rather than in bodies: the bank
   joins the objective while our weight is not yet clear of theirs, because that
   marginal body is the difference between capturing at two progress a tick and
   being eroded. Under binary control the first body has already bought
   everything a second one could, so nothing changes.
3. **`lifecycle.automaticReturnPlacement`.** When arrivals rally onto the
   own-side chain-adjacent objective, a death costs the return clock and no
   walk, so the caution premium that set the bank's standoff shrinks by a tile.

Everything else about the banker survives untouched: it still identifies its
economy anchor from the lifecycle assignment rather than from a slot number,
still lends against declared capacity on the earliest legal tick, still drops
replacements where the last exchange happened, still never Splits or Anchors.

**How much of this is really one revision.** Following the revision-2 precedent
of saying so rather than hiding it: I count the three readings above as one
change because they are one sentence — *price the objective tick, then spend
bodies on it* — and because each of them is inert unless the contract declares
the corresponding field. A reviewer who counts "weight instead of bodies" and
"time instead of bodies" as two revisions would not be wrong. What I would
defend hardest is that none of it is a new *behaviour*: the same ladder runs,
with the bank's commit test and the objective's edge preference reading three
fields they previously ignored.

## Inferring a fact the observation schema does not carry

The hold length is a contract field; its **start and owner are not observable**.
`Ratchet.cs` derives them:

- an advance is the tick `ActivePositionIndex` moves;
- the sign of that move against this team's declared `objectiveIndexDelta` says
  whose advance it was;
- the contract publishes its own redeploy arithmetic
  (`capture tick + 1 + pause`), so `ControlResumesAtTick` recovers the exact
  completion tick instead of the tick we happened to notice on.

The honest limit: **private memory is life-scoped**, so a body created inside
somebody else's hold can prove that an advance happened recently (the resume
clock is still running) and cannot prove whose. Guessing is 50/50 and the two
errors cost about the same, so that case reports "no hold known" and plays the
baseline. There is no legitimate way around it — team perception shares the
current observable union, not history — short of steganography through body
facing, which I considered and rejected as a gimmick that would also cost a
rotation per bit under `facing-locked`.

**The capture-throttling idea I deliberately did not implement.** Inside their
hold, a capture that completes is discarded, so the theoretically optimal play
is to hold progress below threshold and complete exactly as the hold expires. I
worked the arithmetic before writing any code. You cannot stop accumulating
while holding sole presence, so throttling means leaving the objective; off the
objective progress decays 1 per 2 ticks while on it progress gains 1 per tick.
For a 40-tick hold, the naive "just keep capturing" line advances about 10 ticks
later than a perfectly timed throttle — but the throttle requires abandoning the
objective for roughly 13 consecutive ticks in the middle of their hold, which
hands them the region and the first claim. Priced against that, it is a wash,
and it is a wash that loses badly whenever the timing estimate is off. Recorded
here because "the exploit exists and is not worth taking" is a balance finding,
not just a coding decision.

## What I measured, honestly

Candidate versus the **rebuilt revision-2 source**, `--classes
fabricator-vs-fabricator`, `--movement facing-locked` on every cell, both sides
(`--swap`), 12 seeds per side. W/L/D are the candidate's, resolved by artifact
hash rather than by slot, because `--swap` moves the candidate to the other team
and the CLI's own total is slot-relative.

| Arm (`--pendulum` / numbers) | Candidate record vs rebuilt r2 | Reading |
| --- | --- | --- |
| control (unmodified) | **9W 9L 6D** | exactly neutral — see below |
| `ratchet` | **24W 0L 0D** | decisive, and it holds on both sides |
| `ratchet-contest` | **24W 0L 0D** | decisive; base-breach at t250 / t188 |
| `--capture-threshold 9 --prime-respawn-ticks 9` | **7W 7L 10D** | exactly neutral |

The control and numbers rows are neutral in a stronger sense than the numbers
suggest: every result is exactly antisymmetric across the swap, because on a
contract that declares no hold, spawn-anchored arrivals, and binary control,
**revision 3 emits byte-identical decisions to revision 2**. I checked this
directly rather than inferring it — 926 and 939 decisions compared tick by tick
across two full matches, zero differences, and the outcome equals the revision-2
self-mirror to the tick and the point. Those two rows are therefore a
measurement of the map's side bias, not of the revision.

The same run under the controlled WASM runtime (4 seeds per side) reproduces
every completion reason, end tick, and territorial score exactly: control 3W 3L
2D, `ratchet` 8W 0L 0D, `ratchet-contest` 8W 0L 0D, numbers 2W 2L 4D.
`nilbots verify` accepts the replays.

### Where the wins actually come from (ablations, 8 seeds per side)

I did not want to report a 24-0 without knowing which sentence earned it, so I
ablated the revision one reading at a time.

| Build | `ratchet` | `ratchet-contest` |
| --- | --- | --- |
| full revision 3 | 16W 0L 0D | 16W 0L 0D |
| hold clock disabled (`ratchetHoldTicks` forced 0) | 16W 0L 0D | 16W 0L 0D |
| forward-rally repricing disabled | 0W 0L 16D | 16W 0L 0D |
| standoff repricing alone disabled | 0W 0L 16D | 16W 0L 0D |

So the attribution is clean and not what I expected:

- **`ratchet` is won entirely by one tile of standoff.** Reading
  `automaticReturnPlacement` and shortening the bank's staging band by one tile
  because a death no longer costs a walk home is the whole margin; remove it and
  the arm collapses to sixteen draws.
- **`ratchet-contest` is won entirely by massing.** In the contest cell the
  candidate stood two or more bodies on the active objective for 26 ticks of a
  216-tick match; the predecessor did so for **zero**, because binary-control
  doctrine says a second body buys nothing. Two weight against one is one
  progress a tick where the predecessor got none, and two against zero is a
  capture in eight ticks instead of fifteen. Massing is *not* the mechanism on
  `ratchet`: there the predecessor actually stacked more (113 two-body ticks
  against the candidate's 49 in the same match) and still lost every game.
- **The hold clock earns nothing measurable.** It fires — the phase tags the
  bot writes into its own decision reasons show 528 sheltered decision-ticks on
  `ratchet` and 1020 sheltered plus 636 barren on `ratchet-contest` across the
  twelve seeds, against exactly zero on the two arms that declare no hold — and
  it is, as far as I can tell, correct. It simply does not separate in
  self-play: a mirror shares the window, so a doctrine that reacts to the window
  symmetrically produces no differential. I am keeping it because the brief asks
  the doctrine to price the fact and because it costs nothing measurable, but I
  am not claiming it as a win, and an evaluator should treat it as
  **unfalsified rather than validated**. (The candidate never once enters a
  barren phase on `ratchet` — it does not lose ground on that arm — which is its
  own small illustration of why a mirror cannot exercise the branch.)

### Two contract-driven ideas that read beautifully and measured to nothing

Both were implemented, measured, and **deleted** rather than shipped, because a
population revision should not carry complexity it cannot defend:

1. **Predicting the opposing Prime's return tiles.** Under a forward rally the
   enemy Prime reappears beside the fight, and both halves are contract facts
   (return delay on its lifecycle profile, placement policy on the lifecycle
   definition), so folding `ExpectedArrivalTiles` for the enemy slot into the
   remembered-contact list is exactly the right model. It changed **zero
   decisions** across every match I am permitted to run. Deleted.
2. **Standing down a redundant second body inside their hold.** Under binary
   control the first body of positive weight buys all the denial there is, so a
   second body walking onto a barren objective is paying for a capture that will
   be discarded. Correct, and it **never fired**: with three slots, a child sees
   only the Prime among its allies most of the time, so the "an ally already
   holds it" precondition is essentially unreachable. Deleted.

### One thing I got wrong and had to measure my way out of

My first cut replaced *every* home-anchored term with the chain-derived arrival
tile, on the reasoning that a spawn anchor is simply the wrong tile once
arrivals move. That cost the `ratchet` arm two games (14W 2L instead of 16W 0L),
and the split ablation showed exactly which use was at fault: the bank's staging
score has a mild leash term pulling it toward its own half, and anchoring that
leash on the rally point makes the leash follow the fight — at which point it
stops being a leash and fights the standoff band. Dropping the leash entirely
under a rally was worse still (8W 8L). The resolution is a distinction I now
think is the actually interesting one on these arms: **"which half of the map is
mine" and "where does my next body appear" are different questions with
different answers, and only the second one moves.** The spawn anchor answers the
first; `ExpectedArrivalTiles` answers the second.

## Time

| Step | Wall time |
| --- | --- |
| `dotnet build` of the editing project | 0.5 s |
| in-process match, 500 ticks | ~2.5 s |
| full four-arm sweep, 12 seeds, both sides (8 parallel CLI runs) | ~40 s |
| cold `nilbots build . --no-cache` (warm Docker builder) | 10.2 s |
| full cumulative suite-5 qualification (T2+T3+T4, both assignments, WASM) | 6.0 s wall / 8.7 s CPU |
| four-arm WASM confirmation sweep, 4 seeds, both sides | 19.9 s |

The inner loop is genuinely excellent, and the parallel-friendly CLI is a large
part of why: eight `nilbots experiment` processes on separate `--out`
directories saturate the machine with no interference at all.

## Documentation gaps, frictions, and hardcoding temptations

**1. Two of my three revision-2 frictions were fixed, and the fix is visible.**
`EXPERIMENTAL-FRONTLINE-CLASSES.md` now documents `facingCoupling` including the
"absent means preserve-facing" rule, and the refreshed scaffold ships
`Capture()`, `ObjectivePresence()`, `ArrivalsRallyForward()`, and
`ExpectedArrivalTiles()` with doc comments that say what the *absence* of a
field means. That mattered: `ratchetHoldTicks` is zero-and-absent on the control
arm, `controlPolicy` differs only by policy-ID substring, and every one of those
is the shape that makes an author assume the concept does not exist. The
scaffold's readers turned what would have been a day of contract archaeology
into reading four XML doc comments. This is the single biggest DX improvement
between revisions.

**2. The hold's start and owner are not in the observation schema, and cannot be
recovered across a death.** This is the one remaining unrecoverable fact.
`ControlResumesAtTick` is delivered and proves an advance just happened;
`ActivePositionIndex` is delivered and proves where the front is; nothing
delivered says *whose* the last advance was, and private memory is life-scoped.
A fresh life inside a hold is structurally blind to the most important number on
the arm. Either `mode.holdOwnerTeamId`/`holdEndsAtTick` as observation fields,
or an event kind for the advance carrying the capturing team, would close it.
The `mode-changed` event does carry the post-change state, but only to lives
that existed when it fired.

**3. Self-play cannot A/B a structural arm, and this pass is the proof.** The
brief says so and the numbers agree: the hold clock produces over fifteen
hundred phase-tagged decision-ticks and exactly zero outcome difference, because
both mirrors experience the same window at the same time. The two arms where I *can*
show an effect are the two where the revision changes what the bot does in a
state the opponent does not share — where its standoff sits, and how much weight
it puts on the objective. A system-owned, non-strategic calibration opponent per
class remains the biggest measurement gap for an isolated author, exactly as I
reported at revision 2, and the pendulum arms make it worse rather than better:
a counterweight is defined by how it changes an *asymmetric* contest.

**4. Ruleset IDs abbreviate the arm and the abbreviation is not documented.**
`--pendulum ratchet-contest` resolves to
`frontline-labs-1-fabricator-vs-fabricator-contest-facing-locked` — the sticky
and rally halves vanish from the ID even though they are in the ruleset, and
`sticky-frontline` becomes `sticky` while `forward-rally` becomes `rally`. With
a 64-character canonical budget that is clearly deliberate, but a reader who
greps replays for `ratchet` will find none of the ratchet matches. One line in
the arm brief mapping token to ID suffix would prevent a genuinely confusing
half-hour.

**5. `--print-candidate-contract` prints the identity but not the contract.**
The name promises the resolved candidate contract; it emits ruleset ID and
fingerprints. The actual policy values I needed — `ratchetHoldTicks: 40`,
`controlPolicy: net-positive-objective-weight-difference-scales-gain…`,
`automaticReturnPlacement: own-side-chain-adjacent-objective-tile-then-assigned-spawn`
— live in `header.contract` of a replay, so confirming what an arm does costs a
match. Either rename the flag or have it dump `header.contract`.

**6. Hardcoding temptations I resisted, and where.** The new one is the hold
length: `40` appears in the arm brief, in the contract, and nowhere in this
source — `Ratchet` reads `capture.ratchetHoldTicks` and treats zero as "no hold
declared", so the same code is correct on an arm that ships a different hold or
none. The redeploy pause is read the same way rather than assumed to be 5, and
the completion tick is reconstructed from the contract's own published
arithmetic rather than from a magic `-6`. The rest survive from earlier
revisions: unlock ticks, placement offsets, capture threshold, projectile
geometry, enemy capacity, and the ranking channel are all resolved, and
`Standoff` (3 tiles, minus one per declared reason) remains the only tuned
constant in the bot.

**7. Confusing terminology, worse than last revision.** "Bank" in this bot,
"Prime" in the rules card, "lifecycle assignment with an
`AssignedRespawnSpawnId`" in the contract — and now "the ratchet" means the
counterweight family in the arm brief, `redeployPolicy` in the contract,
`ratchetHoldTicks` in one field of it, and "high-water mark" inside that policy
ID. Four names for one mechanism across three documents.

## Top remaining frictions, ranked

1. **No neutral opponent per class.** Every strategic measurement an isolated
   author can make is against their own doctrine, and a structural counterweight
   is precisely the thing a mirror cannot measure — the hold clock in this
   revision is correct, active, and unfalsifiable for exactly that reason.
2. **The ratchet hold's owner and start are unobservable, and unrecoverable
   across a death.** The one fact on the registered structural arm that a
   contract-driven bot cannot read, in a game whose whole discipline is "read it
   from the contract".
3. **The arm's own identity is lossy and its values are not printable.** The
   ruleset ID silently abbreviates which counterweights are live, and
   `--print-candidate-contract` prints fingerprints rather than the capture and
   lifecycle policy values that decide how the arm plays.
