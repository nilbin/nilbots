# Design reference

Three self-contained visual pages plus the implementation notes below. **Open the HTML
pages straight from disk** — everything they need is inlined, which is also why
`climb.html` is 680 KB.

| page | what it is |
|---|---|
| `climb.html` | The specification. Colour policy, type system, ground, logotype, viewer, full-screen chrome, bot page, first run, CLI. This is the one to read. |
| `logotype.html` | The mark on its own: the construction, the alphabet, four finishes, size tests. |
| `directions.html` | The nine directions Climb was chosen from. Kept as provenance — it predates the Forge ground and the type system, so its palettes and typography are **not** current. |
| `AUTO_ARENA.md` | Product and frontend contract for the proposed daily ranked-set scheduler, including Shop semantics, quota behavior, and how it will reuse the landed manual Arena authority. |
| `UX_FLOW_REVIEW.md` | End-to-end review of the main player jobs, route closure, Arena entry/continuation, and the remaining product/backend gaps. |

Published copies, for sharing without a checkout:
[Climb](https://claude.ai/code/artifact/8d87e1eb-0014-4339-a6df-a823b532da7c) ·
[Logotype](https://claude.ai/code/artifact/62508eef-fd28-4a63-9cd4-7f5d17ca0916)

## Rebuilding

Python 3, no dependencies:

```bash
python3 build_climb.py     # → climb.html
python3 build_logo.py      # → logotype.html
python3 build.py           # → directions.html
```

Each script resolves paths from its own location, so run them from anywhere.

## What generates what

- **`logotype.py`** — the wordmark. Letters are polylines through tile centres on the
  arena grid; rasterising gives cells, tracing gives outlines, and every corner is
  chamfered at the radius `web/src/render3d/wallSolids.ts` mills walls with. One
  construction renders as `line()` (the route), `solid()` (the wall), `void()` (the floor)
  and `ground()` (nil cut out, bots standing on it). Nothing is hand-drawn, so the mark can
  be regenerated at any size — including as SVG for `Logo.tsx` and the favicon.
- **`typeface.py`** — the shared type system. One variable family at three widths
  (condensed for labels, normal for reading, expanded for display) plus a mono reserved
  for values a machine wrote.
- **`charts.py`** — the viewer timeline and the generations chart. The timeline's marks are
  the **real** event list of `bastion-01` seed 4711 as read out of a running review build,
  so its lumpiness is the match's own rather than something evenly spaced to flatter the
  design.
- **`looks.json`** — the chassis sprites, inlined so the identity chips are the actual
  shipped art.
- **`shots/arena-hero.jpg`** — a real frame from the 2.5D renderer (bastion-01, tick 30).
  `shots/arena-alt.jpg` is `gallery-01`, which is nearly white — it is in the document to
  make the point that viewer chrome cannot assume a dark arena.

Templates hold the prose and markup; the `build_*.py` scripts substitute `{{tokens}}`.
`views.fragment.html` is spliced into `climb.template.html` at `<!--VIEWS-->`.

## Fonts

`fonts/archivo.woff2` and `fonts/mono.woff2` are the **latin subsets** served by Google
Fonts, committed so the build needs no network. Both are SIL Open Font License 1.1;
the licences are beside them and must travel with the files.

- Archivo — Omnibus-Type, `fonts/OFL-Archivo.txt`
- Geist Mono — Vercel, `fonts/OFL-GeistMono.txt`

`fetch_fonts.py` re-downloads them and documents where they came from. It needs network
and is not part of a normal build.

## Status

The pages are a specification, not a record of what is built. Their **Open** section lists
what is still undecided or missing — seasons do not exist, `▲14 places` needs a rank
snapshot nothing writes yet, and the renderer preference needs a name. Two decisions are
recorded there rather than implemented: 2.5D becoming the default, and replacing the mobile
WebView with a shared three.js renderer.

Implementation status lives in `IMPLEMENTATION.md`. To review the real routed web app
without a backend, run this from `web/`:

```bash
npm run site-review
npm run site-review:tunnel  # public and unauthenticated
```

This is separate from `npm run review`, which is the replay-only preview.
