---
name: replay-review
description: Serve the arena viewer to a real device and select replays worth judging for visuals, audio, spectator clarity, or Arc Relay tactics. Use when a user asks to review, preview, watch, look at, or listen to the arena, a replay gallery, fog, lighting, sound cues, tactical plays, or a candidate audio pack — especially on a phone.
---

# Replay review

Judgements about how the arena *looks or sounds* can only be made by a human on a real
screen and real headphones. This skill gets it in front of them, on a replay that actually
exercises the thing under review.

## Serving it

From `web/`:

```bash
npm run review                 # LAN — open the printed http://<ip>:4173 on a phone
npm run review -- --tunnel     # + a public HTTPS URL via cloudflared
npm run review -- --no-build   # serve what is already in dist-review
```

For iterative tactics or sheet review, build the production viewer once, then
reuse it with `--no-build` and refresh only the selected replay payloads and
explanatory cards. A one- or two-replay targeted gallery plus a focused browser
smoke is enough while discovering. Rebuild assets, run the full browser matrix,
and publish the permanent gallery only for a frozen promotion candidate or a
change to viewer code/assets. Do not turn every replay refresh into a release
gauntlet.

For Arc signature presentation, the ordinary owner-review loop is even smaller:

```bash
dotnet run --project src/BotArena.Cli -- \
  experiment arc-relay-signature-stage --signature <id> --out <dir>
```

This runs the real current Arc rules on Tapline geometry, but under a short,
explicitly diagnostic stage contract: one caster, the minimum ally/enemy/Core
context the named signature actually needs, and everyone else waiting. Install
one or a few of these compact broadcasts in the existing review build and hand
them to the owner early. It makes no audit, balance, or doctrine claim.

The convenience path builds and installs a picker in one command:

```bash
node scripts/build-arc-relay-signature-stage-review.mjs \
  vector-dash target-paint smoke-canister
```

These stage broadcasts are already compact format-2 runtime payloads and may be
installed directly; the canonical-to-transport projection rule below is for
large canonical diagnostic/audit replays, not this focused stage command.

For Core pickup, carried-height, handoff, drop, and Arc Toss animation review,
use the separate eight-body custody stage:

```bash
dotnet run --project src/BotArena.Cli -- \
  experiment arc-relay-core-stage --out <dir>
node scripts/build-arc-relay-core-stage-review.mjs
```

It fields exactly eight distinct chassis around one centre-Well Core. The real
rules drive carried movement through every body, six adjacent handoffs, two
voluntary drops with cross-team reacquisition, and one long Arc Toss. Its
receipt refuses to write a review replay unless all eight bodies handled the
same Core and every custody beat completed. Like a signature stage, it is a
presentation diagnostic and makes no balance, pathing, or doctrine claim.

To place that already-built viewer under `/core-stage/` in the existing private
sound-lab Sites package, rebuild the package explicitly with:

```bash
node scripts/build-audio-sound-lab-site.mjs --include-core-stage
```

The flag is deliberate: the ordinary audio-only site build stays independent
of generated replay output, while a Core handoff copies `web/dist-review` as a
relative-path-safe subtree. This still does not authorize either capture
harness.

The two retained all-signature capture programs are **owner-request-only**:
`scripts/capture-arc-relay-presentation-proof.mjs` and
`scripts/capture-arc-relay-signature-motion-proof.mjs`. Never invoke either
because renderer code, models, assets, or a normal visual iteration changed.
They are a frozen-candidate promotion harness and require both an explicit owner
request in the current conversation and `--owner-requested-full-proof true`.
Absent that request, do not capture a frame matrix or clips. A focused live-page
boot smoke may verify that a handoff URL loads; it does not pretend to judge the
art for the owner.

For repeated screenshots of one build and replay, group frames by render
profile. Load one page per profile, wait for all models once, pause, then seek
and capture every required tick without reloading the page. Keep baseline and
candidate inputs pinned to the same replay, viewport, camera, renderer, and
ticks. Mount desktop and mobile separately because the render profile is fixed
at viewer startup; reuse one browser context when a query override can select
the profile so both pages share the HTTP cache. On a retry, capture only missing
or failed frame IDs and retain already successful pinned frames. Never start a
fresh browser context merely to capture another tick.

A retained screenshot set is not yet a review gallery. When frames are part of
the requested evidence, also provide a navigable, phone-friendly index that
groups them by the comparison the reviewer is meant to make (for example
start/action/resolve or desktop/phone), labels the pinned replay and tick, and
links to the live replay when one exists. Deliver the exact public gallery URL;
a capture directory, JSON index, README, or contact sheet by itself is not a
review handoff. Prefer an index that reads the retained capture manifest so a
partial retry cannot leave the gallery stale.

An existence assertion is not visual evidence. A captured treatment must be
large and isolated enough for the reviewer to identify and judge without being
told where it is. Keep an ordinary gameplay-scale overview beside any close
review frame, and label the close frame honestly; never present a whole-arena
shot with one detected mesh as proof that an effect reads. If the effect still
cannot be identified in the pinned close frame, treat that as a presentation
failure rather than a capture success.

The same boundary applies to gameplay artifacts: ordinary review runs mind
sources in-process. Do not rebuild tracked WASM, mint a stock artifact or
playlist, reset ratings, or bump release versions until the owner explicitly
opens the deployment checkpoint described by the `sheet-tuning` skill.

For Arc Relay, a canonical replay is an audit input, never a runtime gallery
payload. Project it first with `scripts/arc-relay-broadcast.py` (300 KiB gzip
ceiling), then install it as `transport` alongside the hash-matched replay so
the installer can retain team vision. Never omit `transport`: a canonical
diagnostic replay can exceed 100 MB after decompression and strand a phone on
“Loading replay.” Keep the generated `.json.gz` sibling beside every runtime
JSON.

Before handing over any public URL, load that exact URL in a phone-sized real
browser, wait for the replay UI (not merely HTTP 200), and fail on console or
page errors. Confirm the chosen replay list and one replay payload both arrive
gzip-encoded. A `curl` response alone does not prove that the viewer booted.

**The review build cannot be opened from disk.** It is deliberately not self-contained —
separate hashed atlases, audio and JavaScript, so a phone streams them instead of parsing
one ~15 MiB inline document — and `file://` blocks ES modules. Double-clicking
`dist-review/index.html` gives a blank page. Always serve it.

`--tunnel` is **public and unauthenticated**; say so when handing over a link, and stop the
server when the review is done. Needs `brew install cloudflared`.

When an agent starts a server or tunnel that must survive its response, follow
the detached `screen` procedure in the `sheet-tuning` skill. A foreground tool
session, `&`, and `nohup` are not durable in this environment: their children
are reaped after the turn. Verify the named sessions and public URL from a
separate command before delivery, retain their log paths, and stop only those
exact sessions after review.

## Choosing which games to review

This is the part that decides whether a review is worth anything, and the intuitive
choices are wrong.

**Cue variety dominates.** A replay with no impacts never plays the impact sound and never
casts an impact light, so however long it runs it exercises a third of the work. One real
candidate ran 121 ticks with 37 shots and *zero* hits — busy to watch, and useless for
judging two of the three cues. Require shots **and** damage **and** a destruction.

**Both bots must fight.** A match against a bot that never fires runs full length and
fires every cue, but it is target practice. Without this check the picker chose
`hunter v idle`, where the opponent never moves. Require at least two distinct shooters.

**Stronger bots make worse replays.** This is the counter-intuitive one. Pincer gen-10
against Bastille gen-5 ends in 10–39 ticks across five maps, because good bots kill
quickly. A mid-table pairing like `Rampart gen-2 v hunter` runs 97 ticks with every cue
firing. *Better bots, worse replay.* Prefer 60–140 ticks: long enough to watch fog resolve
as a bot moves through cover, short enough to rewatch.

**Offer several, not one.** Review is comparative — a fog treatment or light intensity that
reads well on one fight can be wrong on the next, and reloading loses the comparison. The
build carries a set and shows a picker, one per matchup and map.

Overrides, when the reviewer has asked for something specific:

```bash
REVIEW_BOTS="Pincer,Bastille" REVIEW_COUNT=5 npm run review -- --tunnel
```

`REVIEW_BOTS` matches substrings of bot names and **wins outright over the score** — the
score stands in for judgement, it does not replace a person asking for a matchup.

If nothing suitable exists, generate some: `dotnet run --project src/BotArena.Cli -- spar
"<bot>" "<bot>" --map <map>`. Replays are picked from the local API, so the server must be
running.

## Arc Relay owner reviews

For a sheet, executor, pathing, or tactical iteration, the `sheet-tuning`
skill is the controlling workflow: trace first, run its standard six-cell
exhibit, and deliver the outcome-visible phone gallery. Do not substitute a
battery or the broad corpus below. The exhibit wrapper's explicit eligibility
skip is intentional because an exhibit makes no eligibility or balance claim.

Use the rest of this section for a broad renderer, awareness, presentation, or
promotion review—not the ordinary fast iteration loop.

Do not fill an Arc Relay gallery with arbitrary smoke matches or the first
cells returned by a sweep. Prefer the latest tracked, canonical-verified,
cohort-eligible operation corpus whose rules, map, stock artifact, and viewer
match the work being reviewed. The ten-play stock-recovery baseline named by
the `replay-highlights` skill is the current pin: its retained campaign passes
the v4 bars for both teams in every match, and a second seed campaign repeats
all ten successes. Do not treat operation success alone as gallery
eligibility. Re-run the pinned retention check or select a replacement set
that passes the current bars before the next broad owner review.

A useful Arc Relay set must:

- contain distinct coordinated plays rather than ten variants of ordinary
  Core return;
- show causal trigger, preparation, commitment, physical success or failure,
  bounded release, and return to baseline;
- include real opposition and counterplay opportunities, not target practice;
- cover several theaters, class signatures, possession beats, fights, and
  match phases;
- exclude runtime faults and felt-degeneracy failures; and
- be outcome-visible for an owner review unless the user explicitly requests
  a blind methodology read.

Prefer at least six replays and cover all ten operations for a broad renderer,
awareness, or tactics review. A shorter targeted gallery may use fewer only
when every selected replay exercises the feature under review.

For a broad or promotion gallery, let `scripts/build-review-gallery.py` enforce
the current Arc Relay bars. Outside the `arc-relay-exhibit.sh` fast loop, never
use its explicit eligibility skip except to show a clearly labelled diagnostic
failure.

For tactical galleries, follow the explanatory card contract in the
`replay-highlights` skill. A matchup label is not enough: the reviewer must
know the actual opponent's allocation and doctrine, whether it is bespoke
counterplay or general live opposition, what caused the play, what the
operation intends, how the opponent can answer it, when it gives up, and what
causal evidence to watch for.

## Diagnosing "there is no sound"

`/audio-check.html` in the review build isolates the layers in three taps: a Web Audio
tone, an `<audio>` element, and `decodeAudioData` on a real cue. Whichever fails first is
the answer.

The most common cause on iPhone is **the ring/silent switch**, which mutes Web Audio while
leaving `<audio>` elements alone. The viewer declares `navigator.audioSession.type =
'playback'` to opt out of that, but a reviewer on an older Safari will still hit it.

## What this cannot tell you

Structure, types, byte counts and golden-frame hashes are all verifiable here. Whether the
fog reads well, whether the light intensities are right, whether a candidate pack suits the
game — those need eyes and ears. Do not report a visual change as verified because the
suite is green: golden frames hash the canvas and would not catch a CSS layout regression
at all.
