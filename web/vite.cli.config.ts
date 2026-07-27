import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import { viteSingleFile } from 'vite-plugin-singlefile';

/**
 * The CLI's replay artifact: one self-contained `index.html` per map theme.
 *
 * `nilbots play` writes a `viewer.html` the player can copy anywhere, mail to someone, or
 * open from disk — so it has to inline everything, and a `file:` URL cannot fetch sibling
 * modules anyway. That constraint belongs to this build alone; everything served over HTTP
 * uses `vite.config.ts` and streams hashed assets.
 *
 * **Scoped to one theme, because a replay draws exactly one.** Themes are essentially the
 * whole artifact — 14 MB of the 15 MB, against 236 KB for every bot chassis, projectile
 * look and audio cue combined — and an unscoped build inlined all four into every viewer.
 * Worse, it grew linearly: the tenth theme would have put a single HTML file past 30 MB.
 * The CLI picks the artifact matching the replay's `ThemeId`, so what a player opens
 * carries the one theme it actually renders and nothing else.
 */
const THEME = process.env.BOTARENA_CLI_THEME;

export default defineConfig({
  // `public/` contains HTTP-only assets such as content-addressed soundtrack packs.
  // A CLI viewer is a self-contained file:// artifact and cannot fetch them; copying the
  // directory here would duplicate every pack into every per-theme build for no benefit.
  publicDir: false,
  plugins: [
    scopeToTheme(THEME),
    stubDimensionalRenderer(),
    stubExternalSoundtrack(),
    react(),
    tailwindcss(),
    viteSingleFile(),
  ],
  define: {
    // A scoped artifact has no `control-room` to fall back to, so the fallback becomes the
    // theme it does have. Unscoped builds keep the ordinary default.
    __BOTARENA_DEFAULT_THEME__: JSON.stringify(THEME ?? 'control-room'),
    __BOTARENA_DIMENSIONAL_RENDERER__: 'false',
    __BOTARENA_EXTERNAL_SOUNDTRACK__: 'false',
  },
  build: {
    outDir: THEME ? `dist-cli/${THEME}` : 'dist-cli',
  },
});

/**
 * Replace the 2.5D renderer with a stub.
 *
 * `viteSingleFile` inlines every chunk, so a dynamic import is not a saving here the way it
 * is on the web — three.js would land inside the artifact whether or not anyone switches
 * renderer, and `nilbots play` would carry a WebGL engine it can never reach. The Canvas2D
 * viewer is the only one the CLI offers, and this makes that true of the bytes as well as
 * the UI.
 *
 * A stub rather than an error, so the toggle simply renders nothing if it is ever reached.
 */
function stubDimensionalRenderer(): Plugin {
  return {
    name: 'nilbots-stub-3d-renderer',
    enforce: 'pre',
    resolveId(source) {
      return source.includes('render3d/ArenaCanvas3D') ? '\0nilbots-no-3d' : null;
    },
    load(id) {
      return id === '\0nilbots-no-3d' ? 'export default function NoRenderer() { return null; }' : null;
    },
  };
}

/** Keep the network-only soundtrack runtime out of copied file:// viewers. */
function stubExternalSoundtrack(): Plugin {
  return {
    name: 'nilbots-stub-external-soundtrack',
    enforce: 'pre',
    resolveId(source) {
      return source.includes('soundtrack/AdaptiveSoundtrack')
        ? '\0nilbots-no-soundtrack'
        : null;
    },
    load(id) {
      return id === '\0nilbots-no-soundtrack'
        ? 'export default function NoSoundtrack() { return null; }'
        : null;
    },
  };
}

/**
 * Narrow the theme globs to one directory, at build time.
 *
 * It has to happen here rather than at runtime: `import.meta.glob` takes a literal pattern
 * and Rollup follows every match, so filtering the resulting map in `arenaThemes.ts` would
 * shrink nothing — every atlas would still be inlined, just never read.
 *
 * The rewrite is asserted rather than assumed. If a pattern is renamed and this silently
 * matches nothing, the build would keep succeeding and quietly ship all four themes again,
 * which is exactly the failure this exists to prevent — so it throws instead.
 */
function scopeToTheme(theme: string | undefined): Plugin {
  return {
    name: 'nilbots-scope-cli-theme',
    enforce: 'pre',
    transform(code, id) {
      if (!theme || !id.endsWith('render/arenaThemes.ts')) return null;
      const pattern = "'../assets/themes/*/";
      if (!code.includes(pattern))
        throw new Error(
          `Cannot scope the CLI artifact to "${theme}": no ${pattern}…' glob in ` +
            'arenaThemes.ts. The pattern moved — update vite.cli.config.ts, or every ' +
            'theme will be inlined into every viewer again.',
        );
      return code.split(pattern).join(`'../assets/themes/${theme}/`);
    },
  };
}
