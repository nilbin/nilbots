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
  plugins: [scopeToTheme(THEME), react(), tailwindcss(), viteSingleFile()],
  define: {
    // A scoped artifact has no `control-room` to fall back to, so the fallback becomes the
    // theme it does have. Unscoped builds keep the ordinary default.
    __BOTARENA_DEFAULT_THEME__: JSON.stringify(THEME ?? 'control-room'),
  },
  build: {
    outDir: THEME ? `dist-cli/${THEME}` : 'dist-cli',
  },
});

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
