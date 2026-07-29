# DX report — iron-root

Written before seeing any aggregate population result, standing, or other
entrant's revision.

## Freeze identity

| Field | Value |
| --- | --- |
| Entrant | `iron-root` |
| Population / wave | Frontline Labs classes, wave 1 (`classes-wave-1-2026-07-29`) |
| Authoring lineage | `iron-root-v1` |
| Class | `bulwark` (declared in `botarena.json`) |
| Doctrine | FORTRESS ROTATOR |
| Role | `verdict-doctrine` |
| Target tier | cumulative T4 |
| Budget | one authoring pass; mechanical contract repairs free; no open-ended strategic iteration |
| Author packet | `docs/FRONTLINE-BALANCE-POPULATION-AUTHOR-PACKET.md`, sha256 `79ad08b6c4cc7c9494c9cd87bafbe5f2b9ca25ec97a1d380cd1f7cc46501df6a` |
| Rules card | `docs/FRONTLINE-LABS-RULES.md`, sha256 `42e12c66f3adc8628dfb505f9f403d8fd2ec3a150da140ebfd9e644bb6789a9a` |
| Class addendum | `docs/EXPERIMENTAL-FRONTLINE-CLASSES.md`, sha256 `676cb185b37ea82758b19ba110d4e1366cb0037d465e8777b2959c188dde77a4` |
| Source-tree hash | `0b1cf8673df95cf328a39f90487f383ab6bf653ba5db8ed750e79dde6271e728` (sha256 over the ordinal-sorted per-file sha256 list of `*.cs`, `botarena.json`, `IronRoot.csproj`) |
| Toolchain | NativeAOT-LLVM `10.0.0-rc.1.26306.1`, SDK/Guest `0.10.4`, actor protocol 1.0, WASI p1 core module, platform-matched Docker builder (macOS arm64 host) |
| Build cache key | `73c71bd267bb25439e8c9c55bd1e615c88e845301aa749cb70e4c3c92372c0d3` (penultimate revision) → final artifact recompiled `--no-cache` |
| **`out/bot.wasm` sha256** | **`ed5c7bccaa98947b9e413d506eeb527c6ffe9e17af2de20cfb3ea10611d18928`** |
| `evidence/t4/qualification.json` sha256 | `02c174c457d6f1ae3be37a48ef2ad65c747f1026cc0165408417985c39496bf3` |
| Cumulative T3 prerequisite report sha256 | `f6a4fea4d29ff60aecbec6b542ef13c1bcea0dd009679b4640f3f927849c7ca9` (hash-linked inside the T4 report) |
| Qualification contract fingerprint | `2e3d4bcd4652814c5f58ce19c606f52031985b998d2b269d1c00d418edeb6ddb` |
| Qualification outcome | suite `frontline-qualification-5`, profile `frontline-duel-depth-union-t4-v1`, **exit 0**, **T4 awarded**, `balanceEvidenceEligible: true` |

All five T4 components passed (`suppression-choke`, `entry-initiative`,
`prediction-chamber`, `front-rotation`, `map-holdout`), with the cumulative T3
and T2 prerequisites rerun and hash-linked automatically.

## Timings (Apple Silicon, warm Docker builder)

| Step | Time |
| --- | --- |
| `dotnet build` of the editing project | ~0.4 s |
| In-process 500-tick match | ~2 s including the in-process bot build |
| `botarena build --no-cache` (cold cache, Docker) | **7.3 s** |
| `qualify --suite frontline-qualification-5` (36 probe matches, WASM, both assignments) | **10.4 s wall / 85 s CPU** |
| WASM 500-tick class-arm match | 3.6 s |

The inner loop is genuinely fast. The single best DX property of this platform
is that the expensive artifact — the canonical WASM — costs seven seconds, so
there is never a reason to qualify a stale build.

## Repairs and strategy passes

One authoring pass, then three mechanical repair cycles driven by probe/self-play
evidence. No open-ended strategic iteration.

1. **Authoring pass.** Doctrine written contract-first: forms classified as
   static/mobile by their own action masks, anchor and mobilize routes read from
   the same-life transition catalog, fortress tiles scored by how many active
   objective tiles their eight lanes actually reach.
2. **Repair — the fortress never rooted.** Self-play showed the designated body
   standing on a covering tile for 390 consecutive ticks. Two causes: the site
   list included every tile with non-zero coverage, so the body would settle on
   a half-covering tile and then refuse to anchor there; and the punish gate was
   a raw `enemyReach + windup` radius, which on a 23-wide map is satisfied
   permanently. Replaced with a top-coverage-tier site list and a *priced*
   windup — can each visible muzzle actually occupy a tile with a firing lane
   onto us in time, at its own cadence, and does the expected damage leave us
   alive.
3. **Repair — allied bodies deadlocked.** 480 blocked moves in 500 ticks: every
   body pathed at the same objective tiles and blocked each other, and one child
   spent 239 ticks walking into a permanently reserved return spawn that the
   legality mask never marks as unusable. Fixed with distinct stations derived
   from the same frozen observation (fortress / holder / ranked overwatch) and a
   refusal counter that stops routing through a tile that has blocked three
   times. Blocked moves fell to 32.
4. **Repair — T2 `straight-evade` failed.** The bot walked two tiles deeper into
   a walled corridor toward an inbound bolt because the bolt was not yet inside
   a fixed tile radius, then had no perpendicular exit left. Replaced the radius
   with ticks-to-impact and added a trap rule: leave early when the current tile
   has no exit at all. The first version of that fix over-corrected and broke
   `entry-initiative` (the bot stopped entering the objective under fire), so
   safety was made to saturate at three ticks — beyond that the errand decides
   between two survivable tiles, which is the difference between evading and
   conceding.

## Documentation gaps

- **`--classes` is inferred from the manifest and is mutually exclusive with
  every other experiment flag.** Once `"class": "bulwark"` is in `botarena.json`,
  the project can never be run against the base contract locally: adding
  `--auto-companions` fails with `Use one Frontline Labs experiment option at a
  time`, and plain `experiment frontline-labs` silently resolves the class arm.
  But the qualification suites take a bare `.wasm`, which has no manifest, so
  they run the *base* contract. A class-declaring entrant therefore cannot
  locally reproduce the contract it is actually qualified against without
  building a second, class-free copy of its own project. I had to do exactly
  that to test the fabricate-and-child-anchors path at all. This is the single
  biggest gap between the docs and the workflow the packet asks for.
- **Nothing states that a fortress cannot capture.** `objectiveWeight: 0` is in
  the contract and the rules card says a turret "cannot capture or contest", but
  neither the class addendum nor the doctrine brief connects that to the obvious
  consequence: a bulwark that anchors before it has a companion has voluntarily
  removed its only scoring body. That is the whole balance question for this
  class and it is left to be rediscovered.
- **Turret coverage is eight rays, not an area.** Easy to read
  "omnidirectional, range 8" as a radius. It is not: on the current map the best
  tile beside the centre objective covers four of its six tiles, and several
  plausible-looking tiles cover two. A worked example in the class addendum
  would save every bulwark author the same hour.
- **`ObservedProjectile` reports `TilesPerAdvance` and `TicksUntilAdvance` but
  not `TicksPerAdvance`.** Time-to-impact — the single most useful derived
  quantity for evasion — cannot be computed exactly from the observation alone;
  I had to take the minimum cadence across all declared attack profiles as a
  conservative stand-in. Either add the field or document the intended idiom.
- **Permanently reserved spawn tiles are invisible to legality.** The contract
  says automatic-return placement is "permanently reserved for slot against
  other actors and lifecycle claims", but the tile carries no map tag and
  `move` reports it as an available direction. A bot can only learn it by being
  blocked forever. A tile tag would make this contract-discoverable.

## Hardcoding temptations

Real ones, all resisted, and each one would have passed the class arm while
failing qualification:

- **Form names.** `"bulwark-prime-turret"` is right there in the contract, and
  the base contract calls the same concept `turret`. Deriving "static" from the
  absence of a movement action in the form's own mask costs eight lines and is
  the only reason the base contract works.
- **Windup length.** Three for the prime, one for the child, one for mobilize —
  and one on the base contract's only anchor route. Reading
  `Windup.DurationTicks` matters because the punish budget is computed from it.
- **The map.** The rules card prints `frontline-labs-01` with exact objective
  coordinates, which is very tempting and would have failed `map-holdout`
  outright — the qualification probes each run their own map with different
  fingerprints, one per probe.
- **Team 0 advances toward higher indices.** Every probe runs both assignments.
  `FrontlineTeamAdvance` exists precisely for this and it is easy to skip.
- **Unlock ticks 120/260.** The addendum says not to; the base contract makes
  them `Ready` slots needing explicit fabrication rather than automatic lives,
  so the number would not even have meant the same thing.
- **Action codes.** `shoot` is 4, `shoot-straight` is 105, `mobilize` is 104.
  Pairing the stable ID with the mask's code is barely more work and is the
  difference between one bot and three.

## Confusing terminology

- **"Anchor" and "Mobilize" are documentation words, not contract words.** The
  contract has `transform` with a form target and `mobilize` with no arguments,
  both of catalog kind `same-life-transition`, distinguishable only by which of
  their two forms can move. Prose and contract never meet.
- **`irreversibleForLife` reads backwards on the reverse route.** It is `false`
  on the anchor transition and `true` on `mobilize`, which encodes "you may
  un-root once, and then never root again". Correct, but it took reading both
  routes together to see that the *return* is the thing that is limited.
- **"Available" versus "will succeed".** Well documented, and still the source
  of two of my three repairs — a reserved spawn tile and a same-destination
  collision both produce `Blocked` from an `Available` action.
- **`Distance` on `ObservedSound` is a band index, not tiles**; `Bearing` is a
  sector index, not degrees. Both are `int`. The XML docs say so; the types do
  not, and a wrong guess is silent.
- **"Prime" has no contract representation.** I derived it from
  `InitialAvailability == ActiveAtTickZero`, which is what the doctrine brief
  means by it, but the word never appears in the schema.

## What I could not evaluate

Under a one-pass budget, the mirror is the only opponent available, and a mirror
of a fortress doctrine mostly measures who reaches the covering tile first. The
class arm mirror ends 0–0 or ±8 territorial; the base-contract mirror ends ±6.
Whether the rooted phase is worth its zero objective weight against a striker's
longer gun or a fabricator's earlier bodies is exactly the question this entrant
exists to answer, and I have deliberately not tried to guess it.
