using System.Text.Json;
using System.Text.Json.Serialization;
using BotArena.Engine;

namespace BotArena.Toolchain;

public sealed record BotManifest
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("entryType")] public required string EntryType { get; init; }
    [JsonPropertyName("sdkVersion")] public string SdkVersion { get; init; } = "0.1";
    [JsonPropertyName("appearance")] public BotAppearance? Appearance { get; init; }
    /// <summary>Optional default for the CLI's --rules when this project is the --bot
    /// (gen-3 finding: repeating --rules on every command is easy to silently drop
    /// while practicing for a rules experiment). An explicit flag always wins.</summary>
    [JsonPropertyName("rules")] public string? Rules { get; init; }
    /// <summary>The bot's class, chosen at creation (DECISIONS #154): a
    /// Frontline Labs class ID such as "striker". A classed bot always plays
    /// its declared chassis — the experiment command resolves the class arm
    /// and team binding from both entrants' declarations. Null means the bot
    /// is class-agnostic (base contracts and smoke populations).</summary>
    [JsonPropertyName("class")] public string? Class { get; init; }
}

public sealed record BotAppearance
{
    [JsonPropertyName("accent")] public string? Accent { get; init; }
    [JsonPropertyName("look")] public string? Look { get; init; }
    [JsonPropertyName("projectile")] public string? Projectile { get; init; }
}

/// <summary>Pinned toolchain identity. Every value participates in the build-cache key —
/// bump any of them and every bot rebuilds (plan §12).</summary>
public static class ToolchainInfo
{
    /// <summary>The published `Nilbots` tool version. MUST be bumped whenever
    /// SdkVersion or BuildPipelineVersion changes: those decide artifact bytes, and
    /// `submit` refuses against a server the installed tool cannot match, so a player
    /// needs a NEW tool version to upgrade to (DECISIONS #93). 0.9.x carries
    /// SDK/Guest 0.10.x, build pipeline 4, the frozen local Frontline
    /// actor/replay-v2 experiment, and the negotiated generic actor-match
    /// programming model. 0.9.1 adds the exact local Frontline Labs runner,
    /// generic-actor scaffold, and replay-v3 verification without changing
    /// SDK/Guest bytes. 0.9.4 carries SDK/Guest 0.10.3, whose generic
    /// Frontline contract can expose optional capture-gain schedules while
    /// retaining the exact static hosted-v1 bytes. It also carries the
    /// approved Obsidian Foundry effects
    /// in self-contained replay viewers and the generated HTTP contracts used
    /// by CLI server commands. 0.9.5 carries SDK/Guest 0.10.4 and the additive
    /// declared automatic-activation lifecycle contract used only by explicit
    /// experimental candidates. 0.9.6 adds the profile-scoped, WASM-only
    /// foundation and cumulative qualification runners without changing
    /// SDK/Guest bytes. 0.9.7 selects the platform-matched Docker NativeAOT
    /// compiler host and guards the emulated fallback against multi-node
    /// stalls, while preserving player artifact bytes and cache keys. 0.9.8
    /// adds the pendulum counterweight arms. 0.9.9 adds the class-skill kit
    /// (VOLLEY / AEGIS SHELL / FIVE SLOTS) — multi-projectile attacks, a
    /// form-level projectile guard with its own observed event, and asymmetric
    /// slot topology — all additive, so player artifact bytes and cache keys
    /// are unchanged. 0.9.10 converts AEGIS SHELL from absorption to
    /// DEFLECTION on owner ruling: the guard returns a team-flipped bolt along
    /// the reversed heading, the observed event becomes projectile-deflected
    /// and names both bolts, the stance's arc locks on entry, and the arm is
    /// identified `parry`. 0.9.11 makes the kit adoption-grade and follows
    /// the fight: one threshold-triggered automatic return serves both
    /// stances (VOLLEY after one fan, AEGIS SHELL after three deflections),
    /// so a form-transition event now carries its cause and a same-life
    /// route may declare an automatic-return trigger — both omitted while
    /// inert, so player artifact bytes and cache keys are unchanged; the
    /// two stance arms are reidentified `cast` and `break` because their
    /// behaviour changed, and `--bend universal` hands every class's mobile
    /// gun the one-bend grammar at its own depth. The same version carries
    /// the packaged viewer's camera (follows the active lives, with manual
    /// override and a fit toggle) and arrival materialization for
    /// fabrication, automatic return, and automatic activation — viewer
    /// changes ride the CLI compatibility surface. 0.9.12 makes the forward
    /// rally mirror-fair: the rally arms select a new lifecycle placement
    /// that orders the own-side objective region along the placing team's own
    /// advance axis instead of by absolute map order, so the two mirror-image
    /// rally regions hand the two sides reflected tiles. The historical
    /// absolute-order placement stays defined and resolvable for archived
    /// replays; rally-carrying arm fingerprints move, every other arm and the
    /// hosted contract stay byte-identical, and player artifact bytes and
    /// cache keys are unchanged. 0.9.13 registers `--pendulum keel`, the
    /// phase-1b level that composes every built counterweight
    /// (ratchet-contest plus enemy-sole-decay) under one short token, so
    /// those cells fit the 64-character canonical ID budget beside a class
    /// pair; it renames nothing, every existing arm and the hosted contract
    /// stay byte-identical, and player artifact bytes and cache keys are
    /// unchanged. 0.9.14 is the batched between-phase observability bump
    /// (DECISIONS #169): the Frontline mode observation publishes the live
    /// territory-ratchet hold's owner and expiry, and ObservedProjectile
    /// publishes the firing profile's advance cadence and damage per hit. All
    /// four were already authoritative engine-side and none is a rules fact,
    /// so every ruleset — the hosted contract included — keeps byte-identical
    /// rules, map, and match fingerprints, and the negotiated observation
    /// schema version is unchanged because the new fields are trailing tagged
    /// fields an older guest ignores. Replay-v3 documents grow two mode keys
    /// and two projectile keys, so a replay recorded before this version is
    /// not readable by the new reader and vice versa. The same version
    /// registers the phase-2 composite arm identities `helm`, `veer`, and
    /// `rig`, which rename nothing that already existed. 0.9.15 carries
    /// SDK/Guest 0.10.6: typed class identity becomes a first-class contract
    /// and observation fact, and a visible tile publishes the spawn
    /// reservation that makes it unavailable. The canonical `classId` is
    /// emitted only where a ruleset declares classes, following the #156
    /// additive pattern exactly, so every class-free ruleset — the hosted
    /// contract included — keeps byte-identical topology and match
    /// fingerprints and the pinned goldens are untouched; a class arm was
    /// already a distinct content-identified ruleset. The observation fields
    /// are trailing tagged additions, so the negotiated schema version is
    /// unchanged and no new contract profile was minted. Replay-v3 documents
    /// grow four `classId` keys and one `spawnReservation` key, so a replay
    /// recorded before this version is not interchangeable with one after.
    /// 0.9.16 fixes the volley/deflection identity interleave the phase-2
    /// factorial's cross-class cells surfaced: a fan bolt deflected during
    /// its own launch traversal minted the return's identity mid-fan, so the
    /// session violated its own contract promise of
    /// contiguous-ascending-in-launch-order volley identities and the
    /// chronology validator (correctly) rejected the match. The fan's
    /// identity block is now reserved before any bolt flies. No previously
    /// valid replay changes: single-bolt attacks allocate identically, and
    /// every affected match faulted rather than producing a replay.
    /// 0.9.17 registers the FIVE SLOTS tuning variants (DECISIONS #171):
    /// `--five-slots trim|boom|drag` mint suffixed ruleset identities that
    /// move one lever each (slot count, extra-slot schedule, ordinary-child
    /// rebuild clock). The default `full` arm and every existing ruleset are
    /// byte-identical; player artifact bytes and cache keys are unchanged.
    /// 0.9.18 adds the round-2 composites `moor` (trim + drag) and `wane`
    /// (trim + a half-step 22-tick rebuild) after round 1 measured the two
    /// single levers fixing different edges. Existing rulesets stay
    /// byte-identical; artifact bytes and cache keys are unchanged.
    /// 0.9.19 registers the stance-ground arm (`--stance-ground free`, and
    /// wane + free as the composite `berth`): the VOLLEY and AEGIS SHELL
    /// entry routes drop the transition-placement-forbidden tag kind, so a
    /// skill stance can rise on objective tiles and in the corridor; turret
    /// anchors keep the tag. Pure route data — existing rulesets stay
    /// byte-identical.
    /// 0.9.20 registers the aim arm (`--aim offset`; composites `sail` =
    /// rig + aim and `crew` = the tuned game rig + aim + wane): the ±1-sector initial launch offset returns to every
    /// class's mobile gun (DECISIONS #173 — the one-bend grammar had
    /// dropped it by conflation). Existing rulesets stay byte-identical.
    /// 0.9.21 is a replay-viewer revision plus the open game: the follow
    /// camera centres the action instead of sliding the frame back inside
    /// the map, and playback holds a screen wake lock (DECISIONS #175).
    /// The same version registers `--stance-ground open` and the `deck`
    /// composite (DECISIONS #176): every transform placement is free —
    /// turret anchors included — and the turret becomes a true cycle
    /// (anchor⇄mobilize unlimited per life, health mapped by the existing
    /// PreserveRatioFloorMinimumOne policy in both directions, entry heal
    /// removed). A ground arm is inert-omitted where nothing it touches
    /// exists, so one flag set serves every pair. Existing rulesets stay
    /// byte-identical.
    /// 0.9.22 makes the experiment command's self-contained viewer OPT-IN
    /// (--viewer, or implied by --open): the viewer embeds the whole replay
    /// into a multi-megabyte theme template and was most of a sweep's disk
    /// footprint — a wave-5 full-disk incident produced replays that parsed
    /// and lied. Replay writes now verify byte length through a temp file
    /// and fail loudly instead. play/replay keep writing viewers. The same
    /// version raises the submission source cap from 256 KB to 2 MB (owner
    /// ruling: the cap had become the binding constraint on contract-driven
    /// authorship).
    /// 0.9.23 registers the cooldown-clock arm (`--cooldown ticking`,
    /// contract fact `tickResolution.cooldownClock`, composite `tide` =
    /// the open game on the ticking clock): the attack cooldown advances
    /// with time in every form, ending the gunless-stance freeze (#180).
    /// Inert-omitted, so every existing ruleset keeps its exact bytes.
    /// 0.9.24 carries SDK/Guest 0.10.7: the canonical reader accepts the
    /// two trailing additive contract facts of this window —
    /// `tickResolution.cooldownClock` and the ROUTE COOLDOWN capability
    /// (#181, `sameLifeTransitions[].cooldownTicks`): a cooldown-bearing
    /// route is refused for its declared window after each completion,
    /// held per unit slot so it survives the body, with automatic
    /// returns exempt. Both are inert-omitted, so every existing ruleset
    /// keeps its exact bytes; frozen pre-0.10.7 artifacts fault on
    /// contracts that CARRY either fact until rebuilt — the accepted
    /// #170 consequence. No skill declares a route cooldown yet; the
    /// observation publication of remaining ticks is bound to the first
    /// skill that does.
    /// 0.9.25 registers the volley salvo (`--volley salvo`, composite
    /// `swell` = tide + salvo, #182/#183 — the entry-2 first mint `surf`
    /// re-minted when the entry sharpened to the uniform 1-tick grammar):
    /// every fan bolt deals 2, the fan's profile counter drops to the
    /// 1-tick floor, and the stance entry
    /// routes carry the first declared route cooldown (8 ticks) — which
    /// binds the #181 readability law, so it carries SDK/Guest 0.10.8:
    /// live route-cooldown clocks are published in observations
    /// (self/ally `routeCooldowns`, trailing tagged wire field, replay-v3
    /// `routeCooldowns` key present only while live). Cast cells and
    /// every clockless observation keep their exact bytes.
    /// 0.9.26 carries SDK/Guest 0.10.9: TEAM RANDOMNESS
    /// (`GenericActorContext.TeamRandom`). Teams have no channel, so
    /// coordination has only ever worked because every life on a team gets
    /// the identical frozen observation and any pure function of it is a
    /// shared plan; per-life `Random` silently broke that for any plan that
    /// touched it (the wave-6 finding — the scaffold's own
    /// `OrderedDirections` consumed it and desynced authors' sweeps).
    /// `TeamRandom` is re-derived per tick from a host-derived team seed, so
    /// a life born mid-match agrees with its teammates on its FIRST tick and
    /// no teammate's private draws can shift the shared plan. No rules bytes
    /// move: the team seed rides MatchStart as a trailing tagged field, every
    /// schema version stays, and a frozen artifact still negotiates and runs
    /// — it simply cannot see the stream. Replay-v3 life starts grow one
    /// `teamRandomSeed` key, so the engine-authored fixtures were regenerated
    /// and a replay written before this version is not interchangeable with
    /// one written after.
    /// The same version adds the MUSTER side objective
    /// (`--side-objective muster`, `docs/DESIGN-SIDE-OBJECTIVES-2026-07-30.md`)
    /// and the shared secondary-control capability underneath it: a
    /// capturable site off the frontline chain, latched by sole objective
    /// weight, whose owner's PRIME automatic returns take the forward rally
    /// placement the keel used to hand both teams for free. It runs on its
    /// own map generation (`frontline-labs-02-muster`, alcoves widened to two
    /// approach headings) and publishes exactly two facts, so SDK/Guest
    /// 0.10.9 also carries `secondaryOwnerTeamId` and
    /// `secondaryClaimProgress` on the Frontline mode observation. No rules
    /// bytes move for anyone who does not declare a side objective: the
    /// `gameMode.secondaryControl` block is inert-omitted, so every existing
    /// ruleset — the hosted `frontline-labs-1` included — keeps its exact
    /// fingerprints. Replay-v3 Frontline mode states grow the same two keys.
    /// 0.9.27 registers the CAPTURE CHANNEL (`--capture channel`, composite
    /// `siege` = swell + channel, DECISIONS #187 /
    /// `docs/DESIGN-SCRAP-ECONOMY-2026-07-30.md` parts 2–3). Taking ground
    /// becomes a channel: capture gain counts only bodies that did not
    /// change tile this tick while denial counts all of them, hostile damage
    /// to a CONTROLLING body standing ON the objective reverts that team's
    /// work on the current run by the damage amount (never past where the
    /// run began, and damage to a body OFF the objective reverts nothing —
    /// which is what makes screening the channeler the intended play), the
    /// stationary gain multiplier is capped at 2, an opposing claim erodes
    /// at 8× build speed, and the paired `channel-speed` factor moves the
    /// threshold 15 → 8. It carries SDK/Guest 0.10.10, because the contract
    /// grows three trailing additive capture facts
    /// (`stationaryGainMultiplierCap`, `opposingErosionMultiplier`,
    /// `claimInterrupt`) plus one appended `controlPolicy` value. All three
    /// are inert-omitted together, so every existing ruleset — the hosted
    /// `frontline-labs-1` included — keeps its exact fingerprints; frozen
    /// pre-0.10.10 artifacts fault on a contract that CARRIES them until
    /// rebuilt, which is the accepted #170 consequence. ZERO new observation
    /// facts: every channel rule moves `captureProgress` and
    /// `claimingTeamId` in their exact published shape, so a frozen artifact
    /// that never reads the contract still plays a channel cell — it simply
    /// cannot tell why the number moved.
    /// The same version registers the SCRAP ECONOMY (`--economy
    /// scrap`, composites `forge` = swell + scrap and `bastion` = swell +
    /// channel + scrap; the control level is `--economy scrap-flat`).
    /// Deposits worth 8 arrive at (11,1) and (11,13) every 70 ticks from 60
    /// through 620, every destroyed body leaves a wreck worth 2 at its tile,
    /// stepping onto a pile banks 1 instantly and loads the rest up to a
    /// carry of 6, a load banks in full on your own home pad and drops with
    /// you when you die, and piles expire 80 ticks after they appear. The
    /// bank buys tiers on `edge`, `plate` and `optic` at a flat 10 each — at
    /// most 2 in a track, a full board of 6, applied to the Prime slot's lives —
    /// through the new `invest` verb (action code 106, kind
    /// `mode-investment`, one `upgrade-track` argument), which costs the
    /// casting body its action for the tick and whose affordability lives in
    /// the legality mask. Unlike the channel this arm DOES cost observation:
    /// three trailing additive facts (`mode.scrapTeams`, `mode.scrapPiles`,
    /// and `carriedScrap` on self, allies and visible enemies), all empty or
    /// zero on every ruleset that declares no economy, and no new event kind
    /// at all — every purchase and bank change rides the existing
    /// mode-changed fact carrying the post-change state.
    /// The same version carries the post-wave-8 RULING WINDOW, which moves
    /// numbers on two arms that were already measured and adds two more. The
    /// identity discipline applies in full: every token below is a re-mint,
    /// and the wave-8 spellings keep meaning the wave-8 bytes for ever.
    /// - The channel's opposing-erosion multiplier moves 4 -> 8 ("recapture
    ///   needs to be faster"): one controlling tick now erases even a maximal
    ///   standing claim, so a full flip costs 9 ticks against a fresh
    ///   capture's 8 (1.125x, down from 1.25x). `siege`/`sap`/`mantlet`
    ///   re-mint as `storm`/`mine`/`pavise`.
    /// - SCRAP re-tunes to v1.1 ("the new mechanism needs to be stronger and
    ///   happen earlier"): the first deposit lands at 60 instead of 120, the
    ///   cadence tightens 80 -> 70 and the series runs to nine events
    ///   (60/130/200/270/340/410/480/550/620), a deposit is worth 8 instead
    ///   of 6, and a wreck is worth 2 instead of 1. Carry cap, pile lifetime
    ///   and the flat 10-per-tier price are unchanged — the arm gets stronger
    ///   through affordability rather than through effect inflation, which is
    ///   what protects the class-gap admission rule. The three-tier TOTAL cap
    ///   is REMOVED on the owner's ruling that the economy should be able to
    ///   decide a match: the board is now six tiers (2 per track, 60 scrap).
    ///   `forge`/`anvil`/`smelter` re-mint as `foundry`/`bellows`/`furnace`
    ///   and `bastion`/`redoubt`/`smithy` as `citadel`/`rampart`/`armoury`.
    ///   One behaviour arrives with the tighter cadence: an untaken deposit is
    ///   still standing when the next lands on the same tile, so the two merge
    ///   and a neglected lane accumulates.
    /// - `--roster legion` is a new arm: every team starts with three live
    ///   bodies (the fabricator with a fourth, standing — the owner ruled its
    ///   opening automatic, so #154's class verb prices the later tranches
    ///   instead), gains two slots at tick 150 and three at 300, and
    ///   ends with eight (nine for the fabricator). It runs on its own map
    ///   generation `frontline-labs-03-legion` — the classes map's exact
    ///   tiles plus seven mirror-fair companion anchors per team and a pad
    ///   widened from six tiles to ten so every anchor stays SpawnProtected —
    ///   and registers three topology profiles (8-8, 9-8, 9-9). It supersedes
    ///   FIVE SLOTS' slot schedule and cannot combine with --side-objective.
    /// - `--pendulum hull` is the keel WITHOUT its forward rally, and
    ///   `--horizon long` raises the match limit 500 -> 750. Both are
    ///   registered levels; the horizon is a contract LIMIT, so read
    ///   limits.maxTicks. The whole next-round package (hull + the tuned class
    ///   game + channel + scrap + long) carries one identity per shape:
    ///   `vigil`, `warren`, `bastille`, and with the roster `crusade`,
    ///   `swarm`, `palisade`.
    /// NONE of this moves SDK or Guest bytes: it is all contract DATA — arm
    /// constants, a limits value, topology and lifecycle assignments, and one
    /// new map generation — inside schemas that already exist, so a frozen
    /// artifact from this window still negotiates. A frozen artifact that
    /// hard-coded 500 ticks, four deposits or a three-tier cap will simply
    /// play badly, which is the ordinary contract-driven consequence.
    /// The window stays UNPUBLISHED and continues through the MIND profile's
    /// SDK/Guest half (SDK 0.10.11). That half moves SDK and Guest bytes and
    /// therefore invalidates every build-cache entry by design, but it changes
    /// nothing a player can reach: no CLI command selects the mind profile and
    /// no scaffold emits one until the CLI phase, so this version keeps its
    /// number and grows a note rather than shipping a half-built surface.
    /// 0.9.28 IS that CLI phase: the mind becomes player-reachable.
    /// `nilbots new &lt;Name&gt; --profile generic-mind` scaffolds a mind
    /// project (one program driving the whole army, with Roles.cs as the file
    /// an author edits first, persistent memory shipped working in Recall.cs,
    /// and mind-shaped helpers whose movement reserves its destination tile so
    /// an army cannot walk into itself). `nilbots experiment frontline-labs
    /// --profile mind` runs the same immutable contract with ONE runtime per
    /// participant instead of one per body life — same rules, same map, same
    /// format, same topology, only the capability tuple moves — so a MIXED
    /// match is ordinary: a native mind against a per-life artifact runs
    /// because the artifact's guest wraps itself, with no source edit and no
    /// flag of its own. `qualify` gains the parallel mind suites
    /// `frontline-mind-qualification-3/4/5` on
    /// `frontline-mind-union-t{2,3,4}-v1`, running the same probe contracts
    /// plus two that only exist there: `body-handoff` at T2, where the body
    /// holding the objective is destroyed and another must take the point
    /// within 12 ticks without the army blocking itself, and
    /// `escort-integrity` at T4, the escorted channel held for six consecutive
    /// ticks from both assignments. The documented `respawn-reorient`
    /// requirement is gone from the mind suites because persistent memory made
    /// it vacuous, and the C1-C5 coordination axis folds into those tiers for
    /// a mind (`coordinationGradeAwarded` reads "folded-into-tiers") while
    /// staying exactly as it was for per-life artifacts.
    /// Two long-standing frictions are fixed in the same version: qualify's
    /// viewers become OPT-IN (`--viewer`), because a cumulative T4 run wrote 38
    /// self-contained viewers nobody asked for and turned a 21 MB evidence
    /// directory into 214 MB; and the mind's per-TICK diagnostic text is now
    /// recorded on the replay's mind turn, which is the only home an
    /// army-scoped sentence has — a body's command cannot carry it, and on a
    /// tick with no live bodies there is no command at all.
    /// SDK/Guest stay at 0.10.11: nothing about the compiled artifact moved,
    /// only what the CLI can ask of it.
    /// 0.9.29 is the PRE-FRICTION pass before the mind port wave — the
    /// accumulated authoring frictions of three waves, fixed together, and
    /// again with SDK/Guest unmoved at 0.10.11.
    /// `--print-candidate-contract full` prints a cell's COMPLETE resolved
    /// canonical contract as JSON (the bare flag still prints identity), which
    /// retires the campaign's most-repeated friction: every declared number was
    /// previously reachable only by running a throwaway match and mining
    /// `replay.json`.
    /// A match that ABORTS — an engine invariant refusing the history it was
    /// building — now reports one line on stderr with its own exit code (4),
    /// distinct from usage (1) and from a real fault/disqualification (2), and
    /// no command can report a failure as 0. An aborted cell writes no replay
    /// and says so, so a sweep can stop treating return codes as unreliable.
    /// A mind that traps while owning MORE THAN ONE body now records a clean
    /// participant disqualification instead of aborting the document: the
    /// participant's one fault is restated under each stopped body's identity,
    /// which is what per-life evidence requires (the null pin's first run hit
    /// this with a pre-mind artifact on the legion roster).
    /// `nilbots build` WARNS when botarena.json's declared sdkVersion
    /// disagrees with the toolchain's; it never fails, because a stale
    /// declaration cannot produce a stale artifact and frozen trees must stay
    /// rebuildable.
    /// The mind scaffold's movement no longer releases the tile a commanded
    /// body is vacating unless the contract's
    /// `collisions.followingVacatedActorAllowed` says a sibling may follow it
    /// — under this game it may not, so releasing it handed the army blocked
    /// steps.
    /// 0.9.30 carries the ONE-CHASSIS package (DECISIONS #194): `--chassis
    /// unified` dissolves the prime — one statline, one lifecycle and one
    /// action catalog per class, which is also what makes every fabricator
    /// body a fabrication origin and forces the upgrade ladder onto every body
    /// of the buying team — with the home base as the root factory at total
    /// body loss, a sweepable `--tier-cost`, and `--compositions` putting a
    /// participant's slots on chassis of more than one class. SDK/Guest stay
    /// at 0.10.11 in shape: the new contract facts are trailing
    /// inert-omitted fields and one appended spawn reason, so a bot compiled
    /// yesterday reads today's contract, but the staged assembly bytes moved
    /// and every artifact rebuilds.
    /// 0.9.31 adds the evaluation-only Arc Relay strategy-sheet v1 linker.
    /// 0.9.32 carries the steadier Arc Relay spectator director, three-theater overview,
    /// and causal Core-possession HUD cues in the packaged replay viewer.
    /// The separately hashed sheet data can express bounded coordinated
    /// spatial gambits without rebuilding the frozen stock algorithm; no
    /// hosted contract, SDK wire shape, or player-facing sheet format moves.
    /// Keep in lockstep with BotArena.Cli.csproj's
    /// Version — PackagedCliVersionTests pins them together.</summary>
    public const string CliVersion = "0.9.32";
    // 0.2.0: BotContext.Slot + documented event semantics (DECISIONS #46).
    // 0.3.0: BotContext.Energy for the energy-shot rules candidate (DECISIONS #47).
    // 0.4.0: strafe actions + map dims + zone-control fields for the rules 0.3 slate
    // (RULES-0.3-DESIGN). All carried as trailing observation sections / additive
    // action values, so the wire protocol stays 0.1 and older artifacts keep running.
    // 0.5.0: VisibleProjectiles (trailing P section) for the 0.5 watchability slate
    // (RULES-0.5-DESIGN) — same additive pattern, wire protocol still 0.1.
    // 0.6.0: hardened 0.5 (§H / DECISIONS #59): HeardSounds (trailing H section) and
    // computable bolt timing — the P section grew 4→6 fields, which 0.5.0 adapters
    // cannot parse; wire protocol still 0.1 for every pre-bolt adapter.
    // 0.7.0: active shared control pressure (trailing C section) and ordered
    // multi-tile projectile advances (P section 6→7 fields; DECISIONS #62).
    // 0.8.0: private programmed-shot actions (trailing SP action payload),
    // public limits (trailing SP observation), and exact currently revealed
    // eight-way projectile headings (trailing PH observation).
    // 0.8.1: no wire change — XML documentation now ships beside the SDK dll, and the
    // members that are inert outside the research arms (strafe actions, Energy) are
    // marked [Obsolete] + [EditorBrowsable(Never)] so they no longer read as playable
    // API. Compile-surface change for player projects, hence a version bump.
    // 0.9.0: independent entity-life API, typed Frontline manifest and
    // observations/actions, plus actor protocol 1.0's shared tagged codec.
    // Legacy IBot and line protocol 0.1 remain byte-for-byte compatible.
    // 0.10.0: exact-profile generic actor-match contracts, variable topology
    // and entity collections, typed dynamic action/event/score/mode unions,
    // and generic replay-3-ready lineage. Legacy and Frontline-alpha surfaces
    // remain separate compatibility generations.
    // 0.10.1: generic form-transition events accept a due tick equal to their
    // started tick for end-of-started-tick completion. Pending transition
    // state remains strictly future-due.
    // 0.10.2: transition-created LifeSpawned observations accept intentionally
    // redacted parent/operation lineage while retaining the public transition ID.
    // 0.10.3: optional Frontline capture-gain schedules are parsed from the
    // canonical contract and resolvable against the authoritative tick.
    // 0.10.4: dormant slots may declare a first automatic activation tick and
    // life starts identify that parentless origin separately from deployment,
    // return, fabrication, and replication.
    // 0.10.5: the Frontline mode observation carries the live territory-ratchet
    // hold as nullable holdOwnerTeamId/holdEndsAtTick, and ObservedProjectile
    // carries TicksPerAdvance/DamagePerHit. Both ride the wire as trailing
    // tagged fields, so the observation schema version stays 2 and an older
    // artifact still negotiates and still runs — it simply cannot see them.
    // 0.10.6: typed class identity reaches the contract topology and the
    // observation (self, allies, visible enemies, participants), and a visible
    // tile publishes the spawn reservation that makes it unavailable. Same
    // discipline: the canonical classId is emitted only when a ruleset declares
    // classes (#156), the observation fields are trailing tagged additions, and
    // the observation schema version stays 2.
    // 0.10.7: the canonical contract reader accepts the trailing additive
    // facts tickResolution.cooldownClock (#180) and
    // sameLifeTransitions[].cooldownTicks (#181).
    // 0.10.8: live route-cooldown clocks are readable — ObservedSelfState
    // and ObservedAllyState carry RouteCooldowns ({TransitionId,
    // ReadyAtTick}, ordered, empty when none are live). Trailing tagged
    // wire field on the shared self/ally body, so the observation schema
    // version stays 2 and a clockless observation keeps its exact bytes.
    // 0.10.9: team-scoped randomness. GenericActorMatchStart carries
    // TeamRandomSeed — one value shared by every life on a scoring team,
    // derived host-side from the match seed and the team id in its own
    // "teams:" domain, so neither team can derive the other's — and
    // GenericActorContext exposes it as TeamRandom, an IBotRandom whose
    // stream is RE-DERIVED from (team seed, tick) at the start of every tick
    // rather than advanced across ticks. That is what makes it common
    // knowledge: teammates agree on the Nth draw of a tick regardless of when
    // they were born or what they drew before, so a randomized plan is a
    // coordinated one. Trailing tagged wire field, every schema version
    // unchanged; an artifact built before this simply never sees TeamRandom.
    // The same version publishes the side objective the MUSTER arm contests:
    // ModeObservationState.Frontline carries SecondaryOwnerTeamId (the team
    // holding it, null while neutral) and SecondaryClaimProgress (signed
    // sole-presence ticks — positive for team 0, negative for team 1, zero
    // when no claim stands). Both are trailing tagged wire fields whose
    // absence is the neutral pair, so the observation schema version stays 2
    // and a ruleset that declares no side objective spends no bytes on one.
    // The site's regions, latch threshold, effect, and rally scope are
    // contract data on GenericActorRulesContract.FrontlineGameMode
    // .SecondaryControl, which is null unless the mode declares one.
    // 0.10.10: the canonical contract reader accepts the capture channel's
    // three trailing additive facts on gameMode.capture —
    // StationaryGainMultiplierCap, OpposingErosionMultiplier, and the
    // ClaimInterrupt block (kind, revertPerDamagePoint, scope,
    // granularity) — plus the appended ControlPolicy value that carries
    // them. All three ride together or not at all, exactly like the
    // ratchet's hold, so a ruleset that does not channel spends no bytes on
    // any of them and an explicitly inert encoding is refused. Nothing on
    // the OBSERVATION moves: the channel resolves control, stacking,
    // erosion, and the interrupt entirely through captureProgress and
    // claimingTeamId, which keep their exact published shape and meaning.
    // Read the policy and the three settings from the contract rather than
    // assuming a threshold or a multiplier — a channel cell's threshold is
    // 8, not 15, and its gain multiplier stops at 2.
    // The same version carries the SCRAP ECONOMY's surface: the contract
    // reader accepts gameMode.scrapEconomy (vein sites and schedule, the
    // wreck/assay/carry/pile numbers, the banking regions, the upgrade
    // scope, the tier cap, the purchase mode, and the declared track ladder
    // with its per-tier magnitudes and prices), the action envelope gains
    // ActionKind.ModeInvestment and ActionParameterKind.UpgradeTrack with
    // their argument and constraint, and the observation gains exactly three
    // facts — ScrapTeams and ScrapPiles on the Frontline mode state, and
    // CarriedScrap on self, ally, and enemy bodies. Every one of them is
    // trailing and inert by default, so a ruleset without an economy spends
    // no wire bytes and a frozen artifact reads zeros. The economy block and
    // the side objective are mutually exclusive and the reader refuses a
    // contract carrying both. Read the ladder's prices and caps from the
    // contract and the affordable tracks from the legality mask; never price
    // a tier in the bot.
    // 0.10.11: THE MIND. A second, parallel programming model lands beside
    // the per-life one and does not replace it: IGenericMindBot with
    // StartMatch/Think/EndMatch, MindContext (own Bodies with per-body
    // legality plus MovedLastTick/PreviousPosition/LifeStartedTick/Origin, the
    // complete own Slots table published EVERY tick, team perception and
    // mode/economy/scoreboard delivered ONCE rather than per body, plus
    // Random/TeamRandom/Debug), MindBody with the command surface
    // (Command/Hold/SetRole - commands are WRITTEN onto bodies, never
    // returned, and every own live body is pre-filled with Wait so forgetting
    // one costs a tick rather than the match), MindSlot, MindStart, MindEnd,
    // MindDecisions/MindCommand, and the public role tag.
    // It is a fresh profile - generic-mind-match-1, runtime contract /
    // MatchStart / observation / decision schemas all 1, runtime configuration
    // 2.0 - while the RESOLVED MATCH CONTRACT schema is CARRIED at 2, because
    // the rules, map, forms, actions, transitions, lifecycle, mode and economy
    // are identical: the mind plays the same game, only the driver changed.
    // generic-actor-match-2 is untouched and stays byte-exact.
    // Nothing here is player-reachable yet; no CLI command or scaffold selects
    // the profile. The version moves because the SDK's compile surface and the
    // Guest's bytes both moved - which invalidates the build cache by design
    // (DECISIONS #84) - and because an artifact built from this version
    // attests BOTH profiles: GuestHost.RunDetected gives any existing
    // IGenericActorBot a WrappedPerLifeMind facade, one sub-brain per live
    // body with per-life memory semantics reproduced exactly, so a per-life
    // bot becomes mind-profile-playable by rebuilding its sources with no
    // edits. The one documented divergence is the sub-brain's private random
    // seed, which is derived from the mind's participant-domain seed because
    // the per-life seed is mixed from a match seed the mind is never handed.
    public const string SdkVersion = "0.10.11";
    public const string IlcLlvmVersion = "10.0.0-rc.1.26306.1";
    // 0.10.11: the Guest gains the mind tick loop (GenericMindGuestSession),
    // the mind arm of the negotiation state machine, and WrappedPerLifeMind.
    // 0.10.12: the wrapped per-life mind gives each sub-brain its OWN
    // re-derived per-tick team stream — sharing the mind's one instance
    // advanced a single position across bodies and diverged 33 of 63
    // null-pin cells for any TeamRandom-consuming bot.
    public const string GuestAdapterVersion = "0.10.12";
    // Compiler invocation/container changes that affect artifact bytes without changing
    // the SDK or guest contract. Included in every player-bot cache key.
    // 2: reproducible builds (DECISIONS #81) — the workspace path is mapped to a fixed
    //    virtual root and debug info is dropped, so the same sources produce the same
    //    bytes no matter which directory (or host) compiled them. Every pre-existing
    //    cache entry is invalid because artifact bytes change.
    // 3: the staged assembly closure stopped depending on WHICH host compiled
    //    (DECISIONS #84). Three things changed together: the workspace stages exactly
    //    BotArena.Sdk/Guest instead of every dll beside the invoking host (the CLI
    //    staged 9, the server 74); Sdk/Guest compile identically in any configuration
    //    from any directory; and their fallback build cache is keyed by source content
    //    rather than by GuestAdapterVersion. Artifact bytes change.
    // 4: generated entry-point capability detection exposes every implemented
    //    bot interface without constructing a throwaway instance. This changes
    //    generated source and therefore every controlled artifact cache key.
    public const string BuildPipelineVersion = "4";

    public static string CacheRoot =>
        Environment.GetEnvironmentVariable("BOTARENA_HOME") is { Length: > 0 } home
            ? Path.Combine(home, "cache")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".botarena", "cache");

    /// <summary>env → system install (/opt, readable by the isolated build user) → legacy
    /// home dir. A home-dir sdk breaks isolated builds (botbuild cannot traverse /root),
    /// so it is the last resort, not the default.</summary>
    public static string ResolveWasiSdkPath()
    {
        if (Environment.GetEnvironmentVariable("WASI_SDK_PATH") is { Length: > 0 } env)
            return env;
        const string system = "/opt/botarena/wasi-sdk-29.0";
        if (File.Exists(Path.Combine(system, "bin", "clang")))
            return system;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".wasi-sdk", "wasi-sdk-29.0");
    }
}

/// <summary>A player bot project on disk: botarena.json + .cs sources.</summary>
public sealed class BotProject
{
    public required string Directory { get; init; }
    public required BotManifest Manifest { get; init; }
    public required IReadOnlyList<string> SourceFiles { get; init; }

    public string Accent => Manifest.Appearance?.Accent ?? "#22d3ee";
    public string LookId
    {
        get
        {
            string value = Manifest.Appearance?.Look ?? "vanguard";
            if (!IsPresentationId(value))
                throw new InvalidOperationException(
                    $"Invalid appearance.look '{value}' in botarena.json; use a lowercase kebab-case ID.");
            return value;
        }
    }
    public string ProjectileLookId
    {
        get
        {
            string value = Manifest.Appearance?.Projectile ?? "pulse-bolt";
            if (!IsPresentationId(value))
                throw new InvalidOperationException(
                    $"Invalid appearance.projectile '{value}' in botarena.json; use a lowercase kebab-case ID.");
            return value;
        }
    }

    public static bool LooksLikeProject(string directory) =>
        File.Exists(Path.Combine(directory, "botarena.json"));

    public static BotProject Load(string directory)
    {
        directory = Path.GetFullPath(directory);
        string manifestPath = Path.Combine(directory, "botarena.json");
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"No botarena.json in {directory} — not a bot project.");
        var manifest = JsonSerializer.Deserialize<BotManifest>(File.ReadAllText(manifestPath))
            ?? throw new InvalidOperationException("Empty botarena.json.");
        var sources = System.IO.Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
        if (sources.Length == 0)
            throw new InvalidOperationException($"No .cs sources found in {directory}.");
        return new BotProject { Directory = directory, Manifest = manifest, SourceFiles = sources };
    }

    /// <summary>Deterministic cache key over sources + manifest + every pinned toolchain
    /// version (plan §12). Any relevant change produces a different key.</summary>
    public string ComputeCacheKey() =>
        BotBuilder.ComputeCacheKey(
            SourceFiles.Select(f => new SourceFile(
                Path.GetRelativePath(Directory, f), File.ReadAllText(f))).ToArray(),
            Manifest.EntryType);

    private static bool IsPresentationId(string value) =>
        value.Length is > 0 and <= 64 &&
        value[0] is >= 'a' and <= 'z' &&
        value[^1] != '-' &&
        value.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
}
