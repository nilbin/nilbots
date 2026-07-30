# DX notes — ledger-fly revision 5 (Frontline classes, the open game)

## Isolation statement

Written from this project's own sources, its own frozen predecessors, its own
qualification report, and matches this entrant played against **its own rebuilt
revision-4 source and its own class-variant copies, and nothing else**. No other
entrant's directory, source, standings, replays, or aggregate balance report was
opened; no scratch directory other than my own was read or written. Permitted
material actually consulted: `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`,
`docs/FRONTLINE-LABS-RULES.md`, `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md` (read in
full), `templates/botarena-generic-actor/` (the scaffold carried byte-identical),
the public SDK types under `src/BotArena.Sdk/`, my own frozen wave-4 directory
(read only, left byte-untouched), and `sandbox/cli-publish/`. Private scratch for
this pass was `sandbox/ledger-fly-w5-scratch-4d9f2c71/` — a uniquely named
directory, not a shared or guessable one.

Two disclosures, both in the spirit of the packet's exposure rule.

1. **The cohort directory is still a shared parent of my output directory**, so
   the ordinary act of listing my own freeze target enumerated four sibling
   entrant directory *names* that were already there when I started. I opened
   none of them: no source file, replay, qualification report, standings table or
   aggregate report belonging to another entrant was read, and every match
   reported below was played against my own rebuilt predecessor or my own
   class-variant copies. This is the same structural exposure my revision-4 notes
   reported, unchanged, and it is still cheap to fix: per-entrant directories
   want to be siblings of the cohort root rather than children of it.
2. **A mid-wave doc correction reached me from the orchestrator**, stating that
   the class-identity paragraph's "Mobilize back once per life" was the
   historical rule and that this arm's anchor⇄mobilize cycle is unlimited. I had
   read the pre-correction text, so I verified the claim rather than swapping one
   assumption for another: `irreversibleForLife` is `false` on the mobilize
   routes in the resolved contract, and an anchoring sparring copy of my own
   source produced 30 anchors and 17 mobilizes in a single match with one life
   cycling three times. My source never encoded either rule — it reads the
   routes — so nothing changed in the freeze, but the *reason* my Anchor refusal
   survives is now weight rather than irreversibility, and that is stated in
   `Stances.cs`. The addendum hash below is the post-correction one.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `ledger-fly` |
| Class | `fabricator` (declared in `botarena.json`) |
| Authoring lineage | `ledger-fly-v1` |
| Revision | 5 (wave-5 cohort, the open game) |
| Role | verdict-doctrine |
| Doctrine | attrition banker |
| Target | cumulative T4 (retain) |
| Budget | one strategic revision; mechanical/contract repairs free |
| Predecessor | `arena-bots/frontline-labs/classes-wave-4-2026-07-30/ledger-fly` (untouched) |
| Primary doctrine cell | the whole game: `--movement facing-locked --pendulum keel --skills kit --bend universal --aim offset --stance-ground open --five-slots wane` |
| Resolved ruleset (primary) | `frontline-labs-1-fabricator-vs-fabricator-crew-facing-locked`, rules fingerprint `b28fb9d001d615b303efa11f1d676f42bcb3a76415966962ff1d698e1f0760fa`, topology `two-team-one-controller-four-slots-v1` |
| Author packet | sha256 `d31b59aad68176694ff9119620976acb455f34069601487a9edf4495e31ac16e` |
| Rules card | sha256 `06ff461e3062e5a5b04455672a4deef9423987f6e80eaad10e089007d94c20e8` |
| Class addendum | sha256 `2333bd3c9f412e4e9439779ef3d5f2ca6bc8abae6f00973daf54f7e4c892de50` (post-correction) |
| Template helper | `templates/botarena-generic-actor/ArenaBasics.cs`, sha256 `567e9faff0546472153f773df504dd252de38a63c5b99a50a9c08b72bd192627` (carried **byte-identical**; verified by diff) |
| Source-tree sha256 | `55d746f0d75a74c69665be11b3f2d679fd47bef6b04668f33e50c7c86a77cd72` |
| Toolchain | nilbots CLI 0.9.21, SDK 0.10.6, game rules 0.5, runtime protocol 0.1 / actor 1.0, NativeAOT-LLVM `10.0.0-rc.1.26306.1`, WASI p1 core module, platform-matched Docker builder (Apple Silicon) |
| Build cache key | `633592429c620903f11c66e742b6cd98866caf02f8c355a4713e50dac37df018` |
| **`out/bot.wasm` sha256** | **`12165ad4ba9f157ff121e76f1632dbcbcf826ac19156da51e7ed1c789b4c1306`** (3,460,884 bytes) |
| Qualification | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, WASM, exit **0**, tier **T4**, `balanceEvidenceEligible: true` |
| `evidence/t4/qualification.json` sha256 | `f819ebb46415dd4738dbcd2a92684dedbfa807de03f20fdc8c2f8f85c9f538b1` |
| T3 prerequisite report sha256 | `ea8ea653952870ddf46e94a31c4f43576e4d8fc9bd8f26dab8948a751bf2681b` |
| T2 prerequisite report sha256 | `b91cb79c36a9fa9e652c7166f30eb580b233c158f8ca55b49f7b19e4d7694998` |
| Verified probe replays | 36 under `evidence/t4/` |
| Sparring baseline | revision-4 source rebuilt `--no-cache` against SDK 0.10.6, artifact `4501ec87fb32af6dfe6c523795fe8779b92ab07abf1c5959baf578d0571b9cc4` |

Per-file sha256 of the submitted set is in `SHA256SUMS`, with the source-tree
hash construction (name, NUL, big-endian length, bytes, sorted) carried unchanged
from revisions 2–4. `Gunnery.cs`, `Ledger.cs`, `LedgerFly.cs`, `MatchLens.cs` and
`Stances.cs` changed; `ArenaBasics.cs`, `Bearings.cs`, `FabricationRoute.cs`,
`Field.cs`, `Kinematics.cs`, `Ratchet.cs`, `LedgerFly.csproj` and `botarena.json`
are byte-identical to revision 4. Every suite-5 probe passed on the first
canonical build of this revision. Two independent `--no-cache` builds of the
final source produced the same cache key and the same artifact hash.

**One identity note worth having in writing.** The brief names this game `deck`;
on a fabricator mirror it resolves to
`…-fabricator-vs-fabricator-crew-facing-locked`, because `--stance-ground open`
touches nothing that exists in that cell and is inert-omitted. The same flag set
does resolve `deck` on `bulwark-vs-fabricator` and `sail-open` on
`bulwark-vs-striker`, both of which I saw in my own class-variant probes. The
documented behaviour is exactly right; it is just startling the first time your
primary cell comes back with a different token than your brief.

## Doctrine in one paragraph

The bank is still the slot the contract returns automatically, children are still
the currency, and the unit of account is still the convertible objective-tick.
What revision 5 adds is the **exchange rate**, because this arm moved it: one
capture is `threshold / gain` ticks of sole presence, one body is the rebuild
clock its own slot declares, and when the second exceeds the first — as it does
here, 22 and 30 ticks against a 15-tick capture — a trade the previous revisions
booked as profitable is a loss, so health, bodies and ground are all quoted in
ticks and no trade settles below its declared price: a bolt buys
`damage / maxHealth` of a body and must be paid for with at least that many ticks
of published claim, exposure on a tile is priced at the same rate rather than a
flat constant, and a kill is worth the clock of the slot it empties, which puts
their late-unlocking child above their early one and a fortified zero-weight body
last of all — still a target, never a priority, because a body that has left the
count has already stopped buying the only thing that wins. The other half is
geometry: a gun whose declared shot program carries an initial-aim range launches
45° off facing with zero bends, so one facing owns three straight rays instead of
one, and under a facing-locked profile — where the facing IS the movement lane —
that is the difference between answering an off-lane contact this tick and paying
a rotation that also cancels the step; the same three lanes make suppression a
real choice and deepen the crossfire two bodies can build on one region. And
because the arm makes fortification free and legal anywhere, the stance gate that
was always about objective weight rather than reversibility now carries the whole
argument by itself: this banker does not delete its own scoring presence for
durability, and it does not spend bolts on an opponent that has.

## Mechanical and contract repairs (free per the brief)

1. **Aim-only diagonals are fired.** Revision 4 read `minInitialAimSteps` /
   `maxInitialAimSteps` and combined the offset with its one bend — but its
   program enumerator started at `max(1, minBendCount)`, so a legal
   *zero-bend* diagonal was never emitted. It is now, using the contract's own
   `aimOnlyProgram` sentinel for the inert curvature, and the family is empty
   where the bounds are zero. This is the largest measured effect in the
   revision (below), and it was a bug in the sense that matters: a legal,
   declared, cheaper shot the bot could not take.
2. **"Can answer off-lane" now includes the aim envelope.** The team-level
   reading that decides whether the bank may sit out an exchange asked only
   whether a bend was declared. An initial aim offset grants the same thing more
   cheaply, so the test is `bend OR aim`. Identical on this arm (both are
   declared); correct on an arm that carries only one.
3. **Suppression uses every lane the facing owns.** `TrySuppress` and
   `TryRotateToSuppress` evaluated the facing ray alone; they now evaluate the
   facing ray plus each declared offset, so a rotation is judged on what it
   actually buys instead of a third of it.
4. **Solvency can no longer name an unreachable target.** The lending rule aimed
   for one body clear of the enemy's declared slot capacity; on an asymmetric
   roster that can exceed our own slot count, which turns a decision into a
   condition that is always true. It is now capped by our own roster. Behaviour
   on this arm is unchanged (a Ready slot still always gets queued) — this is
   honesty in the arithmetic, not a strategy change, and it is reported as such.
5. **Zero-weight bodies are read from the observed form**, so the same test
   serves our own stance gate and the firing order.

## The one strategic revision, and how much of it is really one

One sentence — *price every trade at the rate the contract declares between
bodies and ground* — expressed in three places: the bolt we eat, the tile we
stand on, and the body we shoot. I count it as one because all three are the same
division (`replacement ticks` over `conversion ticks`) applied to the three
things a body can spend, and each is inert unless its field is declared: with a
rebuild clock equal to a capture the eat-the-bolt bar falls back to revision 4's
behaviour, the exposure premium reproduces revision 4's constant **exactly**, and
the firing-order term collapses when every slot shares one clock. A reviewer who
counts the firing order as a second revision would not be wrong, and following
the revision-2/3/4 precedent I would rather say so than hide it. The aim work I
claim as a repair, not a revision, because the capability was declared and the
code was already reading its bounds — it simply never emitted the payload.

## What I measured

Candidate versus the **rebuilt revision-4 source**, `fabricator-vs-fabricator`,
the whole game (`--movement facing-locked --pendulum keel --skills kit --bend
universal --aim offset --stance-ground open --five-slots wane`), both sides, 12
seeds per side in-process (24 matches), confirmed on 6 seeds per side under the
controlled WASM runtime. Records are the candidate's and are resolved by which
artifact played the slot: side *a* runs the candidate as team 0 and side *b* as
team 1, because the CLI's own total is slot-relative.

| build | side a | side b | total |
| --- | --- | --- | --- |
| **revision 5** | 4W 8L | 9W 3L | **13W 11L 0D** |
| the null: predecessor mirrored against itself | 3W 9L | 9W 3L | 12W 12L 0D |
| WASM confirmation (6 seeds × 2) | 4W 2L | 4W 2L | 8W 4L 0D |

**Read the null first.** This arm is strongly side-biased: team 1 wins 9 of 12
seeds whichever artifact holds it, so a *neutral* change scores exactly 12-12
across paired sides and the honest claim for revision 5 is **one net game in
twenty-four**, all of it on side a (3W → 4W). The WASM subset agrees with
in-process seed for seed — identical winners, identical scores, identical end
ticks — and for seeds 42, 7 and 13 the **entire accepted-decision stream is
identical** (1607 / 1623 / 1703 decisions). The replay hashes differ, and only
because runtime provenance is inside the hashed header; `nilbots verify` accepts
the WASM replays.

Twelve seeds produce 7 distinct outcomes per side (rather than revision 4's one
to three), so seeds do something on this arm — but four of the twelve are
side-decided in both directions and the same outcome recurs in pairs. **The
honest unit is a couple of dozen paired games with maybe seven independent
stories in them, not 24 measurements.**

### Which sentence earns it (leave-one-out, same 24 matches)

| build | side a | side b | total | vs revision 5 |
| --- | --- | --- | --- | --- |
| full revision 5 | 4W 8L | 9W 3L | **13W 11L** | — |
| no aim-only diagonals | 4W 8L | 5W 7L | 9W 15L | **−4 games** |
| eat-the-bolt unpriced (revision 4's rung) | 2W 10L | 9W 3L | 11W 13L | −2 games |
| revision-4 firing order | 3W 9L | 9W 3L | 12W 12L | −1 game |
| flat exposure premium (revision 4's constant 12) | 4W 8L | 9W 3L | 13W 11L | **0 — no outcome changed** |

- **The diagonal launch is the load-bearing change**, and removing it is worse
  than the predecessor (9-15), which is the interesting part: the price rung
  makes bodies hold contested tiles longer, and a body that holds a tile it
  cannot shoot off-lane from is a body being shot. The two readings are coupled,
  and I would not have predicted the sign.
- **The exposure premium changed nothing at all.** Not one outcome, on 24
  matches. It is derived rather than tuned (and reproduces revision 4's constant
  when a body costs one capture), it costs nothing measurable, and I am keeping
  it because it is the same sentence as the rung that does earn — but I am **not**
  claiming it, exactly as revision 4 declined to claim its eat-the-bolt rung.
  Same posture, second time; a reviewer entitled to be tired of it should read
  this as the second unfalsified reading this lineage has carried, not the first.

### Skill, slot and diagonal usage (candidate only, 12-match WASM sweep)

| quantity | count |
| --- | --- |
| accepted decisions | 9,468 (98.96 % success, 98 blocked) |
| attacks | 1,219 |
| — straight, payload-free | 663 |
| — bend only | 193 |
| — **diagonal launch + bend** | 276 |
| — **diagonal launch, aim-only (new)** | 87 |
| diagonal launches, total | **363 (30 % of attacks)** |
| fabricate actions | 191 |
| slot-lives fielded | 305 |
| distinct slots fielded | 48 (**4.0 per match** — the whole roster, every match) |
| kills | 289 (their bank 113, early children 140, late child 36) |
| own deaths | 285 |
| volleys cast | **0** |
| shells raised | **0** |
| lanes declined into a raised arc | **0** (a refusal is a non-event and is not published; in this cell no arc can exist) |
| turrets anchored | **0** (by doctrine, and no route exists in this cell) |

Volleys and shells are zero for the same reason they were in revision 4 and it is
still not a code path failure: **a fabricator mirror carries neither skill.**
`--skills kit` resolves per class, `sameLifeTransitions` comes back empty (I
checked the resolved contract, not just the behaviour), and the whole kit in this
cell *is* the slot roster. The four-slot roster is the visible effect: every
match fields all four slots, and the late slot's 30-tick clock is why it is
queued first.

### So I exercised the stances against my own class variants

Diagnostics, not records; every artifact below is a copy of **my own source** with
a different declared class, plus one deliberately modified sparring dummy. No
other entrant's artifact was involved. Seed 42, same game flags (`--five-slots
wane` only where a fabricator is in the pair).

| probe | resolved ruleset | result | stance / cycle activity |
| --- | --- | --- | --- |
| candidate vs my `bulwark` copy | `bulwark-vs-fabricator-deck` | candidate **+15** | 32 shells raised (**7 on objective tiles**), 24 mobilizes, **13 deflections against me** |
| candidate vs my `striker` copy | `fabricator-vs-striker-deck` | candidate **+30**, breach @178 | 9 volleys cast (**3 on objective tiles**), 7 engine auto-returns (`automatic-threshold-return`) |
| candidate vs my anchoring dummy | `bulwark-vs-fabricator-deck` | candidate **+3** | **30 anchors, 17 mobilizes, one life cycling 3 times, 13 anchors on objective tiles** |
| my `bulwark` vs my `striker` copy | `bulwark-vs-striker-sail-open` | bulwark +30, breach @176 | 5 shells (all @obj), 5 mobilizes, 6 volleys (4 @obj), 5 auto-returns, 5 deflections |

Three things came out of these that I could not have learned in my own cell.

1. **My revision-4 friction #1 is fixed by the arm, and I could see it.** Revision
   4's loudest complaint was that the aegis shell is documented as holding ground
   while its route forbade every objective tile. Under `--stance-ground open` the
   route's `forbiddenTileTags` is empty and my unchanged gate raises the shell
   **on the objective** — 7 of 32 raises in one probe, 5 of 5 in another. The gate
   needed no edit because it always asked the legality mask rather than
   enumerating tiles, which is the one design decision from revision 4 I would
   defend hardest.
2. **The turret cycle is real and it is unlimited.** 30 anchors and 17 mobilizes
   in 500 ticks, one life cycling three times, and 13 of those anchors on
   objective tiles — a body fortifying the exact ground it thereby stops scoring.
   That is the bargain the addendum describes, now visible, and it is why my
   firing order sends a zero-weight body to the back rather than refusing it: in
   that probe the candidate chose a scoring body over a visible fortified one 3
   times and still shot the fortified one 26 times when it was the only target.
3. **Deflections against me went up, not down** (13 here versus 3 in the
   equivalent revision-4 probe), and open ground is why: shells now rise where the
   exchange is, and the entry windup is 1 tick, so an arc can appear *after* my
   bolt launches. No refusal can cover that case — it is the tempo tax working as
   designed — which brings me to the reading I deleted.

## Two readings I implemented and deleted

- **Aim off the arc a body has not raised yet.** The natural synthesis of this
  wave's two changes: with three launch headings available, prefer the one whose
  *arrival* falls outside the quadrant a bulwark could still guard, computed from
  its declared guard routes, their windups, and the bolt's own flight time. I
  built it, and it failed twice in an instructive order. Preferring an off-arc
  lane across all targets before an on-arc lane at the best target silently
  **inverted the firing order** the same revision had just priced (probe: +15 →
  −7). Fixed to order lanes within a target, it still did not do its job:
  deflections against me went 13 → 16 and the probe margin fell to +8, because a
  body can rotate before it raises, so the arc I predict from its current facing
  is not the arc it presents. Deleted rather than shipped, and the frozen source
  is byte-identical to the pre-experiment state (same build cache key).
- **Spending the idle tick.** Two thirds of revision 4's `wait` decisions are a
  body on contested ground with a **loaded gun and an empty quadrant** (measured:
  519 of 803 in four matches). I tried the two obvious cures — turn to face the
  direction the front arrives from, and drift to the region edge that matters
  this phase — and both cost games: on the 8-seed subset I was iterating against,
  each turned 10W-6L into 8W-8L, and each collapsed **all** seed variation into a
  pure side effect (every seed identical, team 1 winning the lot, which is its own
  warning sign). The mechanism is worth writing down
  because it generalises: **under `facing-locked`, vision and mobility are the
  same resource.** A rotation buys the quadrant and sells the movement lane, and
  the next tick's footwork has to buy it back, so a body that keeps looking where
  the enemy will be spends its match rotating. That leaves the loaded-gun-empty-
  quadrant tick as this doctrine's clearest remaining inefficiency and I do not
  have a fix that measures.

## Time

| Step | Wall time |
| --- | --- |
| `dotnet build` of the editing project | 0.4–0.5 s |
| in-process match, 500 ticks | ~2.5 s |
| 24-match sweep, 12 seeds, both sides, in-process (8-way parallel) | ~20 s |
| 12-match sweep, 6 seeds, both sides, WASM (8-way parallel) | 9.0 s |
| cold `nilbots build . --no-cache` (warm Docker builder) | 8.4–12.5 s |
| full cumulative suite-5 qualification (T2+T3+T4, both assignments, WASM) | 5.9 s |

The inner loop is still excellent and the parallel-friendly CLI is most of why.
One new cost is worth flagging to whoever runs the next wave: **I filled the
disk.** A 500-tick replay plus its self-contained viewer is ~15 MB, my sweeps
wrote several hundred of them, and with several authors doing the same in one
checkout the volume hit 100 % — at which point every tool that writes a temporary
file fails, including the ones you would use to clean up. A `--no-viewer` flag,
or a summary-only mode for sweep work, would remove the entire failure mode; so
would documenting that sweep output is disposable and should be deleted per
batch, which is what I now do.

## Documentation gaps, frictions, and hardcoding temptations

**1. The aim arm is documented exactly right, and I still missed the shot.** The
addendum says a bolt "may launch at 45° off facing (aim-only, zero bends) or
combine the offset with the one-bend program", the SDK carries
`AimOnlyShotProgramValue` with a doc-comment naming it "Required inert curvature
for aim-only attacks", and my revision-4 code already read both bounds — and
still emitted no zero-bend program, because its enumerator was built around the
bend loops and the offset was an inner dimension of them. Nothing in the docs is
wrong. What would have caught it is a *shape* statement rather than a capability
statement: "with ±1 offsets a facing owns three of the eight rays out of your
tile, not one." That sentence is the whole balance content of the arm and it is
the one I had to derive from a 4-game ablation.

**2. `--stance-ground open` fixed my loudest revision-4 friction and I could not
tell from the contract alone which of two rules had moved.** The shell rising on
an objective is either "stance entry routes lost the tag" or "the tag left the
objective tiles", and those are very different games (the second would also let
turrets anchor for free everywhere). Both my probes' turret anchors on objective
tiles answer it — the tag is gone for every transform placement, as documented —
but the answer came from behaviour. A single line in the addendum's ground
section, "the objective tiles keep the tag for nothing else; it is the routes'
`forbiddenTileTags` that empties", would settle it from the contract.

**3. The five-slot variant table and the resolved contract disagree in emphasis,
and the contract wins for a reason worth stating.** The table describes `wane` as
"trim + a half-step 22-tick ordinary rebuild". What the contract delivers is
three *named lifecycle profiles* with `delayTicks` 22, 30 and 18, and the third
one — the prime's automatic return — is the number my whole doctrine is priced
against. A reader who takes the variant table as the spec learns two of the three
clocks that matter. The addendum already says to read the assignments; the
example it gives is the unlock ticks, and the rebuild clocks deserve the same
sentence.

**4. One fact, two names, still.** Carried unchanged from revision 4 because it
cost me time again: a stance the engine returned by itself is
`FormTransition.Automatic` (a bool) to the bot and
`reason: "automatic-threshold-return"` (a string, no boolean) in replay v3. My
probe analysis script read the replay spelling this time and got the right
answer, but only because I had already been burned. Neither the addendum nor
`REPLAY-FORMAT.md` cross-references the two.

**5. Tooling identity, and a version the brief does not name.** The published CLI
binary is `sandbox/cli-publish/botarena` while the brief, the help text and every
doc say `nilbots`; the binary reports **`nilbots 0.9.21`** where the brief names
0.9.20. Nothing misbehaved and the arm tokens all resolved, but "which binary is
the brief describing" is a question an isolated author cannot answer from inside
the sandbox, and a `--version` that matched the brief (or a brief that named the
artifact hash) would close it.

**6. Self-play still cannot A/B a symmetric reading — and this wave the honest
number is one net game.** My cell is a mirror, the arm is side-biased 9-3, and my
paired-side design cancels the bias but leaves very little signal: 13-11 against
a 12-12 null. The ablations are where the real information is (one of them is
−4), and they only exist because I could rebuild my own predecessor. **A
system-owned non-strategic calibration opponent per class remains the single
biggest measurement gap for an isolated author**, and this wave sharpened it: the
three readings that a fabricator author can actually see are the ones that change
its own geometry, while everything the kit and the turret cycle add lives in the
cross-class cells I can only reach by authoring both sides myself.

**7. Hardcoding temptations resisted.** New this revision: the rebuild clocks 22
and 30 and the prime's 18 (each read from its slot's own lifecycle profile, and
the enemy's from theirs), the four-slot roster and the 60/180/300 unlocks (never
counted or assumed — the wave-4 code that would have played `boom` one unlock
early is gone precisely because it never counted), the capture threshold and gain
behind `ConversionTicks`, the ±1 aim bounds and the aim-only sentinel values, the
one-bend depth, the objective weight of every observed form, and
`irreversibleForLife` on a route whose meaning changed under me mid-wave. The
constant `12` that revision 4 measured for exposure now falls out of two declared
numbers instead of being typed. `Standoff` remains the only tuned constant in the
bot.

## Top remaining frictions, ranked

1. **The aim arm's balance content is a shape fact that no document states.**
   "±1 offsets mean a facing owns three of eight rays" is worth four games to a
   facing-locked chassis — the largest single effect I measured this wave — and
   it is currently something each author must discover by ablation. One sentence
   in the addendum's aim section would hand it to everyone equally, which is
   precisely why it should be *in* the addendum rather than in the winners' DX
   files.
2. **No neutral opponent per class, so a mirror cannot test a symmetric reading,
   and now cannot show much at all.** With this arm's 9-3 side bias, a paired
   24-match sweep against my own predecessor resolves to one net game. Two of my
   three readings are visible only through leave-one-out ablations, and the third
   (exposure) is invisible entirely.
3. **Sweep output is huge and undeleted by default, and running out of disk
   breaks every tool including the cleanup ones.** ~15 MB per match, several
   hundred matches per author, several authors per checkout. `--no-viewer` or a
   summary-only sweep mode is a one-flag fix for a failure that stopped my freeze
   mid-write.

Runner-up, carried from revision 4 and still true: the cohort directory is a
shared parent of every entrant's output directory, so listing your own freeze
enumerates everybody else's, and the provided scaffold still recovers class by
parsing a form-ID prefix (`ArenaBasics.ClassOf`) while the brief forbids it and
the contract publishes the typed field on four surfaces.
