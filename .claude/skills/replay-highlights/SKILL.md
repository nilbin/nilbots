---
name: replay-highlights
description: Build and publish selected Bot Arena viewer.html files as a phone-friendly replay gallery. Use when a user asks to share, host, publish, or open replay highlights on another device, especially when local file links show HTML source.
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
source into the ignored worktree before step 4. If the Sites integration is
unavailable, prepare and validate the gallery but report the publishing
blocker; do not silently switch to a public host.

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
