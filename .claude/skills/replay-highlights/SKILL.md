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

It injects each replay into the built viewer at the CLI's marker
(`ReplayOutput.WriteViewer` semantics), hides outcomes, and embeds the
rating panel (methodology dimensions, tick notes, localStorage autosave,
JSON export). Default `--viewer hosted` uses the full WebGL bundle and must
be served; `--viewer self-contained` produces portable Canvas2D pages.
Rebuild `web/` first when the viewer changed; rerunning the script is the
canonical way to refresh a gallery after viewer fixes. Note
`nilbots replay --out` cannot export replay-v3 viewers (v1 only) — the
script's marker injection is the v3 path.

A hosted gallery carries the whole of `web/dist`, and **the soundtrack is a
directory, not a bundled asset**: the score is fetched at runtime from
`soundtracks/index.json`, alongside `/assets`, both by absolute path. So serve
the gallery directory *as the server root*, and if the viewer's music control
reads `SCORE ERROR` while the sound effects still fire, the catalog 404'd —
the pages are fine, the tree beside them is not.

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
