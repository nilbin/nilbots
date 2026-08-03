---
name: replay-highlights
description: Build and publish selected nilbots viewer.html files as a phone-friendly replay gallery. Use when a user asks to share, host, publish, or open replay highlights on another device, especially when local file links show HTML source.
---

# Replay Highlights

Publish through the existing site in `.openai/hosting.json`. Reuse its stable
production URL and project ID; never create a second site for another batch.

## Fast path

1. Select existing `viewer.html` files. Do not rerun matches or rebuild `web/`
   when self-contained viewers already exist.
2. Copy the selected viewers into an ignored scratch directory with short,
   descriptive `.html` filenames.
3. Optionally add `gallery.json` there for exact ordering and card copy. Follow
   the schema in `scripts/build-replay-gallery.py`.
4. Build the deployment tree:

   ```bash
   python3 scripts/build-replay-gallery.py <viewer-dir> out/replay-highlights-site
   ```

5. Verify every replay asset contains `window.__BOTARENA_REPLAY__`, the index
   links resolve to files, and `.openai/hosting.json` contains the existing
   project ID.
6. Commit the generated site source in its own ignored Git worktree and push
   that exact commit to the site's configured source repository using a
   short-lived, per-command credential. Never persist or print its token.
7. Create the archive from that commit with `git archive`, save a site version
   using the same commit SHA, and deploy only that saved version.
8. For verified owner-only access, use the private deployment path. Do not make
   the gallery public unless the user asks. Poll a non-terminal deployment and
   return the production URL only after success.

On a fresh machine, obtain a source-repository credential and clone the site
source into the ignored worktree before step 4.

## Outcome-blind review galleries

For a methodology blind-review sample (the manifest from
`scripts/replay-review-sample.py`), do not hand-assemble pages — use:

```bash
python3 scripts/build-review-gallery.py \
  --sample <blind-review-sample.json> --output <dir> \
  --title "<experiment> — blind review" --review-panel
```

It builds each replay's page from the built viewer, hides outcomes, and
embeds the rating panel (methodology dimensions, tick notes, localStorage
autosave, JSON export). Default `--viewer hosted` uses the full WebGL bundle
and must be served; `--viewer self-contained` inlines the replay at the CLI's
marker (`ReplayOutput.WriteViewer` semantics) for portable Canvas2D pages.
Rebuild `web/` first when the viewer changed; rerunning the script is the
canonical way to refresh a gallery after viewer fixes. Note
`nilbots replay --out` cannot export replay-v3 viewers (v1 only) — the
script's injection is the v3 path.

**A hosted page does not carry its replay.** It is a few kB that fetches
`replays/<sample>.json`, assigns `window.__BOTARENA_REPLAY__` and only then
appends the bundle's module script, because `main.tsx` reads that global at
module-evaluation time. Inlining a 4-19 MB replay into every page — which is
what this used to do — re-shipped the whole bundle per match to a phone on
mobile data. Serve it with the companion script, which sends the `.gz`
siblings the builder writes:

```bash
(cd <gallery-dir> && python3 scripts/serve-gallery.py 8931 --directory .)
cloudflared tunnel --url http://127.0.0.1:8931
```

Plain `python3 -m http.server` still works but sends everything
uncompressed, and replay JSON compresses ~30x.

A hosted gallery carries the whole of `web/dist`, and **the soundtrack is a
directory, not a bundled asset**: the score is fetched at runtime from
`soundtracks/index.json`, alongside `/assets`, both by absolute path. So serve
the gallery directory *as the server root*, and if the viewer's music control
reads `SCORE ERROR` while the sound effects still fire, the catalog 404'd —
the pages are fine, the tree beside them is not.

A gallery that is **not** blind (an unblinded highlight reel) keeps the same
builder: `--index-cards <json>` supplies curated card order, titles,
subtitles, optional explanatory fields, and a `win` flag; `--intro` replaces
the blind-protocol lede, and `--review-protocol` names the record. The index
still emits its own progress markers and export button, so curated copy never
costs the rating flow. The sample ids come from the builder's hash shuffle,
not the manifest order — build once, read the mapping out of the output, then
write the cards.

## Arc Relay owner-gallery baseline

For broad Arc Relay renderer, awareness, or tactics review, default to the
latest compatible retained operation corpus instead of incidental smoke or
balance matches. Run the current felt-degeneracy scorecard first; the gallery
builder does this automatically for complete Arc Relay broadcast-v1 inputs.

The former ten-match pin below is **quarantined**, not a reusable baseline:

```text
arena-bots/arc-relay/forward-combat-operation-proof-v1-2026-08-03/
  evidence/gallery-sample.json
  evidence/gallery-cards.json
  evidence/replays/*.broadcast.json.gz
```

It contains ten canonical-verified, operation-rich matches across Rear Hook,
Lantern Sweep, Fork Shadow, Birth Rotation, Escort Counterpunch, Smoke Breach,
Hardlight Gate, Relay Catch, Decoy Switch, and Emergency Exchange. Current
scoring rejects three matches: Emergency Exchange trips the v4 pickup/drop
cycle, Rear Hook's opponent trips home-carrier non-progress, and Relay Catch's
opponent trips stuck-carrier. Do not reuse or rebuild this set as an eligible
gallery. Regenerate an equivalent or better operation-rich set under the
current bars; do not silently fall back to bland matches merely because they
are newer.

Use `--skip-arc-relay-eligibility` only when the gallery is explicitly about a
known failure. The default builder refusal is part of the methodology.

Owner galleries are outcome-visible by default. Use outcome blindness only
when the requested evaluation methodology requires it.

### Explanatory card contract

Every tactical or Arc Relay curated card must contain:

```json
{
  "id": "sample-01",
  "title": "Operation — side",
  "subtitle": "opponent, outcome, and verified phase ticks",
  "trigger": "The causal, observable condition that arms the play.",
  "tactic": "The intended coordinated sequence and participant roles.",
  "counterplay": "Concrete legal answers available to the opponent.",
  "fallback": "Abort, loss, deadline, and baseline-resumption behavior.",
  "watch": "The actual causal beats and ticks this replay proves."
}
```

Write `counterplay` from the real rules and visible tactical opportunities.
Do not imply that the opponent took an answer unless the replay contains it.
Write `watch` from authoritative replay evidence, not the intended sheet. A
successful operation in a lost match must say both things plainly. Keep these
fields optional only for a gallery whose sole purpose is a narrow visual or
audio asset comparison with no tactical claim.

## Without the Sites integration

The hosted deploy above needs the Sites integration, which only some agent
environments carry (Codex sessions normally do; Claude Code sessions normally
do not). When it is absent, do not silently switch to another public host —
serve the already-built tree directly instead:

```bash
(cd out/replay-highlights-site/.open-next/assets && python3 -m http.server 8917)
cloudflared tunnel --url http://127.0.0.1:8917   # optional public HTTPS URL
```

The LAN URL (`http://<machine-ip>:8917/index.html`) covers same-network
review; the cloudflared quick tunnel adds an off-network HTTPS link. A quick
tunnel is **public and unauthenticated** — say so when handing over the link,
and stop both processes when the review is done. Keep the assembled gallery
(viewers plus `gallery.json`) in an ignored scratch directory that no build
clears — `web/dist-review` and other build outputs are wiped on rebuild and
will eat manually placed files.

## Latency rules

- Tell the user the existing stable gallery URL early when it is already live.
- Batch all requested highlights into one version and one deployment.
- Use `scripts/build-replay-gallery.py`; do not hand-author another Worker or
  framework wrapper.
- Do not install a web framework. The generated OpenNext-compatible static
  Worker and inline replay assets need no build step.
- A deployment-provider wait is the only expected slow portion after initial
  setup. Keep polling without repeating builds, saves, or deploy calls.

## Missing viewer

If only `replay.json` exists, use the CLI's replay export against the already
built viewer:

```bash
scripts/botarena replay <replay.json> --out <scratch-dir>
```

This should be the exception; tournament and `play` outputs normally already
contain a self-contained viewer.
