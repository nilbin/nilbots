# DX report — iron-root, wave 4 (revision 4)

## Isolation statement

Written from this revision's own forensics, its own qualification report, and
private sparring runs against this lineage's own rebuilt predecessor and against
variants of this revision's own source. **No other entrant's source, directory,
replays, standings, or aggregate balance report was opened**, and nothing was
read from a shared or guessably named scratch path. This revision's private
scratch was `sandbox/iron-root-v4-scratch-a71e3f`, a uniquely named directory
created for it and used for nothing else.

The permitted material was exactly: the author packet, the Frontline Labs v1
rule card, the experimental classes addendum (read in full), the
`templates/botarena-generic-actor/` scaffold, `src/BotArena.Sdk/` types, this
lineage's own directories and replays, and `sandbox/cli-publish/`. All three
briefed documents were hash-verified before use and match the brief:

```text
d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e  FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md
06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8  FRONTLINE-LABS-RULES.md
b91047df0c0c3e643fd627f45e9f82a0b60b593f986011107125f6ca28c99518  EXPERIMENTAL-FRONTLINE-CLASSES.md
```

All three frozen predecessor trees were left untouched, and still reproduce the
identities recorded in revision 3's own DX report:

```text
0b1cf8673df95cf328a39f90487f383ab6bf653ba5db8ed750e79dde6271e728  wave-1 source tree
ed5c7bccaa98947b9e413d506eeb527c6ffe9e17af2de20cfb3ea10611d18928  wave-1 out/bot.wasm
9bf1b4caebefdfb77b3d608ecd8ce01aa5f54e20ad77cfa73db20033abbd114b  revision-2 source tree
793c4f2e3406c5ea29efdc5b8f4f1ff6830449be4042c7bc52baa589bca4841c  revision-2 out/bot.wasm
f5e2aec627a0eb901034a2db725056a831153f5607b3491520d49b62fc533cc6  revision-3 source tree
00ede717dacf60eb8e778134cc12145a648c057b69fb570b99340c5bf22f7090  revision-3 out/bot.wasm
```

Nothing was committed to git.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, **wave 4** (`classes-wave-4-2026-07-30`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR — revision 4 codename **AEGIS COUNT** |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 (retain) |
| Budget | **one** strategic revision; mechanical/contract repairs free |
| Primary cell | `--pendulum keel --skills kit --bend universal --movement facing-locked` → registered token **`rig`** |
| Predecessors | wave-1, revision-2 and revision-3 directories, all left untouched |
| Scaffold | `templates/botarena-generic-actor/`, `ArenaBasics.cs` synced verbatim |
| Source-tree hash | `16542cad39c662b5f9b2717b52b235807e3675b7e197259f310cbeb293cf1494` |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest **0.10.6**, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `ab07f9782b30b2554f2bb558ae6ee01109c50daca2da9f71059b82a44aa73d5c` |
| **`out/bot.wasm` sha256** | **`ed6e039d407c7eb5ffdf1d4c645699e1f9c3cdfa0461a9dddd6df124a57c22f3`** |
| `evidence/t4/qualification.json` sha256 | `7439a6cd9f83865b77c1f2e6c1e75d7d07c7fa44f1b7ccb3a41e96ddaab2c595` |
| Cumulative T3 prerequisite report sha256 | `7097c5439af63d92f4eda6af6bc8f3667ca47a7ed0c3f1f44649cb653bba4271` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| T3 prerequisite contract fingerprint | `4e77075bd13bbe56485eb29b57c8b916fec9dcd8c9ef9fdaa40fc6fad6944e8e` |
| **Qualification outcome** | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 awarded**, `balanceEvidenceEligible: true`, `profileComplete: true` |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`) with the cumulative T3 and
T2 prerequisites rerun and hash-linked automatically. The suite runs the
duel-depth union profile — no pendulum, no skills, no bend envelope, no
coupling — and this artifact passes it unchanged.

**It did not on the first attempt**, and that failure is the single most useful
thing in this report; it is written up as friction #1.

### Per-file source hashes

Recipe (unchanged across the lineage, stated so it stays reproducible):

```bash
ls *.cs botarena.json IronRoot.csproj | LC_ALL=C sort \
  | xargs shasum -a 256 | shasum -a 256
```

```text
567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627  ArenaBasics.cs
dfb1470dd8ad0a288f094d7127ed102b306aab0c5dc9624c4c581ccc67ba40d3  ArenaGeometry.cs
268eeed074e0eb2d620490328329a94edb26ccd122e66cde62ed9cc20d132dd3  ContractLens.cs
fc658780b30aa96d0aa0de2c9403c8640f48a5b75a3c08524c14c2f5daff3413  FortressPlan.cs
3853f2b17edc6faa032c3f09464f490f5d24673730e29ef1ca69aafac49106ca  Gunnery.cs
a64ecc283171947e8b0c0bf2addc8538163f5939976b73c705c22d219c192962  IronRoot.cs
8d7859751b3e83de3c624a2ae60158112ecfbe788a6d1e9cfb1b99cbc8092573  IronRoot.csproj
4e69aef2560b076e1c114d65385b553b7052a28fcc51c7371887fe882d39032a  Kinematics.cs
a7db54dc981f17411bedb17984885168b03c1c688f2b1b3d08b89722277d8aba  RatchetClock.cs
b983bb8cd98ad1702d15f07c119c124bae512e0fca1147a2e80e0fa02ce2339c  botarena.json
```

`FortressPlan.cs`, `Kinematics.cs` and `IronRoot.csproj` are byte-identical to
revision 3. `RatchetClock.cs` is byte-identical too and is now a fallback only
(see below). `ArenaBasics.cs` is the current scaffold, synced verbatim.
`botarena.json` differs from revision 3 in one field: `sdkVersion` 0.10.4 → 0.10.6.

## Doctrine in one paragraph

**AEGIS COUNT.** This class buys ground with weight that cannot be removed, and
every bolt is priced by the heading it arrives on. Revision 3 priced the keel
counterweights correctly and then, on the level it was built for, never fortified
once — correctly, because where surplus objective weight scales capture pressure
a body converted into a zero-weight gun is a subtraction from the only quantity
that decides the match, so the tenure gate refused every root and the doctrine
spent five hundred ticks as a plain duelist on a contested tile that paid nobody:
zero advances, zero holds, twenty-one deaths a side. The guard stance is the
answer the fortress could not be, because it keeps objective weight instead of
surrendering it, and it turns a bolt arriving inside its facing arc into a bolt
of ours launched from this tile back down the ray the shooter is standing on — so
against an opponent that answers a contested objective with fire, it converts
their cadence into their casualties while the tile never changes hands. It is
raised against the **muzzle** and not the bolt (an enemy's cooldown is redacted
but its cadence is not: the attack is a published event and the cooldown is a
declared number, and a windup-one stance raised in reaction to a bolt in flight
is always exactly one tick late), it is raised out of this chassis's own declared
fire cadence so the cycle costs no shots, it is never raised over a shot that
would remove a body, it may not be re-raised after a stance that turned nothing
until the opposition's own cadence has come round, and read from the other side
the same facts price fire control: a bolt that lands beats a bolt that is turned,
a bolt that is turned still spends a third of a declared budget whose third
deflection shatters the arc into a forced return, and the only shot held back is
one whose own return would kill the shooter. Everything the observation now
publishes is asked rather than reconstructed — the hold's owner and clock, each
bolt's cadence and damage, the reserved spawn tiles, both declared classes, both
slot counts — and every rule is gated on a declared field, so on the two arms
where no form declares a guard this artifact **is** revision 3, decision for
decision.

## Measured per-arm records vs the sparring baseline

Opponent: this lineage's own **revision-3 source rebuilt from the frozen tree**
(`f5e2aec6…`, rebuilt artifact `fb2bd6fc697ade6e37fa565f0c4c1da5b87496e89b2e043881846106d86c4821`).
The frozen revision-3 artifact itself is pre-0.10.6 and faults on these
contracts, so everything sparred against here was compiled from source with
`--no-cache`, as the brief requires.

**WASM runtime (the frozen-cohort standard), all four phase-2 arm combinations,
both sides, five seeds (42, 7, 104729, 1, 2026) — 40 cells.** Score is signed
territorial progress; margin is candidate minus opponent, averaged per cell.
Both artifacts declare `bulwark`, so the pair resolves to a mirror on a
mirror-symmetric map and swapping sides makes the baseline's own margin exactly
zero-sum: any non-zero number is signal rather than side bias.

| kit | bend | registered token | W–L–D | margin |
| --- | --- | --- | --- | --- |
| off | striker-only | `keel` | 0–0–10 | **+0.0** |
| **on** | striker-only | `helm` | **10–0–0** | **+55.4** |
| off | universal | `veer` | 0–0–10 | **+0.0** |
| **on** | **universal** | **`rig`** (primary) | **7–3–0** | **+17.8** |
| | | all | **17–3–20** | **+18.3** |

Supporting per-arm counts, candidate side only, summed over that arm's ten cells:

| arm | advances | obj ticks (mine/theirs) | hold ticks (mine/theirs) | deaths (mine/theirs) |
| --- | --- | --- | --- | --- |
| `keel` | 0 | 1830 / 1830 | 0 / 0 | 210 / 210 |
| `helm` | 39 | 3835 / 3865 | 1025 / 400 | **95 / 148** |
| `veer` | 0 | 1420 / 1420 | 0 / 0 | 210 / 210 |
| `rig` | 68 | 3287 / 3187 | 1288 / 1083 | 121 / 136 |

Read honestly, that is: a large gain on the arm where the kit resolves and the
bend does not, a real but much noisier gain on the full candidate game, and an
exact wash on the two arms whose contracts declare no guard at all.

**The wash is the strongest claim in this report, and it is stronger than equal
scores.** On `keel` and `veer` this revision's accepted actions, arguments,
forms, positions and health are **byte-identical to revision 3's, tick for
tick** (624 and 635 decisions and body states respectively, verified in WASM in a
same-artifact mirror). The artifact that plays the kit arms *is* revision 3
wherever no form declares a projectile guard. That is not a tuning result; it is
proof that the revision is gated on contract fields rather than on an assumed
arm. On the two arms that do declare one, the first divergence is exactly the
shield rising in place of a shot, at tick 53 of the prime's second life.

**The `rig` variance deserves its own sentence.** Seven wins and three losses,
with per-cell margins of +60, −60, +60, +30, −40, +4, +60, +54, −50, +60. The
mean is positive on both sides and every seed contributes both a win and a loss
somewhere, so I read it as a genuinely close cell rather than a side artefact —
but +17.8 on that spread is a weaker claim than +55.4 on ten straight wins, and
I would not defend a difference of ten points there. The most likely mechanical
reason is written up in "what I could not evaluate": under a universal bend the
opponent's bolts can arrive off the cardinal ray, and my muzzle clock models only
the straight arrival, so the arc is sometimes raised against an angle that no
longer comes.

### Skill-usage counts

| | `keel` | `helm` | `veer` | `rig` |
| --- | --- | --- | --- | --- |
| volleys cast | 0 | 0 | 0 | 0 |
| shells raised (completed) | 0 | 170 | 0 | 77 |
| shells broken (automatic-threshold return) | 0 | 0 | 0 | 0 |
| ticks spent inside the stance | 0 | 450 | 0 | 183 |
| bolts turned by my arcs | 0 | 120 | 0 | 87 |
| turret entries | 0 | 16 | 0 | 16 |
| bends fired / shots | 0 / 1060 | 0 / 1373 | 70 / 1050 | 239 / 1264 |
| unit slots fielded (mine / theirs) | 3 / 3 | 3 / 3 | 3 / 3 | 3 / 3 |

Four of those numbers are zeros that need saying out loud rather than hiding in
a table:

- **Volleys cast: 0, everywhere.** Volley is the striker's skill, so a bulwark
  chassis declares no route into a volley stance and `TryCastVolley` returns null
  on every tick of every cell above. It fired on no tick of the striker probe
  either — see friction #3.
- **Shells broken: 0.** Two independent reasons, both honest. My own arcs never
  reach their declared third deflection because the drop conditions fire first
  (120 deflections across 170 stances is 0.7 per stance). And I never break an
  opponent's, because the only opponent the isolation rules permit never raises
  one. The break mechanic is implemented and priced; it is unexercised.
- **Bends fired: 0 on `helm`.** Correct rather than broken: without
  `--bend universal` the bulwark's gun is the parameterless `shoot-straight` and
  there is no program to submit. On `veer` and `rig` the gun becomes `shoot` and
  bends appear — 309 of 2314 shots on those two arms.
- **Slots: 3 and 3.** `five-slots` is the fabricator's skill, so `--skills kit`
  on a bulwark mirror resolves to the shell alone and the CLI says so
  ("requested skills without an owning class in this cell change no contract
  bytes and are dropped"). The count is read from the topology's own slot list
  rather than assumed; the fabricator probe below is where a 5-vs-3 topology
  actually appeared.

### Ablations, from the corrected base

Full table in `evidence/forensics/ablations-wave4.txt`. 24 cells each (4 arms ×
3 seeds × both sides, in-process, same opponent); the candidate's baseline on
those same 24 cells is `+23.7` overall / `+59.0` helm / `+35.7` rig.

| rule removed | ALL | helm | rig | cost of removing it |
| --- | --- | --- | --- | --- |
| the guard stance | +0.0 | +0.0 | +0.0 | **−23.7** — the entire revision |
| a kill outranks armour | +18.8 | +58.7 | +16.7 | −4.9 overall, **−19.0 on rig** |
| the re-raise hysteresis | +17.5 | +42.3 | +27.7 | −6.2 overall, **−16.7 on helm** |
| the patience clause | +23.7 | +59.0 | +35.7 | 0 — never fires |
| arc-aware fire ordering | +23.7 | +59.0 | +35.7 | 0 — provably inert here |

Removing the stance reproduces revision 3 exactly on all four arms, which is the
cleanest attribution this lineage has ever measured: the shell is not part of the
gain, it is all of it.

### Tried, measured, and turned round

Two rules were shipped backwards in the first draft and corrected by
measurement, not by reasoning. Both corrections are worth more than the rules.

1. **Feeding a guard was gated on the guard already being one bolt from
   breaking.** The reasoning was that anything earlier pays for somebody else's
   punish window, and the arithmetic was airtight. The gate can never open: a
   deflection count only reaches its threshold because somebody fed it, so a
   doctrine that waits for two deflections before contributing one contributes
   none. It is a deadlock wearing a proof. Sparred against a variant of this same
   artifact with the refusal removed — the only opponent that raises shields at
   all — the refusal lost **every cell of the full candidate game, by 31 points
   of territory per cell**. Corrected: a bolt that lands beats a bolt that is
   turned, a bolt that is turned beats no bolt, and only a return that would kill
   the shooter is worth holding.

2. **The shield was raised against bolts in flight, inside the normal
   threat-response block.** Measured: thirty-four ticks a match inside the stance
   and **zero deflections**. A transition retains the source form through combat
   and completes after it, and at this chassis's duelling distance every bolt
   lands the tick after it is fired, so a windup-one shield raised in reaction to
   a visible bolt is *always* exactly one tick late. Corrected to answer the
   muzzle, using the published attack event and the declared cooldown, which
   moved the decision one tick earlier and out of the reactive block entirely.

A third correction was found by measurement in a mirror rather than against the
baseline: **two of these doctrines facing each other livelocked**, each raising a
shield the tick the other's muzzle came ready, seeing nothing fired because the
other had also raised one, dropping, and raising again — 223 stance entries a
match, zero deflections, zero advances, and 35 ticks a side on the scoring
surface. The hysteresis clause is the fix and it is worth +6.2 overall against
the baseline as a side effect. A degenerate mirror is not a scoring result, but a
doctrine that produces 223 transform-flickers a match is a policy the balance
cohort should not have to look at.

## Timings (Apple Silicon, warm Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~0.5 s |
| `botarena build --no-cache` (cold, Docker) | **9.4 s** |
| `qualify --suite frontline-qualification-5` (WASM, both assignments) | **5.9 s wall / 6.6 s CPU** |
| one WASM 500-tick class-arm match | ~3.5 s |
| one in-process 500-tick class-arm match | ~3.9 s |
| 24-cell sweep (4 arms × 3 seeds × 2 sides), in-process | ~93 s |
| 40-cell sweep (4 arms × 5 seeds × 2 sides), WASM | **~141 s** |
| one 24-cell single-rule ablation | ~90 s |

The inner loop is still the best thing about this platform. For a revision whose
whole content is one new stance, what mattered was that **a full 24-cell
single-rule ablation costs ninety seconds**, so five of them plus two
variant-versus-variant duels fit inside fifteen minutes and turned two confident
wrong rules into two measured right ones. The qualification suite at six seconds
is the other half of that: it is cheap enough to run after every behavioural
change, which is exactly what I did not do, and friction #1 is the bill.

## Top 3 frictions

### 1. A published reservation names a UNIT, and reading it as a team fact cost this revision its T4

`ObservedTile.SpawnReservation` is the field that replaces revision 3's ugliest
inference — counting three movement refusals per tile and blacklisting it for
fifty ticks — and it is a real improvement. It carries `teamId`, `unitId`, a
kind, and a nullable `dueTick`. I read it the obvious way: an enemy claim blocks
enemy bodies, so **my** team's claim blocks mine, and a permanent one is a wall.

That is wrong, and the docs say so in a sentence written about a different
subject. The rules card's Fabricate section says "The authored Prime spawn
remains reserved against own **child** movement" — the claim is held *for* a
slot, so it blocks this team's *other* bodies and never the claimant. On the
qualification suite's pressure-entry map that anchor sits on the only approach
lane, one tile ahead of the spawn, and it is claimed for unit 0 — the Prime
itself. My Prime treated its own return anchor as a wall, found two equal-length
detours, and oscillated north–south for five hundred ticks without ever reaching
the objective. `entry-initiative` failed with all three criteria unmet and the
suite awarded T3.

Two things would have prevented it. The field's own doc comment says "Visible
lifecycle output claim, or null when this tile is not reserved for a future or
recurring spawn" and `SpawnReservationKind.AutomaticReturn` says "A slot's
authored return anchor is permanently claimed" — neither says *against whom*,
and "permanently claimed" reads as absolute. One clause ("claimed against this
team's other bodies; the named unit may always enter") would have closed it. And
this is the general hazard of replacing an inference with a field, which is worth
stating as a lesson rather than a complaint: **the inference could not make this
mistake**, because it only ever blacklisted tiles a body had actually been
refused. A field is more precise and less forgiving, and swapping one for the
other is a behavioural change that deserves the six-second qualification run I
skipped.

### 2. Nothing connects a windup-one stance to the tick ordering, and the arithmetic silently inverts

The addendum's skill table gives the shell "windup **1**" and describes the
exchange precisely. What it does not say, and what nothing in the player-facing
material says, is when the shield is actually *up*. The transition contract
carries the answer in a policy ID —
`completion: end-of-started-tick-plus-duration-minus-one-after-mode-update` —
which for duration 1 means the form changes at the end of the tick the route was
requested, **after** combat. Combined with the Anchor section's "During the
windup the life remains a targetable, tile-occupying mobile child", the
consequence is that a windup-one stance requested on tick *t* protects nothing on
tick *t* and everything from *t+1*.

That single tick decides whether the skill works. Bolts advance two tiles per
tick and this chassis duels at two to four tiles, so essentially every bolt lands
the tick after it is fired: a shield raised the moment a bolt becomes visible is
*always* too late, and the measured result is a stance that is occupied for
thirty-four ticks a match and deflects nothing. Getting it right requires
predicting the shot rather than the bolt, which requires knowing that an enemy's
cooldown is redacted (`ObservedEnemyState` says so) while its **cadence is
public** — the attack is a published event carrying the actor, and the cooldown
is a declared number on its form's profile. That composition is the actual skill
of using this skill, and it is assembled from four documents.

A related smaller version: `ObservedProjectile.TicksUntilAdvance` is documented
as "ticks remaining until the next advance", and the scaffold's `Threat` helper
returns `TicksUntilArrival`. Both are correct and both are one-based with respect
to the current tick — a bolt reporting 1 lands during *this* tick's resolution,
not the next one. I got that off by one in the first draft and only found it by
tracing a replay tick by tick.

### 3. `--skills kit` on a mirror is one skill, and two thirds of the assignment is unmeasurable from inside it

The brief asks for volleys, shells, five slots and bends. The isolation rules
permit sparring against this lineage's own predecessor and its own variants.
Those two constraints are exactly incompatible for the class-owned skills,
because **each skill is owned by one class and a lineage has one class**. On a
bulwark mirror `--skills kit` resolves to the shell alone; the CLI is admirably
explicit about it ("requested skills without an owning class in this cell change
no contract bytes and are dropped"), but the consequence is that volley and
five-slots cannot be exercised as doctrine at all, only handled as contract.

I did what the permission allows and probed with copies of my own source
declaring the other two classes (`evidence/forensics/parity-and-crossclass.txt`).
That established real things — the `asymmetric-slots-5-3-v1` topology resolves and
is read from the slot list, the volley forms and routes are found, both cells run
500 ticks with no fault — and it also exposed the limit: **the volley cast never
fired**, on either side, because my gate wants more than one body inside the fan
the muzzle already faces and that configuration did not occur. So I have a
contract-driven volley path that is provably reachable and empirically never
taken, which is precisely the "unused reader" that revision 3's DX warned about.
I have left it deliberately as the smallest correct thing rather than tuning a
rule for a chassis I cannot score, and I am recording it as a gap rather than
implying it works.

The bend has the sharper version of the same problem, and here I could at least
settle it by enumeration rather than by sparring. The whole reason I wanted
guard-aware fire control was "go around the arc": a bend that enters the same
tile on a heading the arc does not cover. Enumerating this chassis's declared
envelope over the whole reach —
`initialAimSteps 0..0, bendDirection ±1, bendAfterTiles 1..2, bendEveryTiles 1,
bendCount 1` — gives **zero** such programs at every distance
(`evidence/forensics/bend-envelope.txt`). With no initial aim offset and exactly
one bend, a bent bolt leaves the straight ray permanently and never comes back to
it. For a bulwark the universal bend is worth eighteen off-axis tiles a
straight-only gun can never touch, and it is worth nothing at all as an angle. It
took an enumeration to learn that, and the addendum's "bulwark and fabricator get
**1–2**" reads like a weaker version of the striker's envelope rather than a
qualitatively different one.

## Documentation gaps

Beyond the three frictions above:

- **`ratchetHoldTicks` versus the published clock.** Revision 3 asked for
  "a nullable `holdOwnerTeamId` / `holdRemainingTicks` pair on the Frontline
  observation" and said it "would delete `RatchetClock.cs` entirely". It very
  nearly did: `holdOwnerTeamId` / `holdEndsAtTick` arrived, the scaffold's
  `LiveHold` is the one-line version, and three chained derivations collapsed
  into one read. The addendum's write-up of what it replaces is the best piece of
  porting documentation in the material. One gap remains: **null is
  indistinguishable from absent**, so a bot cannot tell "no hold binds" from "an
  observation schema that does not publish holds". I kept the inference as a
  contradiction check — the contract declares a duration, the arithmetic says a
  hold must be running, and the observation names none — which is the only case a
  reconstruction can still settle. On every contract this revision can run, that
  branch is unreachable, and I would rather say so than imply it is exercised.
- **`automaticReturn` is documented on the route and invisible on the enemy.**
  The counter and threshold are declared, the `projectile-deflected` event names
  the guard, and a break is published with `automatic: true` — so counting an
  enemy shield's spent budget works. What is not published is the counter's
  *current value* on a visible body, so a bot that loses sight of a guard and
  regains it must either keep a stale tally or restart at zero. I keep the tally
  and drop it when the body is seen out of the form, which is right by the
  declared semantics ("restarts on entry and never survives it") but is a
  derivation the observation could simply answer.
- **`ProjectilesPerAttack` exists; the fan's GEOMETRY does not.** `AttackVolley`
  publishes `ProjectileCount`, `Spread` and `IdentityOrder` as strings. The
  addendum's prose says "your facing lane and both adjacent 45-degree headings",
  which for count 3 implies facing ± 1 octant — but the mapping from a `Spread`
  policy ID to a set of headings is nowhere machine-readable, so a bot must
  either parse a policy string or reimplement the prose. I reimplemented the
  prose as `(count − 1) / 2` octants either side, which is a guess that happens
  to match count 3 and would be wrong for an even count.
- **The turret's `mobilize` and the stance's `mobilize` are the same action ID
  with different semantics.** `mobilize` is parameterless and the source form
  decides the target, which is elegant — but the turret route declares
  `irreversibleForLife: true` and the stance route declares `false`, and both are
  reached through the same catalog entry. A doctrine that treats "the mobilize
  action" as one thing will spend a one-use route thinking it dropped a shield. I
  key everything on the resolved route rather than the action ID, which is what
  the addendum tells you to do, but the collision is a real trap.

## Hardcoding temptations

All resisted; the new ones this revision created:

- **"Immobile means fortified."** This was revision 3's actual derivation and it
  was correct for as long as the turret was the only immobile form. The kit adds
  a second one that keeps its objective weight, and under the old rule both
  routes landed in the same dictionary where `TryAdd` picked whichever the
  catalog listed first — so the doctrine's one-use fortify route was decided by
  collection order. Routes are now classified by what the target form
  *declares*: objective weight zero is fortified, a projectile guard is guarded,
  more than one projectile per attack is a volley.
- **"The shield breaks on the third deflection."** It is
  `automaticReturn.threshold` on the return route, absent on every route with no
  budget, and a guard with no declared budget never breaks at all — which changes
  the answer to "is feeding this worth anything" from yes to no.
- **"Windup 1 in, 1 out."** Both come from their routes, and the cooldown-shadow
  rule compares their sum against the gun's own declared cadence. A chassis with
  a fast gun correctly declines to cycle; one with a slow gun correctly cycles.
- **"Three slots."** Counted from the topology's own slot list, separately for
  each side, because an asymmetric arm exists. Also: the *unlock* ticks come from
  the lifecycle assignments, never from 60/180/300/420 or 120/260.
- **"Bulwark, striker, fabricator."** The class is read from
  `Topology.Teams[].ClassId` for both sides. The scaffold's own `ClassOf` helper
  parses a form-ID prefix and says so; the addendum says explicitly not to, and
  the typed field exists, so the scaffold helper is the one piece of the template
  this revision deliberately does not use.
- **"The heaviest declared hit, the fastest declared cadence."** Revision 3's
  conservative substitute for two facts it could not read. Both are per-projectile
  fields now, which matters exactly because a fan bolt, an ordinary bolt and a
  returned bolt need not agree — and a returned bolt carries the damage class of
  the bolt it was, not of the form that launched it.

## Confusing terminology

Carried forward and still true: "Anchor"/"Mobilize" are prose words with no
contract representation; `irreversibleForLife` reads backwards on the reverse
route; "Available" versus "will succeed"; "facing-locked" restricts movement, not
rotation; "hold" is three different things; a spent capture has no event.

New this revision:

- **"Stance" covers two opposite trades.** The addendum groups volley and shell
  as "stances" and gives them a shared budget grammar, which is exactly right
  mechanically. Strategically they are opposites: one gives up mobility to
  multiply a gun, the other gives up the gun to keep a tile. The word that
  actually matters for both — whether objective weight survives — is in the form
  table, three sections away from the skill table that changes what it is worth.
- **"Deflect" sounds defensive and is an attack.** The table says the bolt "dies
  on the arc" and then says a new one launches under the guard's ownership. Those
  are one event with one name, and the offensive half is the reason the skill is
  interesting. My source calls the outcome "turning" a bolt for exactly that
  reason, which is a private word the contract does not know.
- **"Facing quadrant" is a vision term reused as a collision term.** The bulwark
  sees a facing quadrant, and the guard catches contacts "arriving inside its
  facing quadrant". A bolt travelling east *arrives from* the west, so the arc
  sees the reverse of the bolt's heading — and the addendum's phrasing is about
  where the bolt came from while the observation gives you where it is going.
  Getting that backwards inverts the whole skill and the sign error is silent.
- **"The kit" is not a fixed set.** `--skills kit` resolves per class, so on a
  mirror it can be one skill and in a fabricator mirror it can be five slots and
  nothing else. The addendum states this plainly; it still reads as a superset
  every time.

## Repairs and strategy passes

One strategic revision; everything else is mechanical, each driven by a
measurement or a contract read.

1. **Strategy — AEGIS COUNT** (the one revision: the guard stance, raised
   against the muzzle out of the gun's own idle window, plus the fire-control
   ordering that prices a bolt by the heading it arrives on; two sub-rules
   measured, two measured inert, two shipped backwards and corrected by
   measurement).
2. **Repair — the hold is read, not reconstructed.** `ArenaBasics.LiveHold`
   replaces revision 3's four-step inference; `RatchetClock.cs` survives
   byte-identical as a contradiction-check fallback and is unreachable on every
   contract this revision can run. Said plainly rather than dressed up.
3. **Repair — routes classified by declaration, not by immobility.** See the
   hardcoding note; this one would have silently mis-driven the shell with the
   turret's rules.
4. **Repair — per-projectile threat arithmetic** through
   `ArenaBasics.Threat(projectile, tile)`, with this doctrine's own wall
   occlusion kept on top. Behaviour-neutral on the four measured arms (every
   profile there declares cadence 1 and damage 1, which is also why the parity
   proof is exact), and correct rather than pessimistic the moment a fan bolt or
   a returned bolt disagrees.
5. **Repair — spawn reservations asked instead of learned**, and then repaired
   again after it cost the T4: the claim is scoped to a unit. The refusal counter
   is retained underneath, because it still catches blockages no reservation
   explains.
6. **Repair — both classes and both slot counts read from the topology**, and
   the scaffold's form-ID-prefix class helper deliberately not used.
7. **Repair — `sdkVersion` 0.10.4 → 0.10.6.** The frozen revision-3 artifact
   faults at tick 0 on these contracts; everything sparred against was rebuilt
   from source.

## What I could not evaluate

- **Two thirds of the skill kit, as doctrine.** Volley and five-slots belong to
  other classes and a lineage has one class, so the only thing I could establish
  is that both resolve, are read, and do not fault. Friction #3 has the detail.
  In particular the volley cast is reachable and was never taken.
- **Breaking a guard.** Implemented, priced, and unexercised: the only permitted
  opponent never raises one, and my own arcs never reach their third deflection.
  The 31-point measurement that corrected the feed gate came from a variant of
  this artifact, which is the strongest evidence the isolation rules allow and is
  still one opponent.
- **Whether the arc-aware ordering is worth its code.** It measures exactly zero
  against every opponent I am allowed, and the enumeration proves the mechanism it
  was built for is unavailable to this chassis. I kept it because one sub-clause
  is live — the survivability price on a fed bolt — and because it makes feeding
  a decision rather than an accident, which the brief asks for. A reader could
  reasonably call that forty lines of correct dead weight, and I would not argue
  hard.
- **The `rig` variance, and one specific suspected cause.** Under a universal
  bend the opponent's bolts can arrive off the cardinal ray, and `MuzzleClock`
  models only the straight arrival heading, so the arc is sometimes raised
  against an angle that no longer comes. Extending the muzzle clock to the
  opponent's declared bend envelope is the obvious next move; I designed it, did
  not implement it, and therefore do not claim it.
- **Rotating to face a threat before raising the shield.** The protected quadrant
  is chosen before the shield rises, so a body under fire from outside its arc
  could turn first and armour next tick. Two ticks instead of one, and a real
  thrash risk. Reasoned and declined without measurement, which is the honest
  label for it.
- **Seeds, in a strict self-mirror.** Neither this artifact nor its predecessor
  consults `context.Random`, so a same-artifact mirror on a fixed map produces a
  byte-identical replay for every seed — I verified this across 42, 7 and 104729
  before trusting any mirror number. Candidate-versus-baseline cells are *not*
  seed-invariant (the `helm` and `rig` per-cell margins differ by seed), so the
  five-seed sweep is informative there; but "several seeds" buys less on this
  pairing than it looks, and a reader should weigh the twenty non-degenerate
  cells rather than the forty.
