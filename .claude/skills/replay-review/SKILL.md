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

**The review build cannot be opened from disk.** It is deliberately not self-contained —
separate hashed atlases, audio and JavaScript, so a phone streams them instead of parsing
one ~15 MiB inline document — and `file://` blocks ES modules. Double-clicking
`dist-review/index.html` gives a blank page. Always serve it.

`--tunnel` is **public and unauthenticated**; say so when handing over a link, and stop the
server when the review is done. Needs `brew install cloudflared`.

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

Do not fill an Arc Relay gallery with arbitrary smoke matches or the first
cells returned by a sweep. Prefer the latest tracked, canonical-verified,
cohort-eligible operation corpus whose rules, map, stock artifact, and viewer
match the work being reviewed. The former ten-play forward-combat baseline
named by the `replay-highlights` skill is currently quarantined: current
scoring rejects three of its matches. Do not treat operation success as
gallery eligibility. Regenerate or select a replacement set that passes the
current bars before the next broad owner review.

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

Let `scripts/build-review-gallery.py` enforce the current Arc Relay bars. Never
use its explicit eligibility skip except to show a clearly labelled diagnostic
failure.

For tactical galleries, follow the explanatory card contract in the
`replay-highlights` skill. A matchup label is not enough: the reviewer must
know what caused the play, what the operation intends, how the opponent can
answer it, when it gives up, and what causal evidence to watch for.

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
