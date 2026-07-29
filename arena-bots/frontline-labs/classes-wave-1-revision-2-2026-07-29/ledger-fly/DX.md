# DX notes — ledger-fly revision 2 (Frontline classes population)

Written from this project's own sources, its own qualification report, and the
wave-1 factorial replays of matches this entrant played. No other entrant's
directory, source, standings, or replays were opened, and no aggregate balance
report was read. The two names that appear below (`still-water`, `vector-edge`)
come from my own replay *file paths*, which the assignment supplied; nothing
inside those bots was inspected. Private scratch for this pass was
`sandbox/ledger-fly-v2-scratch-7f3a/` — a uniquely named directory, not a
shared or guessable one. **No accidental exposure to disclose.**

## Assignment and freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Revision | 2 (wave-1 revision-2 pass) |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 (retain) |
| Budget | **one** strategic revision; mechanical repairs free |
| Predecessor | `arena-bots/frontline-labs/classes-wave-1-2026-07-29/ledger-fly` (untouched) |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `3fb217bbf9ad1e181c103ebf19cd4b56ed1e8d38c54343fdc5cc7e6531b1aedf` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `dcf1f4a25c43741af68c2ef77391858f9a7f88e2b23fec16cd871d4be9fd80c5` |
| Template helper | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194` (carried byte-identical) |
| Source-tree hash | `3a7481b0507bc4837dc970bca6b08b3e263693c8a1b5f0da8e53bf65a8c0690a` |
| Toolchain | nilbots CLI 0.9.7, SDK 0.10.4, game rules 0.5, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Build cache key | `82e0a021fbac6b3cb3c02cd214c4674fec173596332dae8cbc99dcc604c84a29` |
| **`out/bot.wasm` sha256** | **`81b7a91704cccdea864f84494bf85690084791c17bfeddfbc5f47942779a986c`** (3,321,420 bytes) |
| Qualification | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, contract fingerprint `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb`, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `ca6dd69ec7a6fc3ed999624a3cac63ed5a11ba9a7c3e9de22d835c224eddda29` |
| T3 prerequisite report sha256 | `0e49d487d0e8d11da11e85c1850c2ec59a74f767e573862e015b0cb22bf33791` |
| T2 prerequisite report sha256 | `00e6667355d14ac2b39331b206b2e2918bfc1af8102a0c6839481fdda42520fb` |
| Verified probe replays | 36 under `evidence/t4/` |

Per-file sha256 of the submitted set:

```text
9cae31d594188f46403887578c2347307cfecd018b2a9d84e06fc82c09afb194  ArenaBasics.cs
28bdce1328322aa8457ac7a11d7991d9f4f0e856543bbc81f237a6578c80537e  FabricationRoute.cs
bb4079b84e3b43e1cc139fe2b5c2deb24ff4cb8abc642b9fa596a5d3f014b773  Field.cs
0c9091fa78e9df84d593a17cbdac51c01b46ce017ec6046a8988170cc9478a6f  Gunnery.cs
928e9e177546ece72a956873b60cdb18aa6642150063a6066e9b8bbd125505fc  Kinematics.cs
02d46ba8d7fae64f3680a1fcf19ba6aaa09a343138da68ad1250cceb228f3b19  Ledger.cs
48ff74fa8818d94f2c0886f6c1927db5839d6d23a4f2c9510bc77452f1cd7729  LedgerFly.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  LedgerFly.csproj
066a2ee55273e0abeb19275e623e73bd38190fc1f980d4e173b244de690ed45d  MatchLens.cs
ca79e974a421862b5860c82d2cca293cfc8eb6b957d105ae76bcc6e876374e71  botarena.json
```

Source-tree hash is sha256 over the sorted submitted files, each contributed as
name, NUL, big-endian 8-byte length, bytes. Every suite-5 probe passed on the
first canonical build of this revision: T2 `contract-matrix`,
`automatic-life-cycle`, `objective-path`, `direct-fire`, `straight-evade`,
`manual-fabrication`; T3 `wall-terminated-bend`, `strict-corner`,
`cadence-parity`, `cooldown-window`, `local-form-safety`; T4
`suppression-choke`, `entry-initiative`, `prediction-chamber`,
`front-rotation`, `map-holdout`.

## Loss forensics (wave-1 replays, this entrant only)

18 striker matches (3 map arms × 2 opponents × 3 seeds) plus the bulwark and
fabricator-mirror sets. Four findings, in the order they mattered.

**1. The lending rule was denominated in a sensor that could not see.**
`WantsReplacement` refused to queue while `FieldBodies >= EnemyBodies`, and
`EnemyBodies` was `max(1, enemies remembered in the last 40 ticks)`. Measured:
30–52 % of all our decision ticks had an *empty* enemy list, so the estimate
sat at its floor of one while the other side fielded two or three. Consequence,
per striker match: 40–160 slot-ticks with a Ready slot never spent, average
bodies 1.05–1.65 against their 1.26–1.91, outnumbered in 30–43 % of ticks, and
out-numbering them essentially never. **The reactive queue did leave us
under-bodied against tempo — that is the answer to the assignment's question,
and it is the revision.**

**2. Ranged pressure won on shot volume, not on aim.** Striker matches: 24–75
attacks from us against 38–130 from them, 7–45 hits against 22–62. Their
shots-per-hit was better, but the dominant term was that they fired about twice
as often. Ours were missing because the ladder had no answer for a tick with an
empty gun: fire cooldown is 2, and on the off tick every rung returned null
(`Contest` returns null once you are already on the objective), so the body
emitted `wait` — 32–46 % of all our ticks, including 58–165 ticks per match
spent waiting *with an enemy in plain sight*. The body stood still in the lane
it had just been shot down.

**3. 27 % of the damage we took came from a shooter we had never seen** on the
tick it fired (14–35 % across the sample), and `Field.Covered` — the term meant
to keep bodies out of lanes — counted only *currently visible* enemies, so for
a third of the match it evaluated to zero everywhere and gave no guidance.

**4. The bot was a pure function of the map and the opponent.** All three seeds
of every wave-1 pairing produced identical outcomes, because v1 never touched
`context.Random`: every tie broke on an absolute compass order. On a
mirror-symmetric map that is a systematic side bias, exactly as the refreshed
template warns.

The prime died 3–10 times per striker match, mostly on the centre objective —
each death gifting 18 ticks of unopposed capture against a 15-tick threshold,
i.e. roughly a free advance per death. That is the mechanism behind the
`base-breach` losses at t222–t452.

## The one strategic revision

**Solvency is priced against declared capacity, not against what we can see,
and the bank pays on the earliest legal tick.** `MatchLens.EnemySlotCapacity`
counts opposing lifecycle assignments whose unlock tick has passed — a contract
fact, immune to the vision cone — and `Ledger.WantsReplacement` targets one
body clear of it. A Ready slot is an unpaid debt rather than a reserve; the
float is the rebuild clock. This inverts wave 1's central sentence
("companions are lent, not spent") and nothing else about the banker.

**What I am counting as repairs, stated plainly so a reviewer can disagree.**
Three changes below have strategic side effects and I have not tried to hide
that: the empty-gun footwork rung, the rotate-onto-a-lane suppression, and
remembered-contact coverage. I count them as repairs because each fills a hole
in a rung the wave-1 doctrine had *already declared* — "suppress rather than
concede while we are the ones holding" could not reach a lane, and step 6
silently did nothing once its goal set was satisfied — rather than introducing
a new strategic idea. A reviewer who counts the footwork rung as a second
strategic revision would not be wrong, and the budget line should be read as
"one revision plus three contested repairs" rather than as a clean one.

## Mechanical repairs and template sync

- `ArenaBasics.cs` is carried byte-identical from the current template and is
  the source of `OrderedDirections` (used by every search: route BFS, evasion,
  footwork, aim rotation, placement-facing choice), `Capabilities` (the
  precondition for the economy rung), `ClassOf` (replay-readable debug text
  only — every decision is conditioned on stats and routes), and `Wait` (the
  terminal fallback). Adopting the ordered preference converted the wave-1
  determinism into seed sensitivity: the same three seeds that produced
  identical v1 replays now produce different scores and different completion
  reasons.
- Allied bolts were already excluded from blocking via the contract's
  `AlliedProjectileContact` policy, so the fixed `Occupied` needed no change on
  our side; the covering-fire positioning it enables is what the new footwork
  rung exploits.
- `Field.Covered` counts remembered contacts as well as visible ones, bounded
  by the longest projectile travel any form we do not own declares.
- The empty-gun rung (`Posture`) takes one step to the least-covered tile and
  is hard-restricted to objective tiles while we hold the objective, so it can
  never trade presence for safety. It requires a two-point improvement, which
  is what stops it oscillating.
- `Gunnery.TryRotateToSuppress` turns onto a lane worth a bolt — but **only
  while nothing is visible**. My first attempt let it rotate with enemies in
  sight and it lost the base mirror 0–6: a rotation also swings the vision
  quadrant, and trading a live contact for a remembered one loses the exchange
  you were already watching. With the blind gate the same arm went 2–0–6. That
  was the single most useful measurement of the pass.
- The terminal fallback still constructs arguments for any available action
  before delegating to `ArenaBasics.Wait`, because a form whose only available
  action takes a payload would otherwise fault.

## Movement-arm adaptation

`Kinematics` resolves `MovementProfile.FacingCoupling` for the current form —
the field is optional in canonical contract bytes and its absence means
`PreserveFacing`, which is read, not assumed.

- **`facing-locked`.** The real repair. The movement mask offers only the
  current facing, so wave 1 planned routes against the mask and concluded that
  three quarters of the map was unreachable: it walked forward or waited. v2
  plans routes on the map geometry and pays at emit time — move when the mask
  offers the step, rotate into it when it does not. Route ties break toward the
  *current* facing (randomised laterals only break the remainder), because
  otherwise two equally short routes make the body rotate back and forth
  forever. Result against the frozen v1: **8–0**, breaching at t181.
- **`move-sets-facing`.** Retreat is repriced twice. The bank's standoff
  shrinks by a tile, because every tile of standoff is also a tile of lost
  facing, and any step that turns away from the exchange carries an explicit
  penalty in evasion, staging, and footwork scoring. Placement is the other
  half: wave 1 spent a whole tick rotating so the child would land on the
  exchange, but under this coupling the approach direction *is* the queue-time
  facing, so v2 walks into the pose when a step that way is safe and legal and
  only buys the rotation when it is not. Net against v1: 1W 2L 5D — roughly
  level, and I would not claim more from eight self-mirror games.
- **`preserve-facing`.** Unchanged baseline.

## Measured effect (in-process, v2 versus the frozen v1, 4 seeds each side)

| Arm | v2 record (both sides) |
| --- | --- |
| class mirror, current map | 2W 0L 6D |
| class mirror, thin-fronts | 7W 1L 0D |
| class mirror, outer-shoulder-bypass | 2W 5L 1D |
| `--movement facing-locked` | 8W 0L 0D |
| `--movement move-sets-facing` | 1W 2L 5D |

Every outer-shoulder result is a max-ticks stalemate decided by 3–4 points of
territorial progress against a capture threshold of 15 — under a third of one
capture, with 1–3 deaths per side across 499 ticks. I read that arm as
noise-with-a-side-bias rather than a strategic regression, but it is the one
arm where I cannot show an improvement and I am recording it as such.

Against ranged pressure (my own v1 driving a striker chassis, 4 seeds): v1 is
breached at t445 in **every** seed; v2 is never breached, reaching the tick cap
in all four and drawing one. Fire output roughly doubled (67–84 attacks against
32), average bodies rose from 1.61 to 1.65–1.94, and the banked-slot pathology
is now visible on the *other* side — 280 unspent slot-ticks for v1 against 3
for v2 in one mirror.

Behaviour was confirmed under the controlled WASM runtime: the class mirror and
the facing-locked arm reproduce the in-process completion reason and end tick
exactly, and `nilbots verify` accepts the replays.

## Time

| Step | Wall time |
| --- | --- |
| `dotnet build` of the editing project | 0.4 s |
| in-process class mirror, 4 seeds, 500 ticks | ~6 s |
| cold `nilbots build . --no-cache` (warm Docker builder) | 8.4 s |
| full cumulative suite-5 qualification (T2+T3+T4, both assignments, WASM) | 11.9 s wall / 93 s CPU |
| WASM confirmation match, 2 seeds | ~9 s |

The inner loop is excellent. The cold build was 2.5–3 min in wave 1 and 8 s
here because the Docker builder image was already warm — worth saying out loud,
because the wave-1 note reads like a permanent cost and it is not.

## Documentation gaps and friction

**1. `--movement` documents itself as un-combinable and then combines.** The
help says the arm *"on its own is a standalone arm and cannot be combined with
the other experiment options"* and, two lines earlier, *"may be paired with
`--classes` (and through it `--duel-map`)"*. Since a class declared in
`botarena.json` is not literally `--classes` on the command line, it is not
obvious which sentence applies to two class-declaring projects. It does pair —
the resolved ruleset is
`frontline-labs-1-classes-fabricator-vs-fabricator-sets-facing` — but I had to
run it to find out. One sentence saying that a declared class counts as
`--classes` for combinability would settle it.

**2. There is no way to face a class you did not write.** The class factorial
is the whole point of this wave, but `frontline-labs` ships no built-in generic
actors, so an isolated author has exactly one legal opponent: their own source.
I got a striker to fight by running `--classes fabricator-vs-striker
--ignore-declared-classes` with my own v1 on the striker side, which is a fine
directional A/B but is still my own doctrine wearing a different chassis. A
system-owned, non-strategic calibration opponent per class — the equivalent of
`frontline-probe` for the classed arms — would close the biggest measurement
gap in this pass without leaking anything about the population.

**3. The movement-coupling field is invisible by design and undocumented in the
brief that introduces it.** `MovementProfile.FacingCoupling` is the single most
behaviour-changing fact in the new arms, and the canonical contract *omits it
entirely* for the default — correct, and exactly the shape that makes an author
assume the concept does not exist. The class addendum tabulates chassis stats
but says nothing about movement kinematics; a one-line "read
`MovementProfiles[].facingCoupling`; an absent field means preserve-facing" in
`EXPERIMENTAL-FRONTLINE-CLASSES.md` would have saved a read of
`ActorCanonicalContractReader`.

**4. In-process and WASM agree on behaviour but not on replay hash.** Same
sources, same seed, same arm: identical completion reason, identical end tick,
different `replayHash`. That is presumably provenance and it is fine — but the
rules card tells authors to "confirm candidate behavior in the default WASM
sandbox" without saying what confirmation looks like, and the obvious reading
(hashes match) is wrong. "Compare outcomes, not hashes, across runtimes" would
help.

**5. Hardcoding temptations I resisted, and where.** Enemy body capacity is the
new one: the shortest path to this revision was `EnemyBodies = 3`, since both
teams have three slots in every current arm. It reads unlock ticks from the
contract's lifecycle assignments instead, so the automatic-companion arm and
any future topology get the right answer. The rest survive from wave 1 — unlock
ticks, placement offsets, capture threshold, projectile geometry and the
ranking channel are all resolved, and `Standoff` (3 tiles) remains the only
tuned constant in the bot, now with a documented coupling-dependent
adjustment.

**6. Confusing terminology, still.** "Bank" in this bot means the
automatically-returning slot; the rules card calls it the Prime; the contract
calls it a lifecycle assignment with an `AssignedRespawnSpawnId`. Three names
for one thing across three documents is a small tax every time someone reads
this source next to the rule card.

## Top remaining frictions, ranked

1. No neutral opponent per class — every strategic measurement an isolated
   author can make is against their own doctrine wearing a different chassis.
2. The movement-coupling contract field is invisible by design (absent means
   default) and undocumented in the arm brief that introduces it.
3. The `--movement` combinability sentence contradicts itself for
   class-declaring projects, so the only way to learn whether an arm pairs with
   a declared class is to run it and read the resolved ruleset ID.
